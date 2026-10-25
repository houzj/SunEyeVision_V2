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
            if (component.ContractMajor != FoundationContract.Major || component.ContractMinor != FoundationContract.Minor)
                throw new ArgumentException("All components must match the current contract version.");
            var role = component.Role == ToolComponentRole.Runtime ? runtimes : editors;
            if (!role.TryAdd(component.ToolId, component)) throw new ArgumentException("Duplicate tool component role.");
        }
        foreach (var editor in editors.Values)
            if (!runtimes.ContainsKey(editor.CompanionToolId!))
                throw new ArgumentException("Editor has no companion runtime in the complete candidate set.");
        return new(runtimes, editors);
    }
}
