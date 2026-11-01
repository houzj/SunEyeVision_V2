namespace SunEyeVision.Plugin.Contracts;

/// <summary>Pure selection over one immutable configuration. Unselected input settings remain
/// owned by the document for editing, copying and persistence, but are not execution dependencies.
/// The host freezes and validates the result once per configuration; settings parameters remain active.</summary>
public interface IToolExecutionInputs
{
    IReadOnlyList<string> SelectExecutionInputs(FieldSettings configuration);
    /// <summary>Checks branch-specific required inputs without resolving any source.</summary>
    IReadOnlyList<Diagnostic> ValidateExecutionInputs(FieldSettings configuration)=>Array.Empty<Diagnostic>();
}
