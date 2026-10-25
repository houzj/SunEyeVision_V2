namespace SunEyeVision.Plugin.Contracts;

public enum ToolComponentRole { Runtime, Editor }

/// <summary>One physical component declaration. An editor is optional and never an algorithm producer.</summary>
public sealed record ToolComponentMetadata
{
    public ToolComponentMetadata(string componentId, string toolId, ToolComponentRole role,
        int contractMajor, int contractMinor, string? companionToolId = null)
    {
        if (string.IsNullOrWhiteSpace(componentId) || string.IsNullOrWhiteSpace(toolId))
            throw new ArgumentException("Component and tool identities are required.");
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (contractMajor < 0 || contractMinor < 0) throw new ArgumentOutOfRangeException(nameof(contractMajor));
        if (role == ToolComponentRole.Runtime && companionToolId is not null)
            throw new ArgumentException("Runtime is the producer, not a companion extension.");
        if (role == ToolComponentRole.Editor && companionToolId != toolId)
            throw new ArgumentException("Editor must explicitly name its same-ToolId runtime companion.");
        ComponentId = componentId; ToolId = toolId; Role = role;
        ContractMajor = contractMajor; ContractMinor = contractMinor; CompanionToolId = companionToolId;
    }
    public string ComponentId { get; }
    public string ToolId { get; }
    public ToolComponentRole Role { get; }
    public int ContractMajor { get; }
    public int ContractMinor { get; }
    public string? CompanionToolId { get; }
}
