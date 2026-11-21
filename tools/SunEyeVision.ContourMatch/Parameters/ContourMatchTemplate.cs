using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HalconDotNet;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>模板在模型坐标系里的外接矩形（HALCON row/column 语义，phi 为弧度）。</summary>
public sealed record ContourMatchTemplateBox(double Row, double Column, double Phi, double Length1, double Length2);

/// <summary>
/// 轮廓匹配模板。原件把模板存成模块字节数据（<c>HPPyramidFeaturepatMatchDialogData.CreatePatE</c>
/// 调 <c>CreateDipolePyramid</c>，模型经 <c>FeaturepatMatchConfigPanalDataBase.AddModelToModule</c>
/// 以 <c>AddSpareModel</c> 写入模块），导出为加密 <c>.contourmxml</c>。
/// 本工具按架构边界改为随工具设置持久化的中立文本（base64 承载 HALCON 形状模板序列化项 + 外接矩形 +
/// 模型角度覆盖范围），不兼容原件加密文件格式。模板创建、读入与释放全部在 Runtime，UI 只传图像/区域。
/// </summary>
public sealed class ContourMatchTemplate
{
    /// <summary>载荷格式版本；格式变化必须提升本串，旧载荷直接报错而不是猜测。</summary>
    public const string Format = "SunEyeVision.ContourMatch.Model/1";
    public const string FileExtension = ".cmatchmodel";

    /// <summary>模型创建期覆盖完整一圈角度，运行期角度范围因此总能落在模型范围内。</summary>
    private const double ModelAngleStartRadians = -Math.PI;
    private const double ModelAngleExtentRadians = 2 * Math.PI;
    private const double ModelAngleStepRadians = Math.PI / 180d;

    private const string ShapeModelKey = "shape-model";
    private const string FormatKey = "format";

    private readonly byte[] _serialized;

    private ContourMatchTemplate(byte[] serialized, ContourMatchTemplateBox box, int levelCount, int pointCount,
        double angleStart, double angleExtent)
    {
        _serialized = serialized;
        Box = box;
        LevelCount = levelCount;
        PointCount = pointCount;
        AngleStart = angleStart;
        AngleExtent = angleExtent;
    }

    public ContourMatchTemplateBox Box { get; }
    public int LevelCount { get; }
    public int PointCount { get; }
    /// <summary>模型覆盖的角度起点（弧度，HALCON 语义）。</summary>
    public double AngleStart { get; }
    /// <summary>模型覆盖的角度跨度（弧度）。</summary>
    public double AngleExtent { get; }

