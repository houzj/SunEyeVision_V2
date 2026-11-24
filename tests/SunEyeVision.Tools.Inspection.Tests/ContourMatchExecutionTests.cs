using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.ContourMatch.Runtime;
using SunEyeVision.Tool.ContourMatch.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>按原厂链路验收：图像 → 搜索区域 → 模板学习 → 匹配 → 排序/判定 → 结果行/几何 → 输出/释放。</summary>
public sealed class ContourMatchExecutionTests
{
    private const int Size = 128;

    private sealed class StubContext(IOutputResources resources) : IToolExecutionContext
    {
        private readonly List<string> _logs = new();
        public RunIdentity Identity { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "contour-match", Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = resources;
        public void Log(string message) => _logs.Add(message);
        public IReadOnlyList<string> Logs => _logs;
    }

    /// <summary>非对称 L 形目标：水平臂 24×6 + 竖直臂 6×18，避免 90 度自相似。</summary>
    private static byte[] Mark(int size, int x, int y)
    {
        var pixels = new byte[size * size];
        for (var row = 0; row < 6; row++)
        for (var column = 0; column < 24; column++)
            pixels[(y + row) * size + x + column] = 255;
        for (var row = 0; row < 18; row++)
        for (var column = 0; column < 6; column++)
            pixels[(y + row) * size + x + column] = 255;
        return pixels;
    }

    private static IReadOnlyRegion FullRegion(FrozenOutputResources owner, IReadOnlyImage image)
    {
        var runs = Enumerable.Range(0, image.Height).Select(row => new RegionRun(row, 0, image.Width - 1)).ToArray();
        return owner.CreateRegion(image.EffectiveDomain.Frame, image.Frame, runs);
    }

    private static string CreateTemplate(FrozenOutputResources owner, byte[] pixels, ContourMatchSettings? settings = null)
    {
        var image = owner.CreateGray8(Size, Size, pixels);
        var roi = FullRegion(owner, image);
        return ContourMatchTemplateBuilder.Create(image, roi, settings ?? new ContourMatchSettings());
    }

