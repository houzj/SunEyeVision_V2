using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.ColorExtract.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>色彩提取契约面：六必需契约 + 运行时预热、端口声明、原厂默认值与逐字段校验。</summary>
public sealed class ColorExtractDeclarationTests
{
    [Fact]
    public void Runtime_declares_catalog_identity_and_the_color_processing_category()
    {
        var tool = new ColorExtractTool();
        Assert.Equal("ColorExtract", tool.ToolId);
        Assert.Equal("颜色抽取", tool.Catalog.DisplayName);
        // 工具箱目录中 IMVSColorExtractModu 位于「颜色处理」；运行时分类必须与之一致才能准入。
        Assert.Equal("颜色处理", tool.Catalog.Category);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        var component = Assert.Single(typeof(ColorExtractTool).Assembly.GetCustomAttributes<ToolComponentAttribute>());
        Assert.Equal("SunEyeVision.Tool.ColorExtract.Runtime", component.Metadata.ComponentId);
        Assert.Equal(tool.ToolId, component.Metadata.ToolId);
        Assert.Equal(ToolComponentRole.Runtime, component.Metadata.Role);
        Assert.Equal(FoundationContract.Major, component.Metadata.ContractMajor);
        Assert.Equal(FoundationContract.Minor, component.Metadata.ContractMinor);
        Assert.Null(component.Metadata.CompanionToolId);
        Assert.Equal(typeof(ColorExtractTool).FullName, component.EntryType);
        Assert.IsAssignableFrom<IToolRuntimeInitialization>(tool);
    }

