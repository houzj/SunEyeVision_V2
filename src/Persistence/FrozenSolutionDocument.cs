using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Persistence;

public sealed record PersistenceDiagnostic(string Code, string Stage, string Severity, string JsonPath,
    string Message, string? WorkflowId = null, string? NodeId = null, string? VariableId = null);
public sealed record FrozenResourceReference(string JsonPath, string Reference, ResourcePurpose Purpose, string? AbsolutePath);
public sealed record FrozenSaveReceipt(FrozenSolutionDocument WrittenSnapshot, string? Warning);
public enum TargetExpectationKind { Unchecked, Missing, MatchSha256 }
public sealed class TargetExpectation
{
    private TargetExpectation(TargetExpectationKind kind,string? hash) { Kind=kind;Sha256=hash; }
    public TargetExpectationKind Kind { get; }
    public string? Sha256 { get; }
    public static TargetExpectation Unchecked { get; }=new(TargetExpectationKind.Unchecked,null);
    public static TargetExpectation Missing { get; }=new(TargetExpectationKind.Missing,null);
    public static TargetExpectation Match(string sha256)
    {
        if(sha256 is null || sha256.Length!=64 || !sha256.All(Uri.IsHexDigit))throw new ArgumentException("Expected target SHA256 must contain 64 hex digits.",nameof(sha256));
        return new(TargetExpectationKind.MatchSha256,sha256.ToUpperInvariant());
    }
}

/// <summary>Exact owned bytes captured and encoded by the session before IO. It holds no live document or native value.</summary>
public sealed class FrozenSolutionDocument
{
    private readonly byte[] _bytes;
    public FrozenSolutionDocument(string targetPath, ReadOnlySpan<byte> bytes,
        IEnumerable<PersistenceDiagnostic> diagnostics, IEnumerable<FrozenResourceReference> resourcePlan,
        TargetExpectation? targetExpectation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        TargetPath = Path.GetFullPath(targetPath);
        _bytes = bytes.ToArray();
        var json = new System.Text.UTF8Encoding(false,true).GetString(_bytes);
        using (var document = System.Text.Json.JsonDocument.Parse(json)) SolutionFormatPolicy.ValidateReadable(document.RootElement);
        CanonicalContent = SolutionSemanticComparer.Canonicalize(json);
        Diagnostics = Array.AsReadOnly((diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).ToArray());
        ResourcePlan = Array.AsReadOnly((resourcePlan ?? throw new ArgumentNullException(nameof(resourcePlan))).ToArray());
        if(Diagnostics.Any(d=>d is null) || ResourcePlan.Any(r=>r is null))throw new ArgumentException("Captured diagnostics and resources must be non-null.");
        TargetExpectation=targetExpectation ?? TargetExpectation.Unchecked;
    }
    public string TargetPath { get; }
    public string CanonicalContent { get; }
    public IReadOnlyList<PersistenceDiagnostic> Diagnostics { get; }
    public IReadOnlyList<FrozenResourceReference> ResourcePlan { get; }
    public TargetExpectation TargetExpectation { get; }
    public byte[] CopyBytes() => (byte[])_bytes.Clone();
}