    private static async Task<ToolOutcome<ContourMatchOutput>> RunAsync(ContourMatchSettings settings, byte[] pixels,
        FrozenOutputResources owner, bool withRoi = true)
    {
        var tool = new ContourMatchTool();
        tool.InitializeRuntime();
        var image = owner.CreateGray8(Size, Size, pixels);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ContourMatchSchema.ImageInput] = image };
        if (withRoi) values[ContourMatchSchema.RoiInput] = FullRegion(owner, image);
        var inputs = new InputValues(tool.Describe().Inputs, values);
        await using var session = tool.CreateSession();
        return await session.ExecuteAsync(new ToolInvocation<ContourMatchSettings>(settings, inputs, new StubContext(owner)));
    }

    private static byte[] TwoMarks(params (int X, int Y)[] positions)
    {
        var pixels = new byte[Size * Size];
        foreach (var (x, y) in positions)
        {
            var patch = Mark(Size, x, y);
            for (var index = 0; index < pixels.Length; index++) pixels[index] = Math.Max(pixels[index], patch[index]);
        }
        return pixels;
    }

    private static RecordValue PositiveSample(double x, double y, double width, double height)
    {
        var shape = new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "rectangle",
            ["center"] = RoiDocumentValue.Point(x, y),
            ["width"] = width, ["height"] = height, ["angle-degrees"] = 0d
        });
        return RoiDocumentValue.Item(Guid.NewGuid(), "正样本", shape);
    }

    [Fact]
    public void Training_rois_positive_sample_builds_a_template()
    {
        using var owner = new FrozenOutputResources();
        var document = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit, new[] { PositiveSample(32, 23, 24, 6) }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));
        var settings = new ContourMatchSettings { TrainingRois = document };
        var payload = CreateTemplate(owner, Mark(Size, 20, 20), settings);
        var template = ContourMatchTemplate.Decode(payload);
        Assert.True(template.PointCount > 0);
    }

    [Fact]
    public void Training_rois_without_positive_sample_fail_fast()
    {
        using var owner = new FrozenOutputResources();
        var document = RoiDocumentValue.Empty;
        var settings = new ContourMatchSettings { TrainingRois = document };
        var image = owner.CreateGray8(Size, Size, Mark(Size, 20, 20));
        // 无正样本且无 ROI 时训练必须明确报错，不能退化成全图模板。
        Assert.ThrowsAny<Exception>(() => ContourMatchTemplateBuilder.Create(image, null, settings));
    }

    [Fact]
    public void Template_payload_round_trips_and_describes_the_contour()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var template = ContourMatchTemplate.Decode(payload);
        Assert.True(template.PointCount > 0);
        Assert.True(template.LevelCount >= 1);
        Assert.True(template.Box.Length1 > 0 && template.Box.Length2 > 0);
        Assert.Equal(payload, ContourMatchTemplate.Decode(payload).Encode());
        Assert.True(ContourMatchTemplate.TryDecode(payload, out _, out var error), error);
        Assert.False(ContourMatchTemplate.TryDecode("not-a-template", out _, out var invalid));
        Assert.Contains("模板", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Single_contour_is_located_with_pose_score_rows_and_display_geometry()
    {
        using var owner = new FrozenOutputResources();
        const int markX = 20, markY = 20, offsetX = 17, offsetY = 11;
        var settings = new ContourMatchSettings { ModelData = CreateTemplate(owner, Mark(Size, markX, markY)) };
        var outcome = await RunAsync(settings, Mark(Size, markX + offsetX, markY + offsetY), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.Count);
        Assert.Equal(1, output.Projection.ModuleStatus);

        // L 形目标外接框 24×18；像素中心语义下框中心 = 左上角 + (11.5, 8.5)。
        var expectedX = markX + offsetX + 11.5;
        var expectedY = markY + offsetY + 8.5;
        Assert.InRange(output.Projection.MatchPoints[0].X, expectedX - 2, expectedX + 2);
        Assert.InRange(output.Projection.MatchPoints[0].Y, expectedY - 2, expectedY + 2);
        Assert.InRange(output.Projection.Boxes[0].Width, 23d, 25d);
        Assert.InRange(output.Projection.Boxes[0].Height, 17d, 19d);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
        Assert.InRange(output.Projection.Angles[0], -0.5f, 0.5f);

        var values = output.ToValues();
        var rows = Assert.IsType<SequenceValue>(values[ContourMatchSchema.Rows]);
        Assert.Equal(1, rows.Count);
        var row = (RecordValue)rows[0]!;
        Assert.Equal(1L, row.Get<long>("element-id"));
        Assert.True(row.Contains("region"));
        Assert.True(row.Contains("outline"));
        Assert.True(row.Get<bool>("judgment-passed"));
        Assert.InRange(row.Get<double>("scale"), 0.5, 2.0);
        Assert.Equal(1L, values[ContourMatchSchema.Count]);
        Assert.NotNull(values[ContourMatchSchema.Scales]);
        Assert.NotNull(values[ContourMatchSchema.OutlinePoints]);
        Assert.DoesNotContain(nameof(ContourMatchResultProjection.Scales), output.Projection.Unavailable);

        Assert.True(output.ResultGeometry.Passed);
        Assert.Contains(output.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
        Assert.NotEmpty(output.DescribeResultRecords());
    }

    [Fact]
    public async Task Empty_search_image_reports_no_match_as_failed_judgement()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings { ModelData = CreateTemplate(owner, Mark(Size, 20, 20)) };
        var outcome = await RunAsync(settings, new byte[Size * Size], owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(0, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Equal(0, output.Projection.ModuleStatus);
    }

    [Fact]
    public async Task Zero_matches_pass_when_the_original_ignore_count_switch_is_on()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 20, 20)),
            OkWhenCountIsZero = true
        };
        var outcome = await RunAsync(settings, new byte[Size * Size], owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(0, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.ModuleStatus);
    }

    [Fact]
    public async Task Missing_roi_and_missing_template_fail_with_explicit_diagnostics()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var noRoi = await RunAsync(new ContourMatchSettings { ModelData = payload }, Mark(Size, 20, 20), owner, withRoi: false);
        Assert.False(noRoi.Success);
        Assert.Contains(noRoi.Diagnostics, item => item.Code == "CONTOURMATCH_ROI_REQUIRED");

        var noTemplate = await RunAsync(new ContourMatchSettings(), Mark(Size, 20, 20), owner);
        Assert.False(noTemplate.Success);
        Assert.Contains(noTemplate.Diagnostics, item => item.Code == "CONTOURMATCH_TEMPLATE_REQUIRED");
    }

    [Fact]
    public void Rectangle_parameter_roi_is_resolved_without_local_document_or_upstream_region()
    {
        using var owner = new FrozenOutputResources();
        var image = owner.CreateGray8(Size, Size, Mark(Size, 20, 20));
        var tool = new ContourMatchTool();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ContourMatchSchema.ImageInput] = image,
            [ContourMatchRoi.ModeId] = ContourMatchRoi.RectangleParameters,
            [ContourMatchRoi.RectangleSource + ".center-x"] = 64d,
            [ContourMatchRoi.RectangleSource + ".center-y"] = 64d,
            [ContourMatchRoi.RectangleSource + ".width"] = 96d,
            [ContourMatchRoi.RectangleSource + ".height"] = 96d,
            [ContourMatchRoi.RectangleSource + ".angle-degrees"] = 0d
        };
        var inputs = new InputValues(tool.Describe().Inputs, values);
        var region = ContourMatchSearchRois.ResolveLocal(inputs, Size, Size);
        Assert.NotNull(region);
        region!.Dispose();
    }

    [Fact]
    public async Task Count_limit_judges_the_number_of_matches()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 16, 16)),
            MaxMatchCount = 2,
            CountLimitEnabled = true,
            CountLimitLow = 1,
            CountLimitHigh = 1
        };
        var outcome = await RunAsync(settings, TwoMarks((10, 10), (70, 70)), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(2, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Contains(output.LimitMessages, message => message.Contains("匹配个数判断", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sort_type_x_orders_rows_and_numbers_them_in_sorted_order()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 16, 16)),
            MaxMatchCount = 2,
            SortType = ContourMatchSortType.X
        };
        var outcome = await RunAsync(settings, TwoMarks((70, 10), (10, 70)), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(2, output.Projection.Count);
        Assert.True(output.Projection.MatchPoints[0].X < output.Projection.MatchPoints[1].X);
        var rows = Assert.IsType<SequenceValue>(output.ToValues()[ContourMatchSchema.Rows]);
        Assert.Equal(1L, ((RecordValue)rows[0]!).Get<long>("element-id"));
        Assert.Equal(2L, ((RecordValue)rows[1]!).Get<long>("element-id"));
    }

    [Fact]
    public void Unsupported_settings_are_rejected_explicitly_instead_of_being_ignored()
    {
        // 极性(0x0303)、等比尺度(0x0307/0308)、尺度判断已按静态源码逻辑对照实现，不再拒绝。
        Assert.Empty(new ContourMatchSettings { Polarity = 0 }.Validate());
        Assert.Empty(new ContourMatchSettings { ScaleStart = 0.8, ScaleEnd = 1.2 }.Validate());
        Assert.Empty(new ContourMatchSettings { ScaleLimitEnabled = true }.Validate());
        Assert.Empty(new ContourMatchSettings { TimeOut = 100 }.Validate()); // 0x030A 超时已实现
        // 参数集已按 HALCON 能力重构：无原生对应的原厂参数（链长/建模类型/横切角度/运行期阈值/噪点开关）
        // 已删除，不再存在"执行期能力拒绝"。
        Assert.Empty(new ContourMatchSettings { MinContrast = 3 }.Validate());
        Assert.Empty(new ContourMatchSettings { Greediness = 0.5, SubPixel = ContourMatchSubPixel.Interpolation }.Validate());
        Assert.Empty(new ContourMatchSettings { Optimization = ContourMatchOptimization.PointReductionHigh }.Validate());
        Assert.NotEmpty(new ContourMatchSettings { Greediness = 1.5 }.Validate());
        Assert.NotEmpty(new ContourMatchSettings { MinContrast = 0 }.Validate());
        var inverted = new ContourMatchSettings
        {
            PyramidMode = ContourMatchPyramidMode.Manual,
            PyramidScaleLevel = 3,
            PyramidScaleRLevel = 5
        }.Validate();
        Assert.Contains(inverted, item => item.Message.Contains("特征尺度", StringComparison.Ordinal));
        Assert.Empty(new ContourMatchSettings().Validate());
    }

    [Fact]
    public async Task Manual_pyramid_and_manual_edge_threshold_build_a_usable_template()
    {
        using var owner = new FrozenOutputResources();
        var templateSettings = new ContourMatchSettings
        {
            PyramidMode = ContourMatchPyramidMode.Manual,
            PyramidScaleLevel = 4,
            PyramidScaleRLevel = 2,
            EdgeThresholdMode = ContourMatchThresholdMode.Manual,
            EdgeThreshold = 20
        };
        var settings = templateSettings with { ModelData = CreateTemplate(owner, Mark(Size, 20, 20), templateSettings) };
        var outcome = await RunAsync(settings, Mark(Size, 33, 41), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(1, output.Projection.Count);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
    }

    [Fact]
    public void Settings_round_trip_through_the_declared_fields()
    {
        var tool = new ContourMatchTool();
        var settings = new ContourMatchSettings
        {
            ModelData = "{\"format\":\"x\"}",
            MaxMatchCount = 4,
            MinScore = 0.7,
            AngleStart = -30,
            AngleEnd = 30,
            SortType = ContourMatchSortType.YX,
            PyramidScaleLevel = 7,
            EdgeThreshold = 64
        };
        var materialized = (ContourMatchSettings)tool.MaterializeSettings(tool.ReadSettings(settings));
        Assert.Equal(settings.MaxMatchCount, materialized.MaxMatchCount);
        Assert.Equal(settings.MinScore, materialized.MinScore);
        Assert.Equal(settings.AngleStart, materialized.AngleStart);
        Assert.Equal(settings.SortType, materialized.SortType);
        Assert.Equal(settings.PyramidScaleLevel, materialized.PyramidScaleLevel);
        Assert.Equal(settings.EdgeThreshold, materialized.EdgeThreshold);
        Assert.Equal(settings.ModelData, materialized.ModelData);
    }

    /// <summary>
    /// 模板训练原图的载荷往返：写入后 Encode/Decode 必须带回原图与尺寸；
    /// 旧模型（无 image 段）解码必须返回 null 而不是报错，保证存量模型可继续使用。
    /// 原厂语义：模板存图开才带原图（decompiled HPFeaturepatMatchDialogUI.cs:1019-1040、:1650-1656）。
    /// </summary>
    [Fact]
    public void Template_payload_round_trips_optional_training_image()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var template = ContourMatchTemplate.Decode(payload);
        Assert.Null(template.Image); // 旧模型/未存图：无原图但不是错误

        var pixels = Enumerable.Range(0, Size * Size).Select(index => (byte)(index % 256)).ToArray();
        var withImage = template.WithImage(pixels, Size, Size);
        var roundTrip = ContourMatchTemplate.Decode(withImage.Encode());
        Assert.NotNull(roundTrip.Image);
        Assert.Equal(Size, roundTrip.ImageWidth);
        Assert.Equal(Size, roundTrip.ImageHeight);
        Assert.Equal(pixels, roundTrip.Image);
        Assert.Null(roundTrip.WithoutImage().Image); // 存图关闭时丢弃
    }

    /// <summary>
    /// 原厂模板同时持久化 otherMask（擦除轮廓点）与 otherMask2（画笔屏蔽区）。
    /// 两层必须独立往返；仅保存合并掩膜会导致再次打开模板后无法继续编辑原工具状态。
    /// </summary>
    [Fact]
    public void Template_payload_round_trips_two_independent_masks()
    {
        using var owner = new FrozenOutputResources();
        var template = ContourMatchTemplate.Decode(CreateTemplate(owner, Mark(Size, 20, 20)));
        var erase = new byte[Size * Size];
        var paint = new byte[Size * Size];
        erase[3] = 1;
        paint[Size + 4] = 1;

        var roundTrip = ContourMatchTemplate.Decode(template.WithMasks(erase, paint, Size, Size).Encode());
        Assert.Equal(erase, roundTrip.Mask);
        Assert.Equal(paint, roundTrip.Mask2);
        Assert.Equal(Size, roundTrip.MaskWidth);
        Assert.Equal(Size, roundTrip.Mask2Width);
        // 旧 API 仍只替换 otherMask，并保留 otherMask2。
        var replaced = roundTrip.WithMask(null, Size, Size);
        Assert.Null(replaced.Mask);
        Assert.Equal(paint, replaced.Mask2);
    }

    /// <summary>
    /// 原厂清空后建模只读取当前会话的 otherMask/otherMask2，不回读旧 ModelData。
    /// 本方必须区分“未修改”与“明确清空”：清空擦除层后不能让旧擦除层重新参与建模，
    /// 同时保留未修改的屏蔽层，避免两层掩膜互相覆盖。
    /// </summary>
    [Fact]
    public void Cleared_template_window_mask_does_not_reuse_stored_erase_layer()
    {
        using var owner = new FrozenOutputResources();
        var template = ContourMatchTemplate.Decode(CreateTemplate(owner, Mark(Size, 20, 20)));
        var erase = new byte[Size * Size];
        var paint = new byte[Size * Size];
        erase[3] = 1;
        paint[Size + 4] = 1;
        var stored = ContourMatchTemplate.Decode(template.WithMasks(erase, paint, Size, Size).Encode());

        var untouched = TemplateMaskResolver.Resolve(
            new TemplateMaskSnapshot(null, false, null, false, 0, 0), stored);
        Assert.Equal(erase, untouched.EraseMask);
        Assert.Equal(paint, untouched.PaintMask);

        // ClearEraseMask() produces this explicit two-layer state: the erase layer is null,
        // while the paint layer remains independently unspecified and therefore comes from storage.
        var clearedErase = TemplateMaskResolver.Resolve(
            new TemplateMaskSnapshot(null, true, null, false, Size, Size), stored);
        Assert.Null(clearedErase.EraseMask);
        Assert.Equal(paint, clearedErase.PaintMask);

        var clearedBoth = TemplateMaskResolver.Resolve(
            new TemplateMaskSnapshot(null, true, null, true, Size, Size), stored);
        Assert.Null(clearedBoth.EraseMask);
        Assert.Null(clearedBoth.PaintMask);
    }

    /// <summary>
    /// 多模板集合载荷：旧单模型载荷解析为 1 项（存量兼容）；多模型集合往返一致。
    /// 原厂多模板是逐模型挂到模块（FeaturepatMatchConfigPanalDataBase.cs:152-180）。
    /// </summary>
    [Fact]
    public void Template_set_decodes_single_payload_and_round_trips_multiple()
    {
        using var owner = new FrozenOutputResources();
        var single = ContourMatchTemplate.Decode(CreateTemplate(owner, Mark(Size, 20, 20)));
        var legacyPayload = single.Encode();

        var legacy = ContourMatchTemplateSet.Decode(legacyPayload);
        Assert.Single(legacy);
        Assert.Equal(ContourMatchTemplateSet.Decode(ContourMatchTemplateSet.Encode(legacy)).Count, 1);

        var two = ContourMatchTemplateSet.Decode(ContourMatchTemplateSet.Encode(new[] { single, single }));
        Assert.Equal(2, two.Count);
        Assert.Equal(single.PointCount, two[0].PointCount);
        Assert.Throws<ContourMatchCapabilityException>(() => ContourMatchTemplateSet.Decode(null));
        Assert.Throws<ContourMatchCapabilityException>(() => ContourMatchTemplateSet.Encode(Array.Empty<ContourMatchTemplate>()));
    }

    /// <summary>
    /// 多模板执行：ModelSet 载荷 + 全部搜索模式必须真正跑通，输出模型索引落在他模板范围内。
    /// 验证多模型匹配与跨模型 NMS 分支不是"只编译不执行"。
    /// </summary>
    [Fact]
    public async Task Multi_template_execution_reports_model_index()
    {
        using var owner = new FrozenOutputResources();
        var baseSettings = new ContourMatchSettings { MinScore = 0.3 };
        var template = ContourMatchTemplate.Decode(CreateTemplate(owner, Mark(Size, 20, 20), baseSettings));
        var set = ContourMatchTemplateSet.Encode(new[]
        {
            new ContourMatchTemplateSet.Entry(template, "模板A", true),
            new ContourMatchTemplateSet.Entry(template, "模板B", false)
        });
        var settings = baseSettings with { ModelData = set, UseMatchAllMode = true };
        var outcome = await RunAsync(settings, Mark(Size, 33, 41), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.True(output.Projection.Count >= 1);
        Assert.All(output.Projection.ModelIndexes, index => Assert.InRange(index, 0L, 1L));
        Assert.All(output.Projection.ModelNames, name => Assert.Contains(name, new[] { "模板A", "模板B" }));
    }

    /// <summary>
    /// 各向异性（尺度范围XY）执行：走 CreateAnisoShapeModel / FindAnisoShapeModel 分支，
    /// 输出必须带 X/Y 尺度且与等比尺度同量级（目标等比时两轴相同）。
    /// </summary>
    [Fact]
    public async Task Anisotropic_scale_mode_executes_and_reports_axes()
    {
        using var owner = new FrozenOutputResources();
        var baseSettings = new ContourMatchSettings
        {
            MinScore = 0.3,
            RangeScaleType = ContourMatchRangeScaleType.XY,
            ScaleXStart = 0.9, ScaleXEnd = 1.1,
            ScaleYStart = 0.9, ScaleYEnd = 1.1
        };
        var payload = CreateTemplate(owner, Mark(Size, 20, 20), baseSettings);
        Assert.True(ContourMatchTemplate.Decode(payload).Anisotropic);
        var outcome = await RunAsync(baseSettings with { ModelData = payload }, Mark(Size, 33, 41), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.True(output.Projection.Count >= 1);
        Assert.Equal(output.Projection.Count, output.Projection.ScaleX.Count);
        Assert.Equal(output.Projection.Count, output.Projection.ScaleY.Count);
        for (var index = 0; index < output.Projection.Count; index++)
            Assert.Equal(output.Projection.ScaleX[index], output.Projection.ScaleY[index], 3);
    }

    /// <summary>
    /// 新建节点（默认设置）必须能读回全部声明字段：ModelData 在创建模板前是空串，
    /// 缺少默认值补齐会让物化设置报 "Missing required port field:..."，表现为点"生成模板"无反应。
    /// </summary>
    [Fact]
    public void Default_settings_materialize_with_empty_template_data()
    {
        var tool = new ContourMatchTool();
        var values = tool.ReadSettings(new ContourMatchSettings());
        Assert.Equal("", values.Get<string>(ContourMatchFields.ModelData));
        var materialized = (ContourMatchSettings)tool.MaterializeSettings(values);
        Assert.Equal("", materialized.ModelData);
        Assert.Empty(materialized.Validate());
    }

    /// <summary>
    /// 多模板元数据往返：每项名称与"加入搜索列表"勾选必须原样保存（原厂 ModelItem.Name / IsSelected）。
    /// </summary>
    [Fact]
    public void Template_set_round_trips_entry_metadata()
    {
        using var owner = new FrozenOutputResources();
        var template = ContourMatchTemplate.Decode(CreateTemplate(owner, Mark(Size, 20, 20)));
        var entries = new[]
        {
            new ContourMatchTemplateSet.Entry(template, "模板A", true),
            new ContourMatchTemplateSet.Entry(template, "模板B", false)
        };
        var decoded = ContourMatchTemplateSet.DecodeEntries(ContourMatchTemplateSet.Encode(entries));
        Assert.Equal(2, decoded.Count);
        Assert.Equal("模板A", decoded[0].Name);
        Assert.True(decoded[0].Selected);
        Assert.Equal("模板B", decoded[1].Name);
        Assert.False(decoded[1].Selected);
    }

    /// <summary>
    /// 新建节点的枚举字段往返：Read（枚举→枚举名文本）与 Materialize（文本→枚举）必须闭合，
    /// 否则新方案节点在保存/重开时会因声明与常量不符被拒（历史问题：JSON constant differs from its declaration）。
    /// </summary>
    [Fact]
    public void Enum_fields_round_trip_through_declared_settings()
    {
        var tool = new ContourMatchTool();
        var settings = new ContourMatchSettings
        {
            RangeScaleType = ContourMatchRangeScaleType.XY,
            Optimization = ContourMatchOptimization.PointReductionMedium,
            SubPixel = ContourMatchSubPixel.Interpolation,
            MinContrast = 12,
            Greediness = 0.6,
            PyramidMode = ContourMatchPyramidMode.Manual,
            EdgeThresholdMode = ContourMatchThresholdMode.Manual
        };
        var values = tool.ReadSettings(settings);
        Assert.Equal(nameof(ContourMatchRangeScaleType.XY), values.Get<string>(ContourMatchFields.RangeScaleType));
        Assert.Equal(nameof(ContourMatchOptimization.PointReductionMedium), values.Get<string>(ContourMatchFields.Optimization));
        Assert.Equal(nameof(ContourMatchSubPixel.Interpolation), values.Get<string>(ContourMatchFields.SubPixel));
        var materialized = (ContourMatchSettings)tool.MaterializeSettings(values);
        Assert.Equal(settings.RangeScaleType, materialized.RangeScaleType);
        Assert.Equal(settings.Optimization, materialized.Optimization);
        Assert.Equal(settings.SubPixel, materialized.SubPixel);
        Assert.Equal(settings.MinContrast, materialized.MinContrast);
        Assert.Equal(settings.Greediness, materialized.Greediness);
        Assert.Equal(settings.PyramidMode, materialized.PyramidMode);
        Assert.Equal(settings.EdgeThresholdMode, materialized.EdgeThresholdMode);
    }
}
