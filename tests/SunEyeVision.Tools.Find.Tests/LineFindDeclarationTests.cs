using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.LineFind.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Find.Tests;

/// <summary>
/// 直线查找契约面：六必需契约 + 呈现契约 + 运行时预热；字段默认值逐项锚定原厂 parameters.json，
/// 范围/枚举锚定托管 LineFindParam.cs（FindMode 4 值）。算法数值待补证，不验证数值输出。
/// </summary>
public sealed class LineFindDeclarationTests
{
    [Fact]
    public void Runtime_declares_stable_identity_catalog_and_required_contracts()
    {
        var tool = new LineFindTool();
        Assert.Equal("LineFind", tool.ToolId);
        Assert.Equal("直线查找", tool.Catalog.DisplayName);
        Assert.Equal("定位", tool.Catalog.Category);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        Assert.Equal("line-find", tool.Catalog.IconKey);
        Assert.IsAssignableFrom<IToolSettingsFields>(tool);
        Assert.IsAssignableFrom<IToolCatalogProvider>(tool);
        Assert.IsAssignableFrom<IToolResultPresentationProvider>(tool);
        Assert.IsAssignableFrom<IToolResultDisplayProvider>(tool);
        Assert.IsAssignableFrom<IToolRuntimeInitialization>(tool);
    }

    [Fact]
    public void Describe_declares_image_roi_fixture_inputs_and_output_ports()
    {
        var schema = new LineFindTool().Describe();
        var byId = schema.Inputs.ToDictionary(port => port.Id);
        Assert.True(byId.ContainsKey(LineFindSchema.ImageInput));
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), byId[LineFindSchema.ImageInput].Type);
        Assert.True(byId[LineFindSchema.ImageInput].Required);
        Assert.False(byId[LineFindSchema.RoiInput].Required);
        Assert.True(byId[LineFindSchema.FixtureInput].Nullable);
        Assert.Equal(LineFindSchema.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(LineFindSchema.Outputs, new LineFindTool().ResultSchema.Outputs);
    }

    [Fact]
    public void Settings_defaults_match_original_parameters_json()
    {
        var s = new LineFindSettings();
        Assert.Equal(1, s.FindMode);
        Assert.Equal(3, s.EdgePolarity);
        Assert.Equal(15, s.EdgeStrength);
        Assert.Equal(2, s.KernelSize);
        Assert.Equal(20, s.RayNum);
        Assert.False(s.RevertFindOrient);
        Assert.False(s.AngleNormalization);
        Assert.False(s.LineAngleEnable);
        Assert.Equal(0d, s.ExpectAngle);
        Assert.Equal(180d, s.RotateTolerance);
        Assert.Equal(0, s.RejectNum);
        Assert.Equal(5, s.RejectDist);
        Assert.Equal(5, s.RegionWidth);
        Assert.Equal(2, s.InitType);
        Assert.Equal(2, s.FitFun);
        Assert.Equal(2, s.FitPointsLimitLow);
        Assert.Equal(200, s.FitPointsLimitHigh);
        Assert.Equal(-180d, s.AngleLimitLow);
        Assert.Equal(180d, s.AngleLimitHigh);
        Assert.Equal(0d, s.ScoreLimitLow);
        Assert.Equal(9999d, s.ScoreLimitHigh);
    }

    [Fact]
    public void Default_settings_pass_admission_and_out_of_range_is_rejected()
    {
        var tool = new LineFindTool();
        Assert.Empty(tool.Validate(new LineFindSettings()));
        Assert.NotEmpty(tool.Validate(new LineFindSettings { FindMode = 5 })); // 原厂 FindMode 仅 1-4
        Assert.NotEmpty(tool.Validate(new LineFindSettings { EdgeStrength = 0 }));
        Assert.NotEmpty(tool.Validate(new LineFindSettings { RayNum = 1 }));
        Assert.NotEmpty(tool.Validate(new LineFindSettings { RegionWidth = 101 }));
        Assert.NotEmpty(tool.Validate(new LineFindSettings { AngleLimitEnable = true, AngleLimitLow = 100, AngleLimitHigh = -100 }));
    }

    [Fact]
    public void Settings_roundtrip_through_declared_fields()
    {
        var tool = new LineFindTool();
        var source = new LineFindSettings { FindMode = 4, EdgeStrength = 90, RayNum = 33, FitFun = 1,
            LineAngleEnable = true, ExpectAngle = 45, AngleLimitEnable = true, AngleLimitLow = -30, AngleLimitHigh = 30 };
        var values = tool.ReadSettings(source);
        var restored = Assert.IsType<LineFindSettings>(tool.MaterializeSettings(values));
        Assert.Equal(source.FindMode, restored.FindMode);
        Assert.Equal(source.EdgeStrength, restored.EdgeStrength);
        Assert.Equal(source.RayNum, restored.RayNum);
        Assert.Equal(source.FitFun, restored.FitFun);
        Assert.Equal(source.ExpectAngle, restored.ExpectAngle);
        Assert.Equal(source.AngleLimitLow, restored.AngleLimitLow);
    }

    [Fact]
    public void Result_projection_and_display_declarations_are_valid()
    {
        var tool = new LineFindTool();
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        tool.ResultDisplay.Validate(tool.ResultSchema);
        Assert.Equal(ResultProjectionKind.StandardTable, tool.ResultProjection.Kind);
        Assert.Equal(8, tool.ResultProjection.Columns.Count);
    }

    [Fact]
    public void Display_settings_expose_all_original_image_view_items()
    {
        var tool = new LineFindTool();
        var defaults = tool.CreateDisplaySettings();
        Assert.NotEmpty(defaults.CopyValues());
        Assert.NotEmpty(tool.DisplaySettingsSchema.Inputs);
        tool.ResultDisplay.Validate(tool.ResultSchema);
    }

    [Fact]
    public void Algorithm_pending_capability_and_session_are_explicit()
    {
        var tool = new LineFindTool();
        Assert.NotNull(tool.CreateSession());
        Assert.Contains("halcon", tool.Capabilities().ResourceKeys);
    }
}
