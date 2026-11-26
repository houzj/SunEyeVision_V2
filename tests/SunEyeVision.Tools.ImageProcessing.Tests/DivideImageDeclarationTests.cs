using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.DivideImage.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>划片拆分契约面：六必需契约、端口声明、原厂默认值、分片几何与逐字段校验。</summary>
public sealed class DivideImageDeclarationTests
{
    [Fact]
    public void Runtime_declares_catalog_identity_and_the_split_combine_category()
    {
        var tool = new DivideImageTool();
        Assert.Equal("DivideImage", tool.ToolId);
        Assert.Equal("划片拆分", tool.Catalog.DisplayName);
        // 工具箱目录中 IMVSDivideImageModu 位于「拆分组合」；运行时分类必须与之一致才能准入。
        Assert.Equal("拆分组合", tool.Catalog.Category);
        Assert.Equal("IMAGE", tool.Catalog.NodeDisplayType);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        // 单项目程序集同时承载运行时和编辑器入口，按角色选取运行时声明。
        var component = Assert.Single(
            typeof(DivideImageTool).Assembly
                .GetCustomAttributes<ToolComponentAttribute>()
                .Where(attribute => attribute.Metadata.Role == ToolComponentRole.Runtime));
        Assert.Equal("SunEyeVision.Tool.DivideImage.Runtime", component.Metadata.ComponentId);
        Assert.Equal(tool.ToolId, component.Metadata.ToolId);
        Assert.Equal(ToolComponentRole.Runtime, component.Metadata.Role);
        Assert.Equal(FoundationContract.Major, component.Metadata.ContractMajor);
        Assert.Equal(FoundationContract.Minor, component.Metadata.ContractMinor);
        Assert.Null(component.Metadata.CompanionToolId);
        Assert.Equal(typeof(DivideImageTool).FullName, component.EntryType);
    }

