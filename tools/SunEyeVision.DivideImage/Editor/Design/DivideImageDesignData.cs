using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.DivideImage.Runtime;

namespace SunEyeVision.Tool.DivideImage.Wpf.Design;

/// <summary>仅用于设计器的初始状态，仍然经由真实 Runtime 的字段声明与默认值。</summary>
public static class DivideImageDesignData
{
    public static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) CreateParameterContext()
        => (new DesignDraft(), new ToolSettingsViewOptions());

    private sealed class DesignDraft : IToolSettingsDraft
    {
        internal DivideImageTool Tool { get; } = new();
        private readonly FieldSettingsDraft _draft;

        public DesignDraft()
        {
            var declaration = (IToolSettingsFields)Tool;
            var schema = ResultDisplayFields.EditorSchema(Tool);
            var defaults = declaration.ReadSettings(new DivideImageSettings()).CopyValues();
            _draft = new(schema, new FieldSettings(schema.Inputs,
                defaults.ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value),
                    StringComparer.Ordinal)));
        }

        public ToolSchema Schema => _draft.Schema;
        public long Revision => _draft.Revision;
        public FieldSettings CaptureFields() => _draft.CaptureFields();
        public FieldSettings CaptureValidatedFields() => _draft.CaptureValidatedFields();
        public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
            => _draft.ApplyFields(changes, acceptCandidate);
    }
}
