using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.DataClassification.Runtime;
using SunEyeVision.Tool.MachineLearningClassifier.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ClassifyCalib.Tests;

/// <summary>
/// 分类类 UI 骨架的契约面：ToolId 与主线 catalog 一致、六必需契约、字段/页签来自原厂 UI 反编译证据、
/// 结果端口与原厂 parameters.json 对齐、Session 对未取证算法明确报诊断（rule-015 不兜底）。
/// </summary>
public sealed class ClassificationDeclarationTests
{
    [Fact]
    public void Data_classification_identity_matches_the_catalog_tool_id()
    {
        var tool = new DataClassificationTool();
        Assert.Equal("DataClassification", DataClassificationTool.Id);
        Assert.Equal("DataClassification", tool.ToolId);
        Assert.Equal("数据分类", tool.Catalog.DisplayName);
        Assert.Equal("逻辑工具", tool.Catalog.Category);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        var component = Assert.Single(typeof(DataClassificationTool).Assembly.GetCustomAttributes<ToolComponentAttribute>());
        Assert.Equal("SunEyeVision.Tool.DataClassification.Runtime", component.Metadata.ComponentId);
        Assert.Equal("DataClassification", component.Metadata.ToolId);
        Assert.Equal(ToolComponentRole.Runtime, component.Metadata.Role);
        Assert.Equal(FoundationContract.Major, component.Metadata.ContractMajor);
    }

    [Fact]
    public void ML_classifier_identity_matches_the_catalog_tool_id()
    {
        var tool = new MachineLearningClassifierTool();
        Assert.Equal("MachineLearningClassifier", MachineLearningClassifierTool.Id);
        Assert.Equal("MachineLearningClassifier", tool.ToolId);
        Assert.Equal("ML分类", tool.Catalog.DisplayName);
        Assert.Equal("识别", tool.Catalog.Category);
        var component = Assert.Single(typeof(MachineLearningClassifierTool).Assembly.GetCustomAttributes<ToolComponentAttribute>());
        Assert.Equal("SunEyeVision.Tool.MachineLearningClassifier.Runtime", component.Metadata.ComponentId);
        Assert.Equal("MachineLearningClassifier", component.Metadata.ToolId);
        Assert.Equal(ToolComponentRole.Runtime, component.Metadata.Role);
    }

    [Fact]
    public void Data_classification_declares_two_pages_and_four_ui_fields()
    {
        var presentation = DataClassificationFields.Schema.Presentation!;
        var fields = presentation.Fields;
        Assert.Equal(4, fields.Count);
        Assert.All(fields, field => Assert.NotNull(field.Layout));
        // 页签按原厂声明顺序：Tab_Basic Params 先，ClassificationSetting 后。
        Assert.Equal(new[] { "基本参数", "分类设置" },
            fields.Select(f => f.Layout!.Page).Distinct().ToArray());
        Assert.Contains(DataClassificationFields.InputSource, fields.Select(f => f.FieldId));
        Assert.Contains(DataClassificationFields.ClassificationSetting, fields.Select(f => f.FieldId));
        var defaults = new DataClassificationTool().ReadSettings(new DataClassificationSettings());
        Assert.Equal(string.Empty, defaults.Get<string>(DataClassificationFields.InputSource));
        Assert.False(defaults.Get<bool>(DataClassificationFields.OpenMutex));
    }

    [Fact]
    public void Data_classification_result_ports_match_original_outputs()
    {
        // 原厂 parameters.json.outputs 仅 ModuRunTime/ModuStatus 两项。
        Assert.Equal(2, DataClassificationOutput.Ports.Count);
        Assert.Contains(DataClassificationOutput.Status, DataClassificationOutput.Ports.Select(p => p.Id));
        Assert.Contains(DataClassificationOutput.RunTime, DataClassificationOutput.Ports.Select(p => p.Id));
        var tool = new DataClassificationTool();
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        Assert.Equal(ResultProjectionKind.PropertyList, tool.ResultProjection.Kind);
    }

    [Fact]
    public void ML_classifier_declares_all_25_parameters_and_defaults()
    {
        var settings = new MachineLearningClassifierSettings();
        Assert.Equal("1", settings.ClassifyType);
        Assert.Equal(1000, settings.SvmMaxIter);
        Assert.Equal(1000, settings.SvmEsp);
        Assert.True(settings.TextureFeature);
        Assert.True(settings.HogFeature);
        Assert.True(settings.GrayFeature);
        Assert.True(settings.GrayRangeFeature);
        Assert.True(settings.GrayHis);
        Assert.False(settings.LbpFeature);
        Assert.False(settings.NumLimitEnable);
        Assert.Equal(1, settings.NumLimitLow);
        Assert.Equal(99999, settings.NumLimitHigh);
        var read = new MachineLearningClassifierTool().ReadSettings(settings);
        Assert.Equal("1", read.Get<string>(MachineLearningClassifierFields.ClassifyType));
        Assert.Equal(25, read.Declarations.Count);
    }

    [Fact]
    public void ML_classifier_feature_visibility_follows_original_train_page()
    {
        var fields = MachineLearningClassifierFields.Schema.Presentation!.Fields;
        // 原厂 MLImagePage.xaml: TextureFeature 控制 Hog/Lbp/Glcm/Edge/Polar 显隐。
        foreach (var id in new[]
                 {
                     MachineLearningClassifierFields.HogFeature, MachineLearningClassifierFields.LbpFeature,
                     MachineLearningClassifierFields.GlcmFeature, MachineLearningClassifierFields.EdgeFeature,
                     MachineLearningClassifierFields.PolarFeature
                 })
        {
            var field = fields.Single(f => f.FieldId == id);
            Assert.Equal(MachineLearningClassifierFields.TextureFeature, field.Layout!.VisibleWhen!.FieldId);
            Assert.Equal(true, field.Layout.VisibleWhen.Value);
        }
        // GrayFeature 控制灰度子项显隐。
        Assert.Equal(MachineLearningClassifierFields.GrayFeature,
            fields.Single(f => f.FieldId == MachineLearningClassifierFields.GrayHis).Layout!.VisibleWhen!.FieldId);
    }

    [Fact]
    public void ML_classifier_result_ports_and_projection_are_self_consistent()
    {
        var tool = new MachineLearningClassifierTool();
        Assert.Equal(6, MachineLearningClassifierOutput.Ports.Count);
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        Assert.Equal(ResultProjectionKind.PropertyList, tool.ResultProjection.Kind);
        Assert.NotNull(tool.ResultSchema.FindOutputDisplay(MachineLearningClassifierOutput.Roi));
    }

    [Fact]
    public void Invalid_settings_are_reported_not_silently_accepted()
    {
        var badClassify = new MachineLearningClassifierSettings { ClassifyType = "9" };
        Assert.Contains(badClassify.Validate(), d => d.Code == "MLCLASSIFIER_SETTINGS");
        var badIter = new MachineLearningClassifierSettings { SvmMaxIter = 5 };
        Assert.Contains(badIter.Validate(), d => d.FieldId == MachineLearningClassifierFields.SvmMaxIter);
    }
}
