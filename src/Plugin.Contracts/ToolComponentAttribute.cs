namespace SunEyeVision.Plugin.Contracts;

/// <summary>Physical component identity, read from the PE before entry types are published.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ToolComponentAttribute : Attribute
{
    public ToolComponentAttribute(string componentId, string toolId, string role, int contractMajor,
        int contractMinor, string entryType, string? companionToolId = null)
    {
        if (role is not ("Runtime" or "Editor")) throw new ArgumentException("Unknown component role.", nameof(role));
        if (string.IsNullOrWhiteSpace(entryType)) throw new ArgumentException("Component entry type is required.", nameof(entryType));
        Metadata = new(componentId, toolId, Enum.Parse<ToolComponentRole>(role), contractMajor, contractMinor, string.IsNullOrEmpty(companionToolId)?null:companionToolId);
        EntryType = entryType;
    }
    public ToolComponentMetadata Metadata { get; }
    public string EntryType { get; }
}
