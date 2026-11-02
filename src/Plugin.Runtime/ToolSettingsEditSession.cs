using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One declaration-owned field state; no typed settings or reference backup authority.</summary>
public sealed class ToolSettingsEditSession : IToolSettingsDraft
{
    private readonly FieldSettingsDraft _draft;
    private readonly IToolDefinition _definition;
    private readonly IToolSettingsFields _declaration;
    public ToolSettingsEditSession(IToolDefinition definition,FieldSettings fields)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition=definition;
        _declaration=definition as IToolSettingsFields??throw new ArgumentException("Runtime settings are not declared.");
        _draft=new(SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema(definition),fields,ValidateEdit);
        ValidateComplete(_draft.CaptureFields());
    }
    private void ValidateEdit(FieldSettings candidate)
    {
        foreach(var port in SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema(_definition).Inputs.Where(port=>port.Required))
            if(candidate.Read(port.Id) is UnsetSetting)throw new ArgumentException("Missing required setting: "+port.Id);
        _=SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.Capture(_definition,candidate);
        foreach(var presentation in SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema(_definition).Presentation?.Fields??Array.Empty<FieldPresentation>())
        {
            if(presentation.Layout is not { } layout||candidate.Read(presentation.FieldId) is not ConstantSetting {Value:long or double} value)continue;
            var number=Convert.ToDouble(value.Value,System.Globalization.CultureInfo.InvariantCulture);
            if(!double.IsFinite(number)||layout.Minimum is {} min&&number<min||layout.Maximum is {} max&&number>max)
                throw new ArgumentException("Setting is outside its declared range: "+presentation.FieldId);
        }
    }
    private void ValidateComplete(FieldSettings candidate)
    {
        ValidateEdit(candidate);
        var algorithm=candidate.Select(_declaration.SettingsSchema.Inputs);
        if(algorithm.Capture().Values.Any(value=>value is ReferenceSetting or VariableSetting))return;
        var settings=_declaration.MaterializeSettings(algorithm.Materialize(new Dictionary<string,object?>()));
        var errors=_definition.Validate(settings);
        if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
    }
    public ToolSchema Schema=>_draft.Schema;
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
