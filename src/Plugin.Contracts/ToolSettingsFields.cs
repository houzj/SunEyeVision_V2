namespace SunEyeVision.Plugin.Contracts;

/// <summary>Finite editing/binding declaration over the Runtime's actual settings type.
/// Read values are canonical projections; Apply returns a new settings instance and
/// must never mutate the supplied settings. UI resources and workflow state are excluded.</summary>
public interface IToolSettingsFields
{
    ToolSchema SettingsSchema { get; }
    InputValues ReadSettings(object settings);
    object ApplySettings(object settings,IReadOnlyDictionary<string,object?> changes);
}

/// <summary>Private, model-only editor state. Applying a rejected proposal must leave
/// the current settings unchanged. Raw text and source selection remain view concerns.</summary>
public interface IToolSettingsDraft
{
    ToolSchema Schema { get; }
    object CaptureSettings();
    InputValues ReadFields();
    /// <summary>Prepare the complete candidate, call the host once, then install it.
    /// A rejected or reentrant callback leaves this draft unchanged.</summary>
    void ApplyFields(IReadOnlyDictionary<string,object?> changes,Action<object> acceptCandidate);
}
