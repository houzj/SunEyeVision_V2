namespace SunEyeVision.Plugin.Contracts;

/// <summary>Finite editing/binding declaration over the Runtime's actual settings type.
/// Read values are canonical projections. Materialization requires the complete resolved
/// field set; it never fills missing fields from a prior/default settings object.</summary>
public interface IToolSettingsFields
{
    ToolSchema SettingsSchema { get; }
    InputValues ReadSettings(object settings);
    object MaterializeSettings(InputValues values);
}

/// <summary>Validate known settings at admission without resolving subscriptions or inventing values.</summary>
public interface IToolKnownSettingsValidation
{
    IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings);
}
/// <summary>Private, model-only editor state. Applying a rejected proposal must leave
/// the current settings unchanged. Raw text and source selection remain view concerns.</summary>
public interface IToolSettingsDraft
{
    ToolSchema Schema { get; }
    long Revision { get; }
    FieldSettings CaptureFields();
    /// <summary>Capture the current private edit snapshot after complete settings validation.</summary>
    FieldSettings CaptureValidatedFields();
    /// <summary>Prepare the complete candidate, call the host once, then install it.
    /// A rejected or reentrant callback leaves this draft unchanged.</summary>
    void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate);
}

/// <summary>Atomic model-only editing of the actual declaration and one frozen field snapshot.</summary>
public sealed class FieldSettingsDraft : IToolSettingsDraft
{
    private FieldSettings _fields;
    private readonly Action<FieldSettings>? _validate;
    private bool _applying;
    public ToolSchema Schema { get; }
    public long Revision {get;private set;}
    public FieldSettingsDraft(ToolSchema schema,FieldSettings fields,Action<FieldSettings>? validate=null)
    {Schema=schema;_fields=fields.Select(schema.Inputs);_validate=validate;_validate?.Invoke(_fields);}
    public FieldSettings CaptureFields()=>_fields;
    public FieldSettings CaptureValidatedFields()
    {
        var revision=Revision;var snapshot=CaptureFields();_validate?.Invoke(snapshot);
        if(revision!=Revision)throw new InvalidOperationException("Settings changed during capture.");
        return snapshot;
    }
    public void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate)
    {
        ArgumentNullException.ThrowIfNull(changes);ArgumentNullException.ThrowIfNull(acceptCandidate);
        if(_applying)throw new InvalidOperationException("A settings proposal is already being accepted.");
        _applying=true;
        try
        {
            var candidate=_fields.Apply(changes);_validate?.Invoke(candidate);
            acceptCandidate(candidate);
            _fields=candidate;
            Revision++;
        }
        finally{_applying=false;}
    }
}
