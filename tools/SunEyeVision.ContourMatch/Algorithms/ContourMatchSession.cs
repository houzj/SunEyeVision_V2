using System.Diagnostics;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 一次同步执行：图像 → 搜索区域（含位置修正）→ 形状模板匹配 → 排序 → 判定 → 结果行/几何 → 输出。
/// 工作流调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class ContourMatchSession : IToolSession<ContourMatchSettings, ContourMatchOutput>, IReusableToolSession
{
    private const double Greediness = 0.9;
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<ContourMatchOutput>> ExecuteAsync(ToolInvocation<ContourMatchSettings> invocation)
    {
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ArgumentNullException.ThrowIfNull(invocation);
            // 参数集已按 HALCON 能力重构：无原生对应的原厂参数已删除，不再需要执行期能力拒绝。
            // 原厂 0x030A TimeOut（秒）：用 linked CTS 做超时取消（0 表示不限），
            // Find 循环内已有 ThrowIfCancellationRequested，故超时可在匹配中途生效。
            var timeoutSeconds = invocation.Settings.Freeze().TimeOut;
            using var timeoutCts = timeoutSeconds > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(invocation.Context.Cancellation)
                : null;
            timeoutCts?.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var output = Execute(invocation, timeoutCts?.Token ?? invocation.Context.Cancellation);
            return ValueTask.FromResult(new ToolOutcome<ContourMatchOutput>(true, output,
                output.IsPass ? BusinessJudgment.Pass : BusinessJudgment.Fail, Array.Empty<Diagnostic>()));
        }
        catch (ContourMatchCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<ContourMatchOutput>.Failed(error.Code, error.Message));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<ContourMatchOutput>.Failed("CONTOURMATCH_CANCELLED", "轮廓匹配已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<ContourMatchOutput>.Failed("CONTOURMATCH_EXECUTION_FAILED", error.Message));
        }
    }

    private sealed record Candidate(long ElementId, double CenterX, double CenterY, double AngleDegrees, double Score, double Scale,
        HRegion Region, ContourMatchBox Box, IReadOnlyList<Point2> Outline, bool Passed, IReadOnlyList<string> FailedLimits,
        int ModelIndex = 0, string ModelName = "", double ScaleX = 0, double ScaleY = 0);

    private static ContourMatchOutput Execute(ToolInvocation<ContourMatchSettings> invocation, CancellationToken cancellation)
    {
        var started = Stopwatch.GetTimestamp();
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new ContourMatchCapabilityException("CONTOURMATCH_SETTINGS_INVALID", string.Join("；", errors.Select(error => error.Message)));
        // 多模板：原厂每个模板挂一个原生模型（FeaturepatMatchConfigPanalDataBase.cs:152-180），
        // 本方把多个模板序列化进 ModelData，逐个建模型后合并结果并做跨模型 NMS。
        var entries = ContourMatchTemplateSet.DecodeEntries(settings.ModelData);
        // 原厂「全部搜索模式」(UseMatchAllMode)：开=搜索全部模板；关=只搜列表里勾选的模板。
        var indexed = entries.Select((entry, index) => (entry, index)).ToArray();
        var active = settings.UseMatchAllMode ? indexed : indexed.Where(item => item.entry.Selected).ToArray();
        if (active.Length == 0)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY",
                "没有可搜索的模板：请勾选“加入搜索列表”的模板，或开启“全部搜索模式”。");
        var templates = active.Select(item => item.entry.Template).ToArray();
        var modelIndices = active.Select(item => item.index).ToArray();
        var modelNames = active.Select(item => string.IsNullOrWhiteSpace(item.entry.Name)
            ? ContourMatchTemplateSet.NewTemplateName(item.index) : item.entry.Name).ToArray();
        foreach (var template in templates) template.RequireAngleCoverage(settings);
        var templateName = TemplateName(settings);

        var inputs = invocation.Inputs;
        var image = inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput)
            ?? throw new ContourMatchCapabilityException("CONTOURMATCH_IMAGE_REQUIRED", "缺少输入图像。");
        var fixture = OptionalFixture(inputs);

        using var native = HalconResourceBridge.CopyImage(image);
        HOperatorSet.GetImageSize(native, out var widthTuple, out var heightTuple);
        using var widthOwner = widthTuple;
        using var heightOwner = heightTuple;
        var width = widthTuple.I;
        var height = heightTuple.I;
        using var gray = HalconImageAdmission.Gray(native);
        using var domain = gray.GetDomain();
        var frame = image.Frame;

        // 搜索区域二选一（原厂 Tab_ROI Area 的绘制/继承）：Local 走本模块 ROI 文档栅格化，
        // Inherited 走上游 RoiInput 端口。两条路径都产出 HRegion，后续位置修正/求交/ReduceDomain 共用。
        using var roiRegion = ResolveSearchRegion(inputs, image, frame, width, height);
        using var posedRoi = fixture is null ? roiRegion.Clone() : HalconResourceBridge.TransformRegion(roiRegion, FixtureDeclarations.ToAffine2D(fixture, ContourMatchSchema.ImageFrame));
        using var baseSearch = domain.Intersection(posedRoi);
        // 原厂 MatchExtentRate「延拓阈值」(0~90) 等价映射：把搜索域按图像短边的百分比外扩，
        // 使目标跨越 ROI 边界时仍可被找到；外扩结果再与有效域相交，不越界。
        // 说明：这是基于参数名与取值范围的可解释等价，不是复刻原厂原生算法的轮廓延拓。
        using var dilated = DilateForExtent(baseSearch, settings.MatchExtentRate, width, height);
        using var search = settings.MatchExtentRate > 0 ? domain.Intersection(dilated) : dilated.Clone();
        RequireSearchArea(search);

        using var reduced = gray.ReduceDomain(search);
        var modelIds = new List<HTuple>(templates.Length);
        var outputResources = invocation.Context.Resources.CreateScope();
        try
        {
            foreach (var template in templates) modelIds.Add(template.LoadModel());
            var matches = FindAll(reduced, modelIds, templates, modelIndices, modelNames, settings, templateName, cancellation);
            var judged = Judge(settings, matches);
            try
            {
                var measurements = new List<ContourMatchMeasurement>(judged.Items.Length);
                foreach (var candidate in judged.Items)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var captured = HalconResourceBridge.CaptureRegion(candidate.Region, ContourMatchSchema.ImageFrame, frame, outputResources);
                    measurements.Add(new(candidate.ElementId, candidate.CenterX, candidate.CenterY, candidate.AngleDegrees,
                        candidate.Score, candidate.Scale, candidate.Box, candidate.Outline, captured, candidate.Passed, candidate.FailedLimits,
                        candidate.ModelIndex, candidate.ModelName, candidate.ScaleX, candidate.ScaleY));
                }
                var modulePassed = judged.Overall && (measurements.Count > 0 || settings.OkWhenCountIsZero) && measurements.All(item => item.Passed);
                // 原厂 ROI 输出 = 检测区域外接框（DetectRectCenterPoint/DetectWidth/Height/Angle）。
                var projection = ContourMatchResultProjection.Create(measurements, modulePassed,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds, fixture, DetectRoiBox(search), templateName);
                var geometry = ContourMatchResultGeometry.Capture(image, modulePassed, search, fixture, cancellation);
                var output = new ContourMatchOutput(settings, projection, measurements.AsReadOnly(),
                    judged.Messages, geometry, new IDisposable[] { outputResources });
                invocation.Context.Log($"[Perf] tool=ContourMatch count={projection.Count} templates={templates.Length} elapsed={projection.ModuleRunTime:0.###}ms");
                return output;
            }
            finally
            {
                foreach (var candidate in judged.Items) candidate.Region.Dispose();
            }
        }
        catch (Exception failure)
        {
            try { outputResources.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
        finally
        {
            foreach (var modelId in modelIds) ContourMatchTemplate.Clear(modelId);
        }
    }

    /// <summary>
    /// 多模板匹配：逐模板 Find 后合并，按原厂「多模板重叠率」(NMS Overlap Threshold) 做跨模型去重，
    /// 再按排序类型重排并统一编号（原厂 MatchModelIndex 因此有真实值）。
    /// </summary>
    private static Candidate[] FindAll(HImage reduced, IReadOnlyList<HTuple> modelIds, IReadOnlyList<ContourMatchTemplate> templates,
        IReadOnlyList<int> modelIndices, IReadOnlyList<string> modelNames,
        ContourMatchSettings settings, string templateName, CancellationToken cancellation)
    {
        if (modelIds.Count == 1)
        {
            var single = Find(reduced, modelIds[0], templates[0], settings, cancellation);
            var name = modelNames.Count > 0 ? modelNames[0] : templateName;
            var modelIndex = modelIndices.Count > 0 ? modelIndices[0] : 0;
            return single.Select(candidate => candidate with { ModelIndex = modelIndex, ModelName = name }).ToArray();
        }
        var pool = new List<Candidate>();
        try
        {
            for (var index = 0; index < modelIds.Count; index++)
                foreach (var candidate in Find(reduced, modelIds[index], templates[index], settings, cancellation))
                    pool.Add(candidate with { ModelIndex = modelIndices[index], ModelName = modelNames[index] });
            var threshold = Math.Clamp(settings.MultiModelMaxOverlap, 0, 100) / 100d;
            var kept = new List<Candidate>(pool.Count);
            foreach (var candidate in pool.OrderByDescending(item => item.Score))
            {
                if (kept.Any(item => OverlapRatio(candidate.Region, item.Region) > threshold))
                {
                    candidate.Region.Dispose();
                    continue;
                }
                kept.Add(candidate);
                if (kept.Count >= settings.MaxMatchCount) break;
            }
            foreach (var candidate in pool.Where(item => !kept.Contains(item))) candidate.Region.Dispose();
            return Number(Order(kept, settings));
        }
        catch
        {
            foreach (var candidate in pool) candidate.Region.Dispose();
            throw;
        }
    }

    /// <summary>两匹配框的交并比（HALCON 区域面积，精确值而非轴对齐近似）。</summary>
    private static double OverlapRatio(HRegion left, HRegion right)
    {
        HOperatorSet.Intersection(left, right, out var intersection);
        using (intersection)
        using (var intersectionArea = new HRegion(intersection))
        {
            HOperatorSet.AreaCenter(left, out var leftArea, out var ignoredRow1, out var ignoredColumn1);
            HOperatorSet.AreaCenter(right, out var rightArea, out var ignoredRow2, out var ignoredColumn2);
            using (leftArea) using (rightArea) using (ignoredRow1) using (ignoredColumn1) using (ignoredRow2) using (ignoredColumn2)
            {
                HOperatorSet.AreaCenter(intersectionArea, out var overlapArea, out var ignoredRow3, out var ignoredColumn3);
                using (overlapArea) using (ignoredRow3) using (ignoredColumn3)
                {
                    var union = leftArea.D + rightArea.D - overlapArea.D;
                    return union <= 0 ? 0 : overlapArea.D / union;
                }
            }
        }
    }

    /// <summary>
    /// 解析运行时搜索域：Local 模式栅格化本地 ROI 文档；Inherited（默认）走上游 RoiInput 端口。
    /// 本地路径是 Halcon 原生 HRegion（已按像素坐标构造），无需再经 CopyRegion 转换。
    /// </summary>
    private static HRegion ResolveSearchRegion(InputValues values, IReadOnlyImage image, ImageFrameAddress frame, int width, int height)
    {
        var local = ContourMatchSearchRois.ResolveLocal(values, width, height);
        if (local is not null) return local;
        var roi = OptionalRegion(values, ContourMatchSchema.RoiInput)
            ?? throw new ContourMatchCapabilityException("CONTOURMATCH_ROI_REQUIRED", "请为搜索区域连接区域输入，或在参数页本地绘制搜索区域。");
        return HalconResourceBridge.CopyRegion(roi, frame, width, height);
    }

    private static IReadOnlyRegion? OptionalRegion(InputValues values, string id)
        => values.Contains(id) ? values.Get<IReadOnlyRegion?>(id) : null;

    private static RecordValue? OptionalFixture(InputValues values)
        => ContourMatchFixtureControl.Read(values);

    private static void RequireSearchArea(HRegion search)
    {
        HOperatorSet.AreaCenter(search, out var area, out var row, out var column);
        using (area) using (row) using (column)
            if (area.Length == 0 || area.LArr.All(value => value == 0))
                throw new ContourMatchCapabilityException("CONTOURMATCH_ROI_EMPTY", "搜索区域与图像有效域没有交集：请检查区域与位置修正。");
    }

    /// <summary>
    /// 原厂 MatchModelName：本方单模板，名称取训练样本（TrainingRois 的 inspection 组首个命名项）。
    /// 无命名样本时返回空串（不臆造名称）。
    /// </summary>
    private static string TemplateName(ContourMatchSettings settings)
    {
        try
        {
            foreach (var item in RoiDocumentValue.Items(settings.TrainingRois, RoiDocumentValue.Inspection))
            {
                var name = item.Get<string?>("name");
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            // 文档结构异常时按未命名处理，由执行结果体现为空名称，不掩盖真实异常类型。
        }
        return "";
    }

    /// <summary>
    /// 搜索域按延拓阈值外扩（原厂 MatchExtentRate 0~90，按图像短边百分比）。
    /// 0 表示不延拓。
    /// </summary>
    private static HRegion DilateForExtent(HRegion region, int extentRate, int imageWidth, int imageHeight)
    {
        if (extentRate <= 0) return region.Clone();
        var span = (int)Math.Round(Math.Min(imageWidth, imageHeight) * Math.Clamp(extentRate, 0, 90) / 100.0);
        if (span <= 0) return region.Clone();
        HOperatorSet.DilationRectangle1(region, out var dilated, span * 2 + 1, span * 2 + 1);
        return new HRegion(dilated);
    }

    /// <summary>
    /// 原厂 ROI 输出（检测区域）：本次实际搜索区域的外接框。HALCON 坐标为 row=y / column=x。
    /// </summary>
    private static ContourMatchBox? DetectRoiBox(HRegion search)
    {
        try
        {
            HOperatorSet.SmallestRectangle1(search, out var row1, out var column1, out var row2, out var column2);
            using (row1) using (column1) using (row2) using (column2)
            {
                if (row1.Length == 0 || column1.Length == 0 || row2.Length == 0 || column2.Length == 0) return null;
                var top = row1.D;
                var left = column1.D;
                var bottom = row2.D;
                var right = column2.D;
                if (bottom < top || right < left) return null;
                return new ContourMatchBox((left + right) / 2, (top + bottom) / 2,
                    right - left + 1, bottom - top + 1, 0);
            }
        }
        catch (HalconException)
        {
            return null;
        }
    }

    /// <summary>形状模板匹配；返回按设置排序后的候选（仍未判定）。</summary>
    private static Candidate[] Find(HImage reduced, HTuple modelId, ContourMatchTemplate template, ContourMatchSettings settings,
        CancellationToken cancellation)
    {
        using var angleStart = new HTuple(settings.AngleStart * Math.PI / 180d);
        using var angleExtent = new HTuple(Math.Max(0.0001, (settings.AngleEnd - settings.AngleStart) * Math.PI / 180d));
        using var minScore = new HTuple(settings.MinScore);
        using var numMatches = new HTuple(settings.MaxMatchCount);
        using var maxOverlap = new HTuple(Math.Clamp(settings.MaxOverlap, 0, 100) / 100d);
        using var subPixel = new HTuple(ContourMatchSettings.HalconSubPixel(settings.SubPixel));
        // 特征尺度（PyramidScaleRLevel）→ 匹配期使用的模板层数；自动模式由 HALCON 自行决定。
        using var numLevels = new HTuple(settings.PyramidMode == ContourMatchPyramidMode.Manual
            ? Math.Clamp(settings.PyramidScaleRLevel, 0, Math.Max(1, template.LevelCount))
            : 0);
        using var greediness = new HTuple(Math.Clamp(settings.Greediness, 0, 1));
        // 原厂 0x0307/0308 尺度范围作为运行期筛选；创建期模型范围已覆盖运行期设置。
        using var scaleMin = new HTuple(settings.ScaleStart);
        using var scaleMax = new HTuple(Math.Max(settings.ScaleStart, settings.ScaleEnd));
        // 各向异性模型：X/Y（列/行）尺度独立，对应原厂「尺度范围XY」与 X/Y 独立尺度搜索。
        using var scaleRowMin = new HTuple(settings.ScaleYStart);
        using var scaleRowMax = new HTuple(Math.Max(settings.ScaleYStart, settings.ScaleYEnd));
        using var scaleColumnMin = new HTuple(settings.ScaleXStart);
        using var scaleColumnMax = new HTuple(Math.Max(settings.ScaleXStart, settings.ScaleXEnd));

        HTuple rows, columns, angles, scores, scaleRows, scaleColumns;
        if (template.Anisotropic)
            HOperatorSet.FindAnisoShapeModel(reduced, modelId, angleStart, angleExtent,
                scaleRowMin, scaleRowMax, scaleColumnMin, scaleColumnMax,
                minScore, numMatches, maxOverlap, subPixel, numLevels, greediness,
                out rows, out columns, out angles, out scaleRows, out scaleColumns, out scores);
        else
        {
            HOperatorSet.FindScaledShapeModel(reduced, modelId, angleStart, angleExtent, scaleMin, scaleMax,
                minScore, numMatches, maxOverlap, subPixel, numLevels, greediness,
                out rows, out columns, out angles, out var scales, out scores);
            // 等比模型的行列尺度相同（同一元组，由 scaleRows/scaleColumns 共用持有）。
            scaleRows = scales; scaleColumns = scales;
        }
        using (rows) using (columns) using (angles) using (scores) using (scaleRows) using (scaleColumns)
        {
            var aniso = template.Anisotropic;
            var count = rows.Length;
            if (count != columns.Length || count != angles.Length || count != scores.Length
                || count != scaleRows.Length || count != scaleColumns.Length)
                throw new ContourMatchCapabilityException("CONTOURMATCH_NATIVE_RESULT", "HALCON 返回的匹配元组长度不一致。");
            if (count == 0) return Array.Empty<Candidate>();
            cancellation.ThrowIfCancellationRequested();
            using var modelContours = ModelContours(modelId);
            var candidates = new List<Candidate>(count);
            try
            {
                for (var index = 0; index < count; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var scaleRow = scaleRows[index].D;
                    var scaleColumn = scaleColumns[index].D;
                    // 等比模型时行列尺度相同，几何平均即原始尺度。
                    var scaleGeometric = Math.Sqrt(Math.Abs(scaleRow * scaleColumn));
                    using var homMat = HomMatAniso(rows[index].D, columns[index].D, angles[index].D, scaleRow, scaleColumn);
                    HOperatorSet.AffineTransContourXld(modelContours, out var moved, homMat);
                    using (moved)
                    {
                        HOperatorSet.SmallestRectangle2Xld(moved, out var boxRow, out var boxColumn, out var boxPhi, out var length1, out var length2);
                        using (boxRow) using (boxColumn) using (boxPhi) using (length1) using (length2)
                        {
                            if (length1.D <= 0 || length2.D <= 0)
                                throw new ContourMatchCapabilityException("CONTOURMATCH_NATIVE_RESULT", "匹配结果外接矩形退化。");
                            HOperatorSet.GenRectangle2(out var boxRegion, boxRow, boxColumn, boxPhi, length1, length2);
                            using (boxRegion)
                            {
                                // 原厂 OutLineEnable（轮廓）/ OutLinePointEnable（轮廓点）任一开启都需要轮廓点集：
                                // 本方 output.outline-points 承载逐点明细（含 match-index），故两者取或。
                                var outline = settings.OutlineEnabled || settings.OutlinePointsEnabled
                                    ? ReadPoints(moved, cancellation) : Array.Empty<Point2>();
                                var box = new ContourMatchBox(boxColumn.D, boxRow.D, length1.D * 2, length2.D * 2,
                                    NormalizeDegrees(-boxPhi.D * 180d / Math.PI));
                                candidates.Add(new(0, boxColumn.D, boxRow.D, NormalizeDegrees(angles[index].D * 180d / Math.PI),
                                    scores[index].D, scaleGeometric, new HRegion(boxRegion), box, outline, true, Array.Empty<string>(),
                                    ScaleX: scaleColumn, ScaleY: scaleRow));
                            }
                        }
                    }
                }
            }
            catch
            {
                foreach (var candidate in candidates) candidate.Region.Dispose();
                throw;
            }
            return Number(Order(candidates, settings));
        }
    }

    private static HObject ModelContours(HTuple modelId)
    {
        using var level = new HTuple(1);
        HOperatorSet.GetShapeModelContours(out var contours, modelId, level);
        if (contours is null) throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板没有可用轮廓。");
        return contours;
    }

    /// <summary>各向异性仿射：列方向(x)与行方向(y)分别缩放，再旋转平移到目标位姿。</summary>
    private static HTuple HomMatAniso(double row, double column, double angle, double scaleRow, double scaleColumn)
    {
        using var zero = new HTuple(0d);
        using var rowTuple = new HTuple(row);
        using var columnTuple = new HTuple(column);
        using var angleTuple = new HTuple(angle);
        HOperatorSet.VectorAngleToRigid(zero, zero, zero, rowTuple, columnTuple, angleTuple, out var rigid);
        using (rigid)
        {
            using var scaleX = new HTuple(scaleColumn);
            using var scaleY = new HTuple(scaleRow);
            HOperatorSet.HomMat2dScale(rigid, scaleX, scaleY, zero, zero, out var homMat);
            return homMat;
        }
    }

    private static HTuple HomMat(double row, double column, double angle, double scale)
    {
        using var zero = new HTuple(0d);
        using var rowTuple = new HTuple(row);
        using var columnTuple = new HTuple(column);
        using var angleTuple = new HTuple(angle);
        HOperatorSet.VectorAngleToRigid(zero, zero, zero, rowTuple, columnTuple, angleTuple, out var rigid);
        using (rigid)
        {
            using var scaleRow = new HTuple(scale);
            using var scaleCol = new HTuple(scale);
            HOperatorSet.HomMat2dScale(rigid, scaleRow, scaleCol, zero, zero, out var homMat);
            return homMat;
        }
    }

    private static IReadOnlyList<Point2> ReadPoints(HObject contours, CancellationToken cancellation)
    {
        HOperatorSet.CountObj(contours, out var count);
        using (count)
        {
            var points = new List<Point2>(256);
            for (var index = 1; index <= count.I; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                HOperatorSet.SelectObj(contours, out var selected, index);
                using (selected)
                {
                    HOperatorSet.GetContourXld(selected, out var rows, out var columns);
                    using (rows) using (columns)
                        for (var point = 0; point < rows.Length; point++)
                        {
                            if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                            points.Add(new Point2(columns[point].D, rows[point].D));
                        }
                }
            }
            return points.AsReadOnly();
        }
    }

    /// <summary>原件排序方式（同族 SDK 枚举语义）：分数降序/角度降序/X升序/Y升序/X升序·Y升序/Y升序·X升序。</summary>
    private static Candidate[] Order(IReadOnlyList<Candidate> candidates, ContourMatchSettings settings)
        => settings.SortType switch
        {
            ContourMatchSortType.Score => candidates.OrderByDescending(item => item.Score).ToArray(),
            ContourMatchSortType.Angle => candidates.OrderByDescending(item => item.AngleDegrees).ToArray(),
            ContourMatchSortType.X => candidates.OrderBy(item => item.CenterX).ToArray(),
            ContourMatchSortType.Y => candidates.OrderBy(item => item.CenterY).ToArray(),
            ContourMatchSortType.XY => candidates.OrderBy(item => item.CenterX).ThenBy(item => item.CenterY).ToArray(),
            ContourMatchSortType.YX => candidates.OrderBy(item => item.CenterY).ThenBy(item => item.CenterX).ToArray(),
            _ => throw new ContourMatchCapabilityException("CONTOURMATCH_SETTINGS_INVALID", "排序方式取值非法。")
        };

    /// <summary>排序完成后按顺序编号，序号即结果表/输出数组的下标。</summary>
    private static Candidate[] Number(IReadOnlyList<Candidate> ordered)
    {
        var result = new Candidate[ordered.Count];
        for (var index = 0; index < ordered.Count; index++) result[index] = ordered[index] with { ElementId = index + 1 };
        return result;
    }

    /// <summary>匹配个数为整体判定，其余限位组逐匹配给出单项结果。</summary>
    private static (Candidate[] Items, bool Overall, IReadOnlyList<string> Messages) Judge(ContourMatchSettings settings, Candidate[] candidates)
    {
        var messages = new List<string>();
        var overall = true;
        void Overall(string name, bool passed) { overall &= passed; if (!passed) messages.Add("[NG] " + name); }
        if (settings.CountLimitEnabled)
            Overall("匹配个数判断", candidates.Length >= settings.CountLimitLow && candidates.Length <= settings.CountLimitHigh);
        if (candidates.Length == 0 && !settings.OkWhenCountIsZero) overall = false;

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var failures = new List<string>();
            void PerMatch(string name, bool passed)
            {
                if (passed) return;
                failures.Add(name);
                messages.Add("[NG] " + name);
            }
            if (settings.AngleLimitEnabled) PerMatch("角度判断", InRange(candidate.AngleDegrees, settings.AngleLimitLow, settings.AngleLimitHigh));
            if (settings.ScaleLimitEnabled) PerMatch("尺度判断", InRange(candidate.Scale, settings.ScaleLimitLow, settings.ScaleLimitHigh));
            // 原厂 X/Y 尺度判断：各向异性模型用列/行真实尺度，等比模型两者相同。
            if (settings.ScaleXLimitEnabled) PerMatch("X尺度判断", InRange(candidate.ScaleX, settings.ScaleXLimitLow, settings.ScaleXLimitHigh));
            if (settings.ScaleYLimitEnabled) PerMatch("Y尺度判断", InRange(candidate.ScaleY, settings.ScaleYLimitLow, settings.ScaleYLimitHigh));
            if (settings.ScoreLimitEnabled) PerMatch("分数判断", InRange(candidate.Score, settings.ScoreLimitLow, settings.ScoreLimitHigh));
            if (settings.MatchPointXLimitEnabled) PerMatch("匹配点X判断", InRange(candidate.CenterX, settings.MatchPointXLimitLow, settings.MatchPointXLimitHigh));
            if (settings.MatchPointYLimitEnabled) PerMatch("匹配点Y判断", InRange(candidate.CenterY, settings.MatchPointYLimitLow, settings.MatchPointYLimitHigh));
            var corners = Corners(candidate.Box);
            if (settings.BoxPointXLimitEnabled)
                PerMatch("Box点X判断", corners.All(point => InRange(point.X, settings.BoxPointXLimitLow, settings.BoxPointXLimitHigh)));
            if (settings.BoxPointYLimitEnabled)
                PerMatch("Box点Y判断", corners.All(point => InRange(point.Y, settings.BoxPointYLimitLow, settings.BoxPointYLimitHigh)));
            candidates[index] = candidate with { Passed = failures.Count == 0, FailedLimits = failures.AsReadOnly() };
        }
        return (candidates, overall, messages.AsReadOnly());
    }

    /// <summary>匹配框四个角点（图像像素坐标，X 向右、Y 向下）。</summary>
    private static Point2[] Corners(ContourMatchBox box)
    {
        var halfWidth = box.Width / 2;
        var halfHeight = box.Height / 2;
        // Box.AngleDegrees 已按产品约定取反（HALCON phi 以行轴为基准），这里换回屏幕角度。
        var radians = -box.AngleDegrees * Math.PI / 180d;
        var (sin, cos) = Math.SinCos(radians);
        Point2 Corner(double alongWidth, double alongHeight) => new(
            box.CenterX + alongWidth * cos - alongHeight * sin,
            box.CenterY + alongWidth * sin + alongHeight * cos);
        return new[]
        {
            Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight),
            Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight)
        };
    }

    private static bool InRange(double value, double minimum, double maximum)
        => minimum <= maximum ? value >= minimum && value <= maximum : value >= minimum || value <= maximum;

    internal static double NormalizeDegrees(double angle) => Math.IEEERemainder(angle, 360d);
}
