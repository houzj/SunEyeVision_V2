using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One declaration-owned field state; no typed settings or reference backup authority.</summary>
public sealed class ToolSettingsEditSession : IToolSettingsDraft
{
    private readonly FieldSettingsDraft _draft;
    private readonly IToolDefinition _definition;
    private readonly IToolSettingsFields _declaration;
    private readonly ToolSchema _settingsSchema;
    private readonly ToolSchema _editorSchema;
    public ToolSettingsEditSession(IToolDefinition definition,FieldSettings fields)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(fields);
        _definition=definition;
        _declaration=definition as IToolSettingsFields??throw new ArgumentException("Runtime settings are not declared.");
        _settingsSchema=_declaration.SettingsSchema;
        _editorSchema=SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema(definition);
        // 动态子项：按当前条数扩展设置端口和编辑器行，并补齐新端点的默认值。
        if(definition is IToolDynamicSettings dynamicSettings)
        {
            var expandedSettings=dynamicSettings.ExpandSettings(_settingsSchema,fields);
            var expandedEditor=dynamicSettings.ExpandSettings(_editorSchema,fields);
            if(!string.Equals(expandedEditor.Fingerprint,_editorSchema.Fingerprint,StringComparison.Ordinal))
            {
                fields=fields.Select(expandedEditor.Inputs);
                var defaults=dynamicSettings.ExpandDefaults(expandedEditor,fields);
                if(defaults.Count!=0)fields=fields.Apply(defaults);
                _settingsSchema=expandedSettings;
                _editorSchema=expandedEditor;
            }
        }
        _draft=new(_editorSchema,fields,ValidateEdit);
        ValidateComplete(_draft.CaptureFields());
    }
    private void ValidateEdit(FieldSettings candidate)
    {
        var required=_editorSchema.Inputs.Where(port=>port.Required);
        foreach(var port in required)
            if(candidate.Read(port.Id) is UnsetSetting)throw new ArgumentException("Missing required setting: "+port.Id);
        _=SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.Capture(_definition,candidate);
        foreach(var presentation in _editorSchema.Presentation?.Fields??Array.Empty<FieldPresentation>())
        {
            // 外部端口呈现（原厂 ButtonSelecter 一类槽位）只校验声明范围，不在这里解析来源。
            if(presentation.Layout is not { } layout||!candidate.Declarations.ContainsKey(presentation.FieldId)
                ||candidate.Read(presentation.FieldId) is not ConstantSetting {Value:long or double} value)continue;
            var number=Convert.ToDouble(value.Value,System.Globalization.CultureInfo.InvariantCulture);
            if(!double.IsFinite(number)||layout.Minimum is {} min&&number<min||layout.Maximum is {} max&&number>max)
                throw new ArgumentException("Setting is outside its declared range: "+presentation.FieldId);
        }
    }
    private void ValidateComplete(FieldSettings candidate)
    {
        ValidateEdit(candidate);
        var algorithm=candidate.Select(_settingsSchema.Inputs);
        if(algorithm.Capture().Values.Any(value=>value is ReferenceSetting or VariableSetting))return;
        var settings=_declaration.MaterializeSettings(algorithm.Materialize(new Dictionary<string,object?>()));
        var errors=_definition.Validate(settings);
        if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
    }
    public ToolSchema Schema=>_editorSchema;
    public long Revision=>_draft.Revision;
    public FieldSettings CaptureFields()=>_draft.CaptureFields();
    public FieldSettings CaptureValidatedFields()
    {
        var revision=Revision;var snapshot=_draft.CaptureFields();ValidateComplete(snapshot);
        if(revision!=Revision)throw new InvalidOperationException("Settings changed during capture.");
        return snapshot;
    }
    public void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate)
        =>_draft.ApplyFields(changes,acceptCandidate);
}
