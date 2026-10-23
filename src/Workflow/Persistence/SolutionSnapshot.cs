using System.IO;
using System.Text.Json.Nodes;
using System.Collections.Generic;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>The exact immutable bytes-to-write projection, shared by validation and session baseline.</summary>
public sealed class SolutionSnapshot
{
    public string Json { get; }
    public string CanonicalContent { get; }
    public string TargetPath { get; }
    public IReadOnlyList<PersistenceDiagnostic> Diagnostics { get; }
    private SolutionSnapshot(string json, string canonicalContent, string targetPath, IReadOnlyList<PersistenceDiagnostic> diagnostics)
        => (Json, CanonicalContent, TargetPath, Diagnostics) = (json, canonicalContent, targetPath, diagnostics);
    public static SolutionSnapshot Prepare(Solution document, string targetPath)
        => PrepareJson(SolutionSession.Serialize(document), targetPath, string.IsNullOrEmpty(document.FilePath));

    internal static SolutionSnapshot PrepareJson(string json, string targetPath, bool untitled)
    {
        var root = JsonNode.Parse(json) ?? throw new System.Text.Json.JsonException("Empty solution snapshot.");
        if (untitled && (string.IsNullOrWhiteSpace(root["Name"]?.GetValue<string>()) || root["Name"]?.GetValue<string>() == "未命名方案"))
            root["Name"] = Path.GetFileNameWithoutExtension(targetPath);
        var diagnostics = new List<PersistenceDiagnostic>();
        SolutionCompatibilityAdapter.Prepare(root, diagnostics);
        var normalized = SolutionSemanticComparer.Canonicalize(root.ToJsonString());
        return new SolutionSnapshot(normalized, normalized, Path.GetFullPath(targetPath), diagnostics.AsReadOnly());
    }
}
