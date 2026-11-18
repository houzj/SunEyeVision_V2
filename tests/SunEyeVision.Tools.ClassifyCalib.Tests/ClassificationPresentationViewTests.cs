using System.Runtime.Versioning;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.DataClassification.Runtime;
using SunEyeVision.Tool.DataClassification.Wpf.Views;
using SunEyeVision.Tool.MachineLearningClassifier.Runtime;
using SunEyeVision.Tool.MachineLearningClassifier.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.ClassifyCalib.Tests;

/// <summary>
/// 呈现层端到端：参数页必须能按正式声明组装（P1 ReadyTask、P2 InputSection 落位），
/// EagerlyComposePages 打开时全部页签都能构建；结果页必须能用正式输出声明构建。
/// 这些是历史上缺一项就会导致宿主"参数页组装失败"的契约。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ClassificationPresentationViewTests
{
    private static void OnSta(Action action) => WpfPresentationHost.Run(action);

    private sealed class DesignDraft : IToolSettingsDraft
    {
        private readonly FieldSettingsDraft _draft;
        public DesignDraft(ToolSchema schema, InputValues defaults)
        {
            _draft = new(schema, new FieldSettings(schema.Inputs,
                defaults.CopyValues().ToDictionary(p => p.Key, p => (ParameterSetting)new ConstantSetting(p.Value), StringComparer.Ordinal)));
        }
        public ToolSchema Schema => _draft.Schema;
        public long Revision => _draft.Revision;
        public FieldSettings CaptureFields() => _draft.CaptureFields();
        public FieldSettings CaptureValidatedFields() => _draft.CaptureValidatedFields();
        public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
            => _draft.ApplyFields(changes, acceptCandidate);
    }

    private static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) DataContext()
    {
        var tool = new DataClassificationTool();
        var declaration = (IToolSettingsFields)tool;
        var defaults = declaration.ReadSettings(new DataClassificationSettings());
        return (new DesignDraft(tool.SettingsSchema, defaults), new ToolSettingsViewOptions { EagerlyComposePages = true });
    }

    private static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) MLContext()
    {
        var tool = new MachineLearningClassifierTool();
        var declaration = (IToolSettingsFields)tool;
        var defaults = declaration.ReadSettings(new MachineLearningClassifierSettings());
        return (new DesignDraft(tool.SettingsSchema, defaults), new ToolSettingsViewOptions { EagerlyComposePages = true });
    }

    [Fact]
    public void Data_classification_parameter_view_composes_all_pages()
    {
        OnSta(() =>
        {
            var (draft, options) = DataContext();
            var editor = new DataClassificationParameterEditor(draft, options);
            try
            {
                Assert.True(editor.ReadyTask.IsCompleted);
                Assert.NotNull(editor.View);
                Assert.False(editor.HasInvalidInput);
            }
            finally { editor.Dispose(); }
        });
    }

    [Fact]
    public void ML_classifier_parameter_view_composes_all_pages()
    {
        OnSta(() =>
        {
            var (draft, options) = MLContext();
            var editor = new MachineLearningClassifierParameterEditor(draft, options);
            try
            {
                Assert.True(editor.ReadyTask.IsCompleted);
                Assert.NotNull(editor.View);
                Assert.False(editor.HasInvalidInput);
            }
            finally { editor.Dispose(); }
        });
    }

    [Fact]
    public void Both_result_views_build_from_the_formal_output_declarations()
    {
        OnSta(() =>
        {
            var dataTool = new DataClassificationTool();
            var dataFactory = new SunEyeVision.Tool.DataClassification.Wpf.DataClassificationEditorFactory();
            using var dataView = dataFactory.CreateResultView(dataTool.ResultSchema, dataTool.ResultProjection);
            Assert.NotNull(dataView.View);

            var mlTool = new MachineLearningClassifierTool();
            var mlFactory = new SunEyeVision.Tool.MachineLearningClassifier.Wpf.MachineLearningClassifierEditorFactory();
            using var mlView = mlFactory.CreateResultView(mlTool.ResultSchema, mlTool.ResultProjection);
            Assert.NotNull(mlView.View);
        });
    }
}
