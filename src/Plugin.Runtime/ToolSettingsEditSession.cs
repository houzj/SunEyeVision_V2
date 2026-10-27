using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One private settings instance. No SDK parameters, second business DTO,
/// workflow publication, resources or WPF callbacks are retained here.</summary>
public sealed class ToolSettingsEditSession : IToolSettingsDraft
{
    private readonly IToolDefinition _definition;
    private readonly IToolSettingsFields _fields;
    private object _settings;
    private bool _applying;
    public ToolSettingsEditSession(IToolDefinition definition,object settings)
    {
        _definition=definition??throw new ArgumentNullException(nameof(definition));
        _fields=definition as IToolSettingsFields??throw new ArgumentException("Runtime has no settings field declaration.");
        if(_fields.SettingsSchema.Outputs.Count!=0)throw new ArgumentException("Settings fields cannot declare algorithm outputs.");
        _settings=Prepare(settings);
    }
    public ToolSchema Schema=>_fields.SettingsSchema;
    public object CaptureSettings()=>_definition.FreezeSettings(_settings);
    public InputValues ReadFields()=>new(Schema.Inputs,_fields.ReadSettings(_settings).CopyValues());
    private object Prepare(object settings)
    {
        var candidate=_definition.FreezeSettings(settings);
        var diagnostics=_definition.Validate(candidate);
        if(diagnostics.Count!=0)throw new ArgumentException(string.Join("; ",diagnostics.Select(x=>x.Message)));
        _=new InputValues(_fields.SettingsSchema.Inputs,_fields.ReadSettings(candidate).CopyValues());
        return candidate;
    }
    public void ApplyFields(IReadOnlyDictionary<string,object?> changes,Action<object> acceptCandidate)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(acceptCandidate);
        if(_applying)throw new InvalidOperationException("A settings proposal is already being accepted.");
        _applying=true;
        try
        {
            var candidate=Prepare(_fields.ApplySettings(CaptureSettings(),changes));
            // The callback receives a separate frozen value, never the draft's next mutable instance.
            acceptCandidate(_definition.FreezeSettings(candidate));
            _settings=candidate;
        }
        finally{_applying=false;}
    }
}