    [Fact]
    public void Describe_declares_image_and_optional_roi_reference_inputs_and_all_original_output_ports()
    {
        var schema = DivideImageTool.Schema;
        var input = Assert.Single(schema.Inputs, port => port.Id == DivideImageTool.ImageInput);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), input.Type);
        Assert.True(input.Required);
        Assert.Equal(FieldEditModes.Reference, input.EditModes);
        var roi = Assert.Single(schema.Inputs, port => port.Id == DivideImageTool.RoiInput);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Region), roi.Type);
        Assert.False(roi.Required);
        Assert.True(roi.Nullable);
        Assert.Equal(FieldEditModes.Reference, roi.EditModes);
        Assert.Equal(DivideImageTool.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(DivideImageOutput.Ports, schema.Outputs);
        var tool = new DivideImageTool();
        Assert.Equal(DivideImageOutput.Ports, tool.ResultSchema.Outputs);
    }

    [Fact]
    public void Output_ports_keep_declared_types_and_result_panel_visibility()
    {
        var expected = new Dictionary<string, TypeRef>
        {
            [DivideImageOutput.Roi] = DivideImageOutput.RectType,
            [DivideImageOutput.Rects] = DivideImageOutput.RectsType,
            [DivideImageOutput.Count] = DataType.Integer,
            [DivideImageOutput.ModuleRunTime] = DataType.Number,
            [DivideImageOutput.ModuleStatus] = DataType.Integer
        };
        Assert.Equal(expected.Keys.OrderBy(x => x, StringComparer.Ordinal),
            DivideImageOutput.Ports.Select(port => port.Id).OrderBy(x => x, StringComparer.Ordinal));
        foreach (var port in DivideImageOutput.Ports)
        {
            Assert.Equal(expected[port.Id], port.Type);
            Assert.False(port.Nullable);
        }
        Assert.Equal(NumericStorage.Int32, DivideImageOutput.Ports.Single(port => port.Id == DivideImageOutput.Count).Storage);
        Assert.Equal(NumericStorage.Int32, DivideImageOutput.Ports.Single(port => port.Id == DivideImageOutput.ModuleStatus).Storage);
        // 原件 DivideRect 是 ROIBOX 数组：形状必须是记录序列，元素字段沿用 center/width/height/angle-degrees。
        Assert.Equal(ValueShape.Sequence, DivideImageOutput.RectsType.Shape);
        Assert.Equal(DivideImageOutput.RectType, DivideImageOutput.RectsType.Element);
        Assert.Null(DivideImageOutput.RectsType.Count.Maximum);
        var schema = DivideImageTool.Schema;
        Assert.False(schema.FindOutputDisplay(DivideImageOutput.ModuleStatus)!.DisplayInResultPanel);
        foreach (var portId in expected.Keys) Assert.NotNull(schema.FindOutputDisplay(portId));
        foreach (var portId in new[] { DivideImageOutput.Roi, DivideImageOutput.Rects })
        {
            var node = schema.FindOutputDisplay(portId)!;
            foreach (var member in new[] { "center", "width", "height", "angle-degrees" })
                Assert.NotNull(node.Children.SingleOrDefault(child => child.FieldId == member));
        }
    }

    [Fact]
    public void Settings_declare_the_four_original_algorithm_parameters_with_original_defaults()
    {
        // 原厂算法 XML 只有四个划片参数。位置修正开关属于基本参数页 AssistGroup，
        // 因此在本方设置声明中与四个算法参数并列，但不改变原厂算法参数集合。
        var settings = new DivideImageSettings();
        Assert.Equal(1L, settings.Columns);
        Assert.Equal(1L, settings.Rows);
        Assert.Equal(0L, settings.OverlapX);
        Assert.Equal(0L, settings.OverlapY);
        Assert.Equal(new[]
        {
            DivideImageFields.PositionCorrectionEnabled,
            DivideImageFields.Columns, DivideImageFields.Rows, DivideImageFields.OverlapX, DivideImageFields.OverlapY
        }, new DivideImageTool().SettingsSchema.Inputs.Select(port => port.Id).ToArray());
        Assert.All(new DivideImageTool().SettingsSchema.Inputs,
            port => Assert.Equal(FieldDefaultPresence.UseDeclaredDefault, port.DefaultPresence));
        Assert.Empty(settings.Validate());
    }

    [Fact]
    public void Presentation_keeps_the_original_run_page_and_field_layout()
    {
        var fields = new DivideImageTool().SettingsSchema.Presentation!.Fields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        Assert.Equal(new[]
        {
            DivideImageFields.PositionCorrectionEnabled,
            DivideImageFields.Columns, DivideImageFields.Rows, DivideImageFields.OverlapX, DivideImageFields.OverlapY
        }, new DivideImageTool().SettingsSchema.Presentation.Fields.Select(field => field.FieldId).ToArray());
        Assert.Equal("基本参数", fields[DivideImageFields.PositionCorrectionEnabled].Layout!.Page);
        Assert.Equal("ROI区域", fields[DivideImageFields.PositionCorrectionEnabled].Group);
        Assert.False(fields[DivideImageFields.PositionCorrectionEnabled].Layout!.AllowReference);
        Assert.All(fields.Values.Where(field => field.FieldId != DivideImageFields.PositionCorrectionEnabled), field =>
        {
            Assert.Equal(DivideImageFields.Page, field.Layout!.Page);
            Assert.Equal(DivideImageFields.Section, field.Group);
            Assert.False(field.Layout.AllowReference);
            Assert.Equal(0, field.Layout.DecimalPlaces!.Value);
        });
        Assert.Equal(1d, fields[DivideImageFields.Columns].Layout!.Minimum!.Value);
        Assert.Equal((double)DivideImageFields.MaximumDivisionsPerAxis, fields[DivideImageFields.Columns].Layout!.Maximum!.Value);
        Assert.Equal(0d, fields[DivideImageFields.OverlapX].Layout!.Minimum!.Value);
        Assert.Equal(100d, fields[DivideImageFields.OverlapX].Layout!.Maximum!.Value);
    }

    [Fact]
    public void Input_presentation_keeps_the_original_basic_page_order_and_groups()
    {
        var presentation = DivideImageTool.Schema.Presentation!;
        // InputROI 是公共 ROI 组合器的外部输入端口，由基本参数页组装，
        // 不再作为第二个裸字段行重复呈现。
        Assert.Equal(new[] { DivideImageTool.ImageInput },
            presentation.Fields.Select(field => field.FieldId).ToArray());
        Assert.Equal("基本参数", presentation.Fields[0].Layout!.Page);
        Assert.Equal("图像输入", presentation.Fields[0].Layout.Section);
        Assert.Equal(FieldEditorKind.Image, presentation.Fields[0].Editor);
    }

    [Fact]
    public void Single_axis_split_covers_the_extent_without_gaps()
    {
        Assert.Equal(new[] { (0d, 100d) }, DivideImageTiling.Split(100, 1, 0, "X"));
        Assert.Equal(new[] { (0d, 50d), (50d, 50d) }, DivideImageTiling.Split(100, 2, 0, "X"));
        // 原生输出为浮点几何，不在工具层将 100/3 提前取整。
        var three = DivideImageTiling.Split(100, 3, 0, "X");
        Assert.All(three, span => Assert.Equal(100d / 3d, span.Size, 5));
        Assert.Equal(0d, three[0].Start);
        Assert.Equal(200d / 3d, three[2].Start, 5);
        AssertCovers(three, 100);
    }

    [Fact]
    public void Overlap_rate_enlarges_tiles_and_keeps_both_ends_anchored()
    {
        // 原生 ProcessAlg：2 片、50% 重叠时尺寸 75，步进 25，中心分别为 37.5 和 62.5。
        var half = DivideImageTiling.Split(100, 2, 50, "X");
        Assert.All(half, span => Assert.Equal(75d, span.Size, 5));
        Assert.Equal(0d, half[0].Start);
        Assert.Equal(25d, half[1].Start, 5);
        AssertCovers(half, 100);
        // 重叠率提高，分片边长单调不减。
        var sizes = new[] { 0, 20, 50, 80, 99 }
            .Select(percent => DivideImageTiling.Split(100, 4, percent, "X")[0].Size).ToArray();
        Assert.Equal(sizes.OrderBy(size => size), sizes);
        Assert.All(sizes, size => Assert.InRange(size, 1, 100));
    }

    [Fact]
    public void Split_rejects_values_outside_the_declared_parameter_domain()
    {
        Assert.Throws<ArgumentException>(() => DivideImageTiling.Split(100, 0, 0, "X"));
        Assert.Throws<ArgumentException>(() => DivideImageTiling.Split(0, 2, 0, "X"));
        Assert.Throws<ArgumentException>(() => DivideImageTiling.Split(100, 2, -1, "X"));
        Assert.Throws<ArgumentException>(() => DivideImageTiling.Split(100, 2, 101, "X"));
    }

    [Fact]
    public void Tiling_enumerates_tiles_row_major()
    {
        var tiles = DivideImageTiling.Compute(8, 6, 2, 3, 0, 0);
        Assert.Equal(6, tiles.Count);
        Assert.Equal(Enumerable.Range(0, 6), tiles.Select(tile => tile.Index));
        Assert.Equal(new[] { (0d, 0d), (4d, 0d), (0d, 2d), (4d, 2d), (0d, 4d), (4d, 4d) },
            tiles.Select(tile => (tile.Column, tile.Row)).ToArray());
        Assert.All(tiles, tile =>
        {
            Assert.Equal(4, tile.Width);
            Assert.Equal(2, tile.Height);
        });
    }

    [Fact]
    public void Settings_validation_reports_the_original_field_identities()
    {
        var columns = Assert.Single(new DivideImageSettings { Columns = 0 }.Validate());
        Assert.Equal("DIVIDE_IMAGE_COLUMNS", columns.Code);
        Assert.Equal(DivideImageFields.Columns, columns.FieldId);

        var rows = Assert.Single(new DivideImageSettings { Rows = 0 }.Validate());
        Assert.Equal("DIVIDE_IMAGE_ROWS", rows.Code);
        Assert.Equal(DivideImageFields.Rows, rows.FieldId);

        var overlapX = Assert.Single(new DivideImageSettings { OverlapX = 101 }.Validate());
        Assert.Equal("DIVIDE_IMAGE_OVERLAP_X", overlapX.Code);
        Assert.Equal(DivideImageFields.OverlapX, overlapX.FieldId);

        var overlapY = Assert.Single(new DivideImageSettings { OverlapY = -1 }.Validate());
        Assert.Equal("DIVIDE_IMAGE_OVERLAP_Y", overlapY.Code);
        Assert.Equal(DivideImageFields.OverlapY, overlapY.FieldId);
    }

    [Fact]
    public void Settings_roundtrip_through_declared_fields()
    {
        var tool = new DivideImageTool();
        var values = tool.ReadSettings(new DivideImageSettings { Columns = 3, Rows = 4, OverlapX = 10, OverlapY = 25 });
        var materialized = Assert.IsType<DivideImageSettings>(tool.MaterializeSettings(values));
        Assert.Equal(3L, materialized.Columns);
        Assert.Equal(4L, materialized.Rows);
        Assert.Equal(10L, materialized.OverlapX);
        Assert.Equal(25L, materialized.OverlapY);
        var merged = tool.ReadSettings(new DivideImageSettings()).CopyValues()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        merged[DivideImageFields.Rows] = 2L;
        merged[DivideImageFields.OverlapY] = 5L;
        var applied = Assert.IsType<DivideImageSettings>(
            tool.MaterializeSettings(new InputValues(tool.SettingsSchema.Inputs, merged)));
        Assert.Equal(1L, applied.Columns);
        Assert.Equal(2L, applied.Rows);
        Assert.Equal(5L, applied.OverlapY);
        // 鏈０鏄庡瓧娈典笉鍦ㄥ０鏄庣鍙ｅ唴锛氭瀯閫?InputValues 鏃跺嵆琚嫆缁濓紝涓嶉渶瑕佸伐鍏疯嚜宸卞厹搴曘€?
        Assert.Throws<ArgumentException>(() => new InputValues(tool.SettingsSchema.Inputs,
            new Dictionary<string, object?> { ["unknown.field"] = 1L }));
        Assert.Throws<ArgumentException>(() => tool.MaterializeSettings(
            new InputValues(new DivideImageTool().SettingsSchema.Inputs, new Dictionary<string, object?>
            {
                [DivideImageFields.Columns] = 0L,
                [DivideImageFields.Rows] = 1L,
                [DivideImageFields.OverlapX] = 0L,
                [DivideImageFields.OverlapY] = 0L
            })));
    }

    [Fact]
    public void Host_edit_session_admits_the_declared_defaults_and_accepts_valid_edits()
    {
        // 与 RuntimeComponentAdmission 相同路径：默认值必须能构造出完整编辑快照，否则工具准入会被拒绝。
        var tool = new DivideImageTool();
        var schema = ResultDisplayFields.EditorSchema(tool);
        var defaults = ((IToolSettingsFields)tool).ReadSettings(new DivideImageSettings()).CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
        var session = new ToolSettingsEditSession(tool, new FieldSettings(schema.Inputs, defaults));
        var captured = session.CaptureValidatedFields();
        Assert.Equal(5, captured.Declarations.Count);
        session.ApplyFields(new Dictionary<string, ParameterSetting>
        {
            [DivideImageFields.Columns] = new ConstantSetting(4L),
            [DivideImageFields.Rows] = new ConstantSetting(2L),
            [DivideImageFields.OverlapX] = new ConstantSetting(10L),
            [DivideImageFields.OverlapY] = new ConstantSetting(0L)
        }, _ => { });
        var edited = session.CaptureValidatedFields();
        Assert.Equal(4L, Assert.IsType<ConstantSetting>(edited.Read(DivideImageFields.Columns)).Value);
        Assert.Equal(2L, Assert.IsType<ConstantSetting>(edited.Read(DivideImageFields.Rows)).Value);
        Assert.Equal(10L, Assert.IsType<ConstantSetting>(edited.Read(DivideImageFields.OverlapX)).Value);
        Assert.Equal(0L, Assert.IsType<ConstantSetting>(edited.Read(DivideImageFields.OverlapY)).Value);

        FieldSettings Known(long columns, long rows, long overlapX, long overlapY)
            => new(new DivideImageTool().SettingsSchema.Inputs, new Dictionary<string, ParameterSetting>
            {
                [DivideImageFields.Columns] = new ConstantSetting(columns),
                [DivideImageFields.Rows] = new ConstantSetting(rows),
                [DivideImageFields.OverlapX] = new ConstantSetting(overlapX),
                [DivideImageFields.OverlapY] = new ConstantSetting(overlapY)
            });
        var invalid = Assert.Single(tool.ValidateKnownSettings(Known(0, 1, 0, 0)));
        Assert.Equal("DIVIDE_IMAGE_COLUMNS", invalid.Code);
        Assert.Equal(DivideImageFields.Columns, invalid.FieldId);
        Assert.Single(tool.ValidateKnownSettings(Known(1, 1, 101, 0)));
        // 100% 是原厂声明上界，必须被接受。
        Assert.Empty(tool.ValidateKnownSettings(Known(3, 3, 100, 100)));
        Assert.Empty(tool.ValidateKnownSettings(Known(3, 3, 99, 99)));
        // 原厂 NumX/NumY 上界 50，51 必须被拒绝。
        Assert.Single(tool.ValidateKnownSettings(Known(51, 1, 0, 0)));
    }

    [Fact]
    public void Result_projection_and_display_requirements_fully_reference_declared_ports()
    {
        var tool = new DivideImageTool();
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        Assert.Equal(ResultProjectionKind.PropertyList, tool.ResultProjection.Kind);
        Assert.Equal(ResultCardinality.One, tool.ResultProjection.Cardinality);
        Assert.All(tool.ResultProjection.Columns, column => Assert.NotNull(column.Value));
        foreach (var column in tool.ResultProjection.Columns)
            Assert.NotNull(tool.ResultSchema.FindOutputDisplay(column.Value!.PortId));
        Assert.Equal(new[] { DivideImageOutput.Roi, DivideImageOutput.Rects, DivideImageOutput.Count, DivideImageOutput.ModuleRunTime },
            tool.ResultProjection.Columns.Select(column => column.Value!.PortId).ToArray());
    }

    private static void AssertCovers(IReadOnlyList<(double Start, double Size)> spans, int extent)
    {
        Assert.Equal(0d, spans[0].Start, 5);
        Assert.Equal(extent, spans[^1].Start + spans[^1].Size, 5);
        for (var index = 1; index < spans.Count; index++)
            Assert.True(spans[index].Start <= spans[index - 1].Start + spans[index - 1].Size,
                $"第 {index} 片与前一片之间出现空隙。");
        Assert.All(spans, span =>
        {
            Assert.True(span.Size > 0);
            Assert.InRange(span.Start, 0d, extent);
            Assert.True(span.Start + span.Size <= extent + 0.00001d);
        });
    }
}
