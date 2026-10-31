namespace SunEyeVision.Plugin.Contracts;

/// <summary>A declared output or exact member path available for ordinary manual references.</summary>
public sealed class ManualReferenceSource
{
    public ManualReferenceSource(string portId, IReadOnlyList<string>? memberPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);
        PortId = portId;
        var path = (memberPath ?? Array.Empty<string>()).ToArray();
        if (path.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Member path entries must be non-empty.", nameof(memberPath));
        MemberPath = Array.AsReadOnly(path);
    }
    public string PortId { get; }
    public IReadOnlyList<string> MemberPath { get; }
}

/// <summary>Explicit source eligibility for ordinary manual parameter references.</summary>
public interface IToolManualReferenceSources
{
    IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; }
}