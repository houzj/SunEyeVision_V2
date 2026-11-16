using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Validates a complete candidate set before publishing an immutable description.
/// This S1 contract consumer does not register or load production plugins; S3 connects the actual host.</summary>
public sealed class ToolComponentCatalog
{
    private ToolComponentCatalog(Dictionary<string, ToolComponentMetadata> runtimes,
        Dictionary<string, ToolComponentMetadata> editors)
    {
        Runtimes = new ReadOnlyDictionary<string, ToolComponentMetadata>(runtimes);
        Editors = new ReadOnlyDictionary<string, ToolComponentMetadata>(editors);
    }
    public IReadOnlyDictionary<string, ToolComponentMetadata> Runtimes { get; }
    public IReadOnlyDictionary<string, ToolComponentMetadata> Editors { get; }

    public static ToolComponentCatalog Prepare(IEnumerable<ToolComponentMetadata> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var runtimes = new Dictionary<string, ToolComponentMetadata>(StringComparer.Ordinal);
        var editors = new Dictionary<string, ToolComponentMetadata>(StringComparer.Ordinal);
        var components = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in candidate)
        {
            ArgumentNullException.ThrowIfNull(component);
            if (!components.Add(component.ComponentId)) throw new ArgumentException("Duplicate physical component identity.");
            // Compatibility window: major identifies breaking contract shape and must match
            // exactly; minor only adds capability, so a component built against an older minor
            // stays admissible. A component built against a newer minor uses declarations this
            // host does not have and must be rejected instead of failing later at execution.
            if (!FoundationContract.IsCompatible(component.ContractMajor, component.ContractMinor))
                throw new ArgumentException(
                    $"Component contract {component.ContractMajor}.{component.ContractMinor} is outside host window " +
                    $"{FoundationContract.Major}.{FoundationContract.OldestCompatibleMinor}..{FoundationContract.Major}.{FoundationContract.Minor} " +
                    $"(component {component.ComponentId}).");
            var role = component.Role == ToolComponentRole.Runtime ? runtimes : editors;
            if (!role.TryAdd(component.ToolId, component)) throw new ArgumentException("Duplicate tool component role.");
        }
        foreach (var editor in editors.Values)
            if (!runtimes.ContainsKey(editor.CompanionToolId!))
                throw new ArgumentException("Editor has no companion runtime in the complete candidate set.");
        return new(runtimes, editors);
    }
}