    [Fact]
    public void Describe_declares_one_image_reference_input_and_all_original_output_ports()
    {
        var schema = ColorExtractTool.Schema;
        var input = Assert.Single(schema.Inputs);
        Assert.Equal(ColorExtractTool.ImageInput, input.Id);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), input.Type);
        Assert.True(input.Required);
        Assert.Equal(FieldEditModes.Reference, input.EditModes);
        Assert.Equal(ColorExtractTool.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(ColorExtractOutput.Ports, schema.Outputs);
        // 原件输入还有 InputROI/InputROIAnnulus/位置修正；本批未实现，不得声明成无法产出的空端口。
        Assert.Single(schema.Inputs);
        var tool = new ColorExtractTool();
        Assert.Equal(ColorExtractOutput.Ports, tool.ResultSchema.Outputs);
    }

    [Fact]
    public void Output_ports_keep_declared_types_nullability_and_result_panel_visibility()
    {
        var expected = new Dictionary<string, TypeRef>
        {
            [ColorExtractOutput.Image] = ColorExtractOutput.ImageType,
            [ColorExtractOutput.TotalArea] = DataType.Number,
            [ColorExtractOutput.Roi] = ColorExtractOutput.RoiType,
            [ColorExtractOutput.ModuleRunTime] = DataType.Number,
            [ColorExtractOutput.ModuleStatus] = DataType.Integer
        };
        Assert.Equal(expected.Keys.OrderBy(x => x), ColorExtractOutput.Ports.Select(port => port.Id).OrderBy(x => x));
        foreach (var port in ColorExtractOutput.Ports)
        {
            Assert.Equal(expected[port.Id], port.Type);
            Assert.Equal(port.Id == ColorExtractOutput.Roi, port.Nullable);
        }
        Assert.Equal(NumericStorage.Float32, ColorExtractOutput.Ports.Single(port => port.Id == ColorExtractOutput.TotalArea).Storage);
        Assert.Equal(NumericStorage.Int32, ColorExtractOutput.Ports.Single(port => port.Id == ColorExtractOutput.ModuleStatus).Storage);
        var schema = ColorExtractTool.Schema;
        Assert.False(schema.FindOutputDisplay(ColorExtractOutput.ModuleStatus)!.DisplayInResultPanel);
        foreach (var portId in expected.Keys) Assert.NotNull(schema.FindOutputDisplay(portId));
        var roi = schema.FindOutputDisplay(ColorExtractOutput.Roi)!;
        foreach (var member in new[] { "center", "width", "height", "angle-degrees" })
            Assert.NotNull(roi.Children.SingleOrDefault(child => child.FieldId == member));
    }

    [Fact]
    public void Settings_declare_the_four_original_algorithm_parameters_with_original_defaults()
    {
        // 原厂证据：IMVSColorExtractModuAlgorithm.xml 只有四个算法参数
        // AreaLimitEnable=False、AreaLimitLow=1、AreaLimitHigh=9999999、InputColorRange=255,0,255,0,255,0,1,0,1。
        var settings = new ColorExtractSettings();
        Assert.Equal("255,0,255,0,255,0,1,0,1", settings.ColorRange);
        Assert.False(settings.AreaLimitEnabled);
        Assert.Equal(1L, settings.AreaLimitLow);
        Assert.Equal(9999999L, settings.AreaLimitHigh);
        Assert.Equal(new[]
        {
            ColorExtractFields.ColorRange, ColorExtractFields.AreaLimitEnabled,
            ColorExtractFields.AreaLimitLow, ColorExtractFields.AreaLimitHigh
        }, ColorExtractFields.Schema.Inputs.Select(port => port.Id).ToArray());
        Assert.Empty(settings.Validate());
    }

    [Fact]
    public void Presentation_keeps_the_original_page_grouping_choice_and_range_layout()
    {
        var fields = ColorExtractFields.Schema.Presentation!.Fields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        Assert.All(fields.Values, field => Assert.Equal(ColorExtractFields.Page, field.Layout!.Page));
        Assert.Equal(ColorExtractFields.RangeSection, fields[ColorExtractFields.ColorRange].Group);
        Assert.Equal(ColorExtractFields.ParamsSection, fields[ColorExtractFields.AreaLimitLow].Group);
        var low = fields[ColorExtractFields.AreaLimitLow];
        var high = fields[ColorExtractFields.AreaLimitHigh];
        Assert.Equal(ColorExtractFields.AreaLimitHigh, low.Layout!.PairWith);
        Assert.Equal("面积上限", low.Layout.PairDisplayName);
        Assert.Equal(0d, low.Layout.Minimum!.Value);
        Assert.Equal(9999999d, low.Layout.Maximum!.Value);
        Assert.Equal(0, low.Layout.DecimalPlaces!.Value);
        foreach (var field in new[] { low, high })
        {
            Assert.Equal(ColorExtractFields.AreaLimitEnabled, field.Layout!.VisibleWhen!.FieldId);
            Assert.Equal(true, field.Layout.VisibleWhen.Value);
        }
    }

    [Fact]
    public void Color_range_text_round_trips_the_original_serialization()
    {
        // 原厂 RangeInfo.ToString：C1High,C1Low,C2High,C2Low,C3High,C3Low,色彩空间(1/2/3),反色(0/1),启用(0/1)；
        // 多项之间用 '#' 连接（ColorExtractList.Value）。
        var text = "255,0,0,0,0,0,1,0,1#120,10,255,20,200,30,2,1,0";
        var ranges = ColorExtractRanges.Parse(text);
        Assert.Equal(2, ranges.Count);
        Assert.Equal(text, ColorExtractRanges.Format(ranges));
        Assert.Equal(new ColorExtractRange(255, 0, 0, 0, 0, 0, ColorExtractColorSpace.Rgb, false, true), ranges[0]);
        Assert.Equal(new ColorExtractRange(120, 10, 255, 20, 200, 30, ColorExtractColorSpace.Hsv, true, false), ranges[1]);
        // 原厂 Serialize 对空串保留构造时的一个默认区间。
        Assert.Equal(new[] { ColorExtractRanges.DefaultRange }, ColorExtractRanges.Parse("  ").ToArray());
    }

    [Fact]
    public void Invalid_range_text_is_rejected_instead_of_skipped()
    {
        Assert.Throws<ArgumentException>(() => ColorExtractRanges.Parse("255,0,255"));
        Assert.Throws<ArgumentException>(() => ColorExtractRanges.Parse("255,0,255,0,255,0,4,0,1"));
        Assert.Throws<ArgumentException>(() => ColorExtractRanges.Parse("255,0,255,0,255,0,1,2,1"));
        Assert.Throws<ArgumentException>(() => ColorExtractRanges.Parse("300,0,255,0,255,0,1,0,1"));
        Assert.Throws<ArgumentException>(() => ColorExtractRanges.Parse("0,255,255,0,255,0,1,0,1"));
        Assert.Throws<ArgumentException>(() => ColorExtractRanges.Parse(string.Join('#', Enumerable.Repeat("255,0,255,0,255,0,1,0,1", 17))));
    }

    [Fact]
    public void Settings_validation_reports_the_original_field_identities()
    {
        var invalidText = new ColorExtractSettings { ColorRange = "abc" }.Validate();
        var textError = Assert.Single(invalidText);
        Assert.Equal("COLOR_EXTRACT_RANGE_INVALID", textError.Code);
        Assert.Equal(ColorExtractFields.ColorRange, textError.FieldId);

        var wrongCount = new ColorExtractSettings
        {
            ColorRange = string.Join('#', Enumerable.Repeat("255,0,255,0,255,0,1,0,1", 17))
        }.Validate();
        var countError = Assert.Single(wrongCount);
        Assert.Equal("COLOR_EXTRACT_RANGE_INVALID", countError.Code);
        Assert.Contains("16", countError.Message);

        var inverted = new ColorExtractSettings { AreaLimitEnabled = true, AreaLimitLow = 500, AreaLimitHigh = 100 }.Validate();
        var areaError = Assert.Single(inverted);
        Assert.Equal("COLOR_EXTRACT_AREA_LIMIT", areaError.Code);
        Assert.Equal(ColorExtractFields.AreaLimitHigh, areaError.FieldId);
        // 未启用面积筛选时不校验停用值，与原厂只在启用时显示 RangeAreaLimit 一致。
        Assert.Empty(new ColorExtractSettings { AreaLimitEnabled = false, AreaLimitLow = 500, AreaLimitHigh = 100 }.Validate());
    }

    [Fact]
    public void Settings_roundtrip_through_declared_fields()
    {
        var tool = new ColorExtractTool();
        var values = tool.ReadSettings(new ColorExtractSettings
        {
            ColorRange = "255,0,0,0,0,0,1,0,1", AreaLimitEnabled = true, AreaLimitLow = 3, AreaLimitHigh = 40
        });
        var materialized = Assert.IsType<ColorExtractSettings>(tool.MaterializeSettings(values));
        Assert.Equal("255,0,0,0,0,0,1,0,1", materialized.ColorRange);
        Assert.True(materialized.AreaLimitEnabled);
        Assert.Equal(3L, materialized.AreaLimitLow);
        Assert.Equal(40L, materialized.AreaLimitHigh);
        var applied = ColorExtractFields.Apply(new ColorExtractSettings(),
            new Dictionary<string, object?> { [ColorExtractFields.ColorRange] = "10,0,20,0,30,0,3,0,1" });
        Assert.Equal("10,0,20,0,30,0,3,0,1", applied.ColorRange);
        Assert.Throws<ArgumentException>(() => tool.MaterializeSettings(
            new InputValues(ColorExtractFields.Schema.Inputs, new Dictionary<string, object?>
            {
                [ColorExtractFields.ColorRange] = "255",
                [ColorExtractFields.AreaLimitEnabled] = false,
                [ColorExtractFields.AreaLimitLow] = 1L,
                [ColorExtractFields.AreaLimitHigh] = 9999999L
            })));
        Assert.Throws<ArgumentException>(() => ColorExtractFields.Apply(new ColorExtractSettings(),
            new Dictionary<string, object?> { ["unknown.field"] = "1" }));
    }

    [Fact]
    public void Host_edit_session_admits_the_declared_defaults_and_accepts_valid_edits()
    {
        // 与 RuntimeComponentAdmission 相同路径：默认值必须能构造出完整编辑快照，否则工具准入会被拒绝。
        var tool = new ColorExtractTool();
        var schema = ResultDisplayFields.EditorSchema(tool);
        var defaults = ((IToolSettingsFields)tool).ReadSettings(new ColorExtractSettings()).CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
        var session = new ToolSettingsEditSession(tool, new FieldSettings(schema.Inputs, defaults));
        var captured = session.CaptureValidatedFields();
        Assert.Equal(4, captured.Declarations.Count);
        session.ApplyFields(new Dictionary<string, ParameterSetting>
        {
            [ColorExtractFields.ColorRange] = new ConstantSetting("255,0,255,0,0,0,1,0,1"),
            [ColorExtractFields.AreaLimitEnabled] = new ConstantSetting(true),
            [ColorExtractFields.AreaLimitLow] = new ConstantSetting(2L),
            [ColorExtractFields.AreaLimitHigh] = new ConstantSetting(20L)
        }, _ => { });
        var edited = session.CaptureValidatedFields();
        Assert.Equal("255,0,255,0,0,0,1,0,1", Assert.IsType<ConstantSetting>(edited.Read(ColorExtractFields.ColorRange)).Value);
        Assert.Equal(true, Assert.IsType<ConstantSetting>(edited.Read(ColorExtractFields.AreaLimitEnabled)).Value);
        Assert.Equal(2L, Assert.IsType<ConstantSetting>(edited.Read(ColorExtractFields.AreaLimitLow)).Value);
        Assert.Equal(20L, Assert.IsType<ConstantSetting>(edited.Read(ColorExtractFields.AreaLimitHigh)).Value);
        // 非法区间文本与颠倒的面积上下限在编辑期给出明确诊断（IToolKnownSettingsValidation）。
        FieldSettings Known(object range, bool enabled, long low, long high)
            => new(ColorExtractFields.Schema.Inputs, new Dictionary<string, ParameterSetting>
            {
                [ColorExtractFields.ColorRange] = new ConstantSetting(range),
                [ColorExtractFields.AreaLimitEnabled] = new ConstantSetting(enabled),
                [ColorExtractFields.AreaLimitLow] = new ConstantSetting(low),
                [ColorExtractFields.AreaLimitHigh] = new ConstantSetting(high)
            });
        var invalidText = Assert.Single(tool.ValidateKnownSettings(Known("255,0", false, 1L, 9999999L)));
        Assert.Equal("COLOR_EXTRACT_RANGE_INVALID", invalidText.Code);
        Assert.Equal(ColorExtractFields.ColorRange, invalidText.FieldId);
        var inverted = Assert.Single(tool.ValidateKnownSettings(Known("255,0,0,0,0,0,1,0,1", true, 500L, 100L)));
        Assert.Equal("COLOR_EXTRACT_AREA_LIMIT", inverted.Code);
        Assert.Equal(ColorExtractFields.AreaLimitHigh, inverted.FieldId);
        Assert.Empty(tool.ValidateKnownSettings(Known("255,0,0,0,0,0,1,0,1", true, 1L, 9999999L)));
    }

    [Fact]
    public void Result_projection_and_display_requirements_fully_reference_declared_ports()
    {
        var tool = new ColorExtractTool();
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        Assert.Equal(ResultProjectionKind.PropertyList, tool.ResultProjection.Kind);
        Assert.Equal(ResultCardinality.One, tool.ResultProjection.Cardinality);
        Assert.All(tool.ResultProjection.Columns, column => Assert.NotNull(column.Value));
        foreach (var column in tool.ResultProjection.Columns)
        {
            var portId = column.Value!.PortId;
            var node = tool.ResultSchema.FindOutputDisplay(portId);
            Assert.NotNull(node);
            if (portId == ColorExtractOutput.Image)
                Assert.All(new[] { "data", "width", "height", "pixel-format" },
                    member => Assert.NotNull(node!.Children.SingleOrDefault(child => child.FieldId == member)));
            if (portId == ColorExtractOutput.Roi)
                Assert.All(new[] { "center", "width", "height", "angle-degrees" },
                    member => Assert.NotNull(node!.Children.SingleOrDefault(child => child.FieldId == member)));
        }
    }
}