    // ── 创建 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 用模板参数在给定图像（可带 ROI 域）上建立形状模板。图像为空、区域为空或原生调用失败一律抛出
    /// <see cref="ContourMatchCapabilityException"/>，不做默认模型兜底。
    /// </summary>
    public static ContourMatchTemplate Create(HImage gray, HRegion? roi, ContourMatchSettings settings, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(gray);
        ArgumentNullException.ThrowIfNull(settings);
        cancellation.ThrowIfCancellationRequested();
        using var count = gray.CountChannels();
        if (count.I != 1) throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_IMAGE_INVALID", "模板图像必须是单通道灰度图。");
        // 训练域优先取模板窗口的正/背景样本区域；无样本时回退到搜索区域（兼容只连 ROI 的旧方案）。
        // 样本区域身份与正负语义见 ContourMatchTrainingRois 与原厂 SignList 证据。
        HOperatorSet.GetImageSize(gray, out var widthTuple, out var heightTuple);
        int imageWidth, imageHeight;
        using (widthTuple) using (heightTuple) { imageWidth = widthTuple.I; imageHeight = heightTuple.I; }
        using var trainingDomain = ContourMatchTrainingRois.BuildDomain(settings.TrainingRois, imageWidth, imageHeight);
        if (trainingDomain is null && roi is null)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REGION_EMPTY",
                "模板区域为空：请在模板窗口绘制正样本，或连接有效的搜索区域。");
        using var effectiveRoi = trainingDomain ?? roi;
        using var reduced = gray.ReduceDomain(effectiveRoi!);
        HOperatorSet.AreaCenter(reduced, out var area, out var ignoredRow, out var ignoredColumn);
        using (area) using (ignoredRow) using (ignoredColumn)
            if (area.D <= 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REGION_EMPTY", "模板区域为空：请先连接有效的搜索区域或在模板窗口绘制正样本。");

        // 压缩比（PyramidScaleLevel）→ 建模板金字塔层数；自动模式交给 HALCON 自适应。
        var manual = settings.PyramidMode == ContourMatchPyramidMode.Manual;
        using var numLevels = manual ? new HTuple(Math.Clamp(settings.PyramidScaleLevel, 1, 10)) : new HTuple("auto");
        // 边缘阈值（EdgeThreshold）→ 建模板对比度；自动模式交给 HALCON 自适应。
        using var contrast = settings.EdgeThresholdMode == ContourMatchThresholdMode.Manual
            ? new HTuple(Math.Clamp(settings.EdgeThreshold, 1, 255))
            : new HTuple("auto");
        using var angleStart = new HTuple(ModelAngleStartRadians);
        using var angleExtent = new HTuple(ModelAngleExtentRadians);
        using var angleStep = new HTuple(ModelAngleStepRadians);
        using var optimization = new HTuple("auto");
        // 原厂 0x0303 Polarity：0=忽略极性、1=考虑极性 → HALCON CreateShapeModel metric。
        // 模板每次运行按当前 settings 重建，故运行参数变更自然生效（逻辑对照；精度由测试人员验证）。
        using var metric = new HTuple(settings.Polarity == 1 ? "use_polarity" : "ignore_polarity");
        using var minContrast = new HTuple(5);

        // 原厂 0x0307/0308 尺度范围：创建期建宽范围多尺度模型，运行期由 ScaleStart/End 筛选
        // （使运行参数变更立即生效，对齐原厂"运行参数"语义）。见 ContourMatchSession.FindScaledShapeModel。
        using var scaleMin = new HTuple(0.5);
        using var scaleMax = new HTuple(2.0);
        using var scaleStep = new HTuple("auto");
        HOperatorSet.CreateScaledShapeModel(reduced, numLevels, angleStart, angleExtent, angleStep,
            scaleMin, scaleMax, scaleStep, optimization, metric, contrast, minContrast, out var modelId);
        using (modelId)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.GetShapeModelParams(modelId, out var modelLevels, out var modelAngleStart, out var modelAngleExtent,
                out var modelAngleStep, out var modelOptimization, out var modelMetric, out var modelContrast,
                out var modelMinContrast, out var modelPoints);
            using (modelAngleStart) using (modelAngleExtent) using (modelAngleStep) using (modelOptimization)
            using (modelMetric) using (modelContrast) using (modelMinContrast) using (modelLevels) using (modelPoints)
            {
                var levels = modelLevels.I;
                var points = modelPoints.I;
                if (points <= 0)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板区域没有可用边缘：请调整区域或边缘阈值后重建模板。");
                var box = ReadBox(modelId);
                var serialized = Serialize(modelId);
                return new ContourMatchTemplate(serialized, box, levels, points, modelAngleStart.D, modelAngleExtent.D);
            }
        }
    }

    private static ContourMatchTemplateBox ReadBox(HTuple modelId)
    {
        using var level = new HTuple(1);
        HOperatorSet.GetShapeModelContours(out var contours, modelId, level);
        using (contours)
        {
            HOperatorSet.SmallestRectangle2Xld(contours, out var row, out var column, out var phi, out var length1, out var length2);
            using (row) using (column) using (phi) using (length1) using (length2)
            {
                if (length1.D <= 0 || length2.D <= 0)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板轮廓退化：区域内没有可用的边缘特征。");
                return new ContourMatchTemplateBox(row.D, column.D, phi.D, length1.D, length2.D);
            }
        }
    }

    private static byte[] Serialize(HTuple modelId)
    {
        HOperatorSet.SerializeShapeModel(modelId, out var item);
        try
        {
            HOperatorSet.GetSerializedItemPtr(item, out var pointer, out var size);
            using (pointer) using (size)
            {
                if (size.I <= 0) throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_SERIALIZE", "模板序列化结果为空。");
                var bytes = new byte[size.I];
                Marshal.Copy(pointer.IP, bytes, 0, bytes.Length);
                return bytes;
            }
        }
        finally
        {
            HOperatorSet.ClearSerializedItem(item);
            item.Dispose();
        }
    }

    // ── 载荷编解码 ───────────────────────────────────────────────────────

    public string Encode()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(FormatKey, Format);
            writer.WriteString(ShapeModelKey, Convert.ToBase64String(_serialized));
            writer.WriteNumber("row", Box.Row);
            writer.WriteNumber("column", Box.Column);
            writer.WriteNumber("phi", Box.Phi);
            writer.WriteNumber("length1", Box.Length1);
            writer.WriteNumber("length2", Box.Length2);
            writer.WriteNumber("levels", LevelCount);
            writer.WriteNumber("points", PointCount);
            writer.WriteNumber("angle-start", AngleStart);
            writer.WriteNumber("angle-extent", AngleExtent);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>解析模板载荷；空、格式不符或缺项一律抛出明确异常（不猜测、不返回空模板）。</summary>
    public static ContourMatchTemplate Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REQUIRED", "尚未创建模板：请先在“模板参数”中新建模板。");
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException error)
        {
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据不是有效载荷：" + error.Message);
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据必须是一个对象。");
            if (!root.TryGetProperty(FormatKey, out var format) || format.GetString() != Format)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_VERSION",
                    "模板数据版本不受支持（需要 " + Format + "）。");
            if (!root.TryGetProperty(ShapeModelKey, out var model) || model.GetString() is not { Length: > 0 } encoded)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据缺少形状模板内容。");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException error)
            {
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "形状模板内容不是有效 base64：" + error.Message);
            }
            var box = new ContourMatchTemplateBox(
                ReadNumber(root, "row"), ReadNumber(root, "column"), ReadNumber(root, "phi"),
                ReadNumber(root, "length1"), ReadNumber(root, "length2"));
            var levels = (int)ReadNumber(root, "levels");
            var points = (int)ReadNumber(root, "points");
            var angleStart = ReadNumber(root, "angle-start");
            var angleExtent = ReadNumber(root, "angle-extent");
            if (box.Length1 <= 0 || box.Length2 <= 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板外接矩形无效。");
            if (angleExtent <= 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板角度覆盖范围无效。");
            return new ContourMatchTemplate(bytes, box, levels, points, angleStart, angleExtent);
        }
    }

    private static double ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据缺少有效数值：" + name);
        return number;
    }

    // ── 执行期装载 ───────────────────────────────────────────────────────

    /// <summary>把载荷反序列化为 HALCON 形状模板句柄；调用方负责 <see cref="Clear"/>。</summary>
    public HTuple LoadModel()
    {
        var pointer = Marshal.AllocHGlobal(_serialized.Length);
        try
        {
            Marshal.Copy(_serialized, 0, pointer, _serialized.Length);
            using var pointerTuple = new HTuple(pointer);
            using var sizeTuple = new HTuple(_serialized.Length);
            HOperatorSet.CreateSerializedItemPtr(pointerTuple, sizeTuple, "true", out var item);
            using (item)
            {
                HOperatorSet.DeserializeShapeModel(item, out var modelId);
                return modelId;
            }
        }
        catch (HOperatorException error)
        {
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_LOAD", "模板装载失败：" + error.GetErrorMessage());
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    public static void Clear(HTuple modelId)
    {
        HOperatorSet.ClearShapeModel(modelId);
        modelId.Dispose();
    }

    /// <summary>运行期角度范围必须落在模型覆盖范围内，否则明确报错而不是给出空结果。</summary>
    public void RequireAngleCoverage(ContourMatchSettings settings)
    {
        var requestedStart = settings.AngleStart * Math.PI / 180d;
        var requestedEnd = settings.AngleEnd * Math.PI / 180d;
        var modelEnd = AngleStart + AngleExtent;
        if (requestedStart < AngleStart - 1e-9 || requestedEnd > modelEnd + 1e-9)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_ANGLE_RANGE",
                "运行角度范围超出模板覆盖范围，请重建模板后再执行。");
    }

    /// <summary>面板用的一行摘要；模板无效时由调用方显示错误，不伪造数值。</summary>
    public string Describe()
        => $"边点数 {PointCount}，金字塔层数 {LevelCount}，外接矩形 {Box.Length1 * 2:0.#}×{Box.Length2 * 2:0.#} 像素";

    public static bool TryDecode(string? payload, out ContourMatchTemplate? template, out string error)
    {
        try
        {
            template = Decode(payload);
            error = "";
            return true;
        }
        catch (ContourMatchCapabilityException failure)
        {
            template = null;
            error = failure.Message;
            return false;
        }
    }
}
