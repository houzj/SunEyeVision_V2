namespace SunEyeVision.Plugin.Contracts;

/// <summary>Tool-declared field effects that are applied in the same edit as their trigger.</summary>
public interface IToolFieldTransitions
{
    IReadOnlyDictionary<string, ParameterSetting> ExpandChanges(
        FieldSettings current, IReadOnlyDictionary<string, ParameterSetting> changes);

    /// <summary>Projects an input editor's accepted setting without modifying the persistent fields.</summary>
    ParameterSetting ProjectInput(FieldSettings current, string fieldId) => current.Read(fieldId);
}
