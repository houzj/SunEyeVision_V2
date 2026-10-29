using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One declaration-owned field state; no typed settings or reference backup authority.</summary>
public sealed class ToolSettingsEditSession : IToolSettingsDraft
{
    private readonly FieldSettingsDraft _draft;
    public ToolSettingsEditSession(IToolDefinition definition,FieldSettings fields)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var declaration=definition as IToolSettingsFields??throw new ArgumentException("Runtime settings are not declared.");
        _draft=new(declaration.SettingsSchema,fields,candidate=>
        {
            foreach(var port in declaration.SettingsSchema.Inputs.Where(port=>port.Required))
                if(candidate.Read(port.Id) is UnsetSetting)throw new ArgumentException("Missing required setting: "+port.Id);
            if(candidate.Capture().Values.Any(value=>value is ReferenceSetting))return;
            var settings=declaration.MaterializeSettings(candidate.Materialize(new Dictionary<string,object?>()));
            var errors=definition.Validate(settings);
            if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
        });
    }
    public ToolSchema Schema=>_draft.Schema;
    public FieldSettings CaptureFields()=>_draft.CaptureFields();
    public void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate)
        =>_draft.ApplyFields(changes,acceptCandidate);
}
