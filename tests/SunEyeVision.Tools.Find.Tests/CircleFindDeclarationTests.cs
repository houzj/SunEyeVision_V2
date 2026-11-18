using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.CircleFind.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Find.Tests;

/// <summary>
/// 圆查找契约面：六必需契约 + 呈现契约 + 运行时预热；字段默认值逐项锚定原厂 parameters.json，
/// 范围/枚举锚定托管 CircleFindParam.cs。算法数值待补证，本测试不验证数值输出。
/// </summary>
public sealed class CircleFindDeclarationTests
{
    [Fact]
    public void Runtime_declares_stable_identity_catalog_and_required_contracts()
    {
        var tool = new CircleFindTool();
        Assert.Equal("CircleFind", tool.ToolId);
        Assert.Equal("圆查找", tool.Catalog.DisplayName);
        Assert.Equal("定位", tool.Catalog.Category);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        Assert.Equal("circle-find", tool.Catalog.IconKey);
        Assert.IsAssignableFrom<IToolSettingsFields>(tool);
        Assert.IsAssignableFrom<IToolCatalogProvider>(tool);
        Assert.IsAssignableFrom<IToolResultPresentationProvider>(tool);
        Assert.IsAssignableFrom<IToolResultDisplayProvider>(tool);
        Assert.IsAssignableFrom<IToolRuntimeInitialization>(tool);
    }

    [Fact]
    public void Describe_declares_image_roi_fixture_inputs_and_output_ports()
    {
        var schema = new CircleFindTool().Describe();
        var byId = schema.Inputs.ToDictionary(port => port.Id);
        Assert.True(byId.ContainsKey(CircleFindSchema.ImageInput));
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), byId[CircleFindSchema.ImageInput].Type);
        Assert.True(byId[CircleFindSchema.ImageInput].Required);
        Assert.Equal(FieldEditModes.Reference, byId[CircleFindSchema.ImageInput].EditModes);
        Assert.False(byId[CircleFindSchema.RoiInput].Required);
        Assert.True(byId[CircleFindSchema.FixtureInput].Nullable);
        Assert.Equal(CircleFindSchema.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(CircleFindSchema.Outputs, new CircleFindTool().ResultSchema.Outputs);
    }

    [Fact]
    public void Settings_defaults_match_original_parameters_json()
    {
        var s = new CircleFindSettings();
        Assert.Equal(1, s.FindMode);
        Assert.Equal(3, s.EdgePolarity);
        Assert.Equal(15, s.EdgeThresh);
        Assert.Equal(2, s.EdgeWidth);
        Assert.Equal(30, s.RadNum);
        Assert.Equal(0, s.RejectNum);
        Assert.False(s.CoarseDetectFlag);
        Assert.Equal(8, s.CcdSampleScale);
        Assert.Equal(10, s.CcdCircleThresh);
        Assert.Equal(5, s.RejectDist);
        Assert.Equal(5, s.ProLength);
        Assert.Equal(2, s.InitType);
        Assert.Equal(2, s.FitFun);
        Assert.False(s.RadiusLimitEnable);
        Assert.Equal(99999d, s.RadiusLimitHigh);
        Assert.Equal(99999d, s.CenterXLimitHigh);
        Assert.Equal(99999d, s.CenterYLimitHigh);
        Assert.Equal(100d, s.FitErrorLimitHigh);
        Assert.Equal(3, s.FitPointsLimitLow);
        Assert.Equal(200, s.FitPointsLimitHigh);
    }

    [Fact]
    public void Default_settings_pass_admission_and_out_of_range_is_rejected()
    {
        var tool = new CircleFindTool();
        Assert.Empty(tool.Validate(new CircleFindSettings()));
        var invalid = new CircleFindSettings { EdgeThresh = 256 };
        Assert.NotEmpty(tool.Validate(invalid));
        var invalidEnum = new CircleFindSettings { FindMode = 0 };
        Assert.NotEmpty(tool.Validate(invalidEnum));
        var invalidPair = new CircleFindSettings { RadiusLimitEnable = true, RadiusLimitLow = 100, RadiusLimitHigh = 10 };
        Assert.NotEmpty(tool.Validate(invalidPair));
    }

    [Fact]
    public void Settings_roundtrip_through_declared_fields()
    {
        var tool = new CircleFindTool();
        var source = new CircleFindSettings { FindMode = 2, EdgeThresh = 80, RadNum = 45, FitFun = 3,
            CenterXLimitEnable = true, CenterXLimitLow = -10, CenterXLimitHigh = 20 };
        var values = tool.ReadSettings(source);
        var restored = Assert.IsType<CircleFindSettings>(tool.MaterializeSettings(values));
        Assert.Equal(source.FindMode, restored.FindMode);
        Assert.Equal(source.EdgeThresh, restored.EdgeThresh);
        Assert.Equal(source.RadNum, restored.RadNum);
        Assert.Equal(source.FitFun, restored.FitFun);
        Assert.Equal(source.CenterXLimitEnable, restored.CenterXLimitEnable);
        Assert.Equal(source.CenterXLimitLow, restored.CenterXLimitLow);
    }

    [Fact]
    public void Result_projection_and_display_declarations_are_valid()
    {
        var tool = new CircleFindTool();
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        tool.ResultDisplay.Validate(tool.ResultSchema); // 无效声明会在此抛出异常
        Assert.Equal(ResultProjectionKind.StandardTable, tool.ResultProjection.Kind);
        Assert.Equal(6, tool.ResultProjection.Columns.Count);
    }

    [Fact]
    public void Display_settings_expose_all_original_image_view_items()
    {
        var tool = new CircleFindTool();
        var defaults = tool.CreateDisplaySettings();
        Assert.NotEmpty(defaults.CopyValues());
        Assert.NotEmpty(tool.DisplaySettingsSchema.Inputs);
        tool.ResultDisplay.Validate(tool.ResultSchema);
    }

    [Fact]
    public void Algorithm_pending_capability_and_session_are_explicit()
    {
        // 算法数值未取证：Session 明确诊断而非伪造数值；能力声明 halcon 供运行时预热。
        var tool = new CircleFindTool();
        Assert.NotNull(tool.CreateSession());
        Assert.Contains("halcon", tool.Capabilities().ResourceKeys);
    }
}
