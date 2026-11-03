using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Persistence.Variables;

namespace SunEyeVision.Persistence;

/// <summary>A single independently versioned variable-file transaction; candidates own their decoded resources.</summary>
public static class VariableArchive
{
    public const string Format = "SunEyeVision.GlobalVariables";
    public const int FormatVersion = 1;

    public static VariableArchiveCandidate Read(ReadOnlySpan<byte> bytes, FrozenVariableTypeCatalog types,
        JsonSerializerOptions sourceOptions, DecodedResourcesScope scope, IVariableValueExtensionCodec? extension = null)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(sourceOptions);
        ArgumentNullException.ThrowIfNull(scope);
        try
        {
            if (extension != null && !ReferenceEquals(extension.Scope, scope))
                throw new InvalidOperationException("Codec and candidate must own the same resource scope.");
            using var document = JsonDocument.Parse(bytes.ToArray());
            SolutionDocumentValidator.CheckDuplicateKeys(document.RootElement.GetRawText());
            var root = JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject ?? throw new JsonException("Variable archive must be an object.");
            if (root.Count != 3 || !root.ContainsKey("Format") || !root.ContainsKey("FormatVersion") || !root.ContainsKey("Variables") ||
                root["Format"]?.GetValue<string>() != Format || root["FormatVersion"]?.GetValue<int>() != FormatVersion || root["Variables"] is not JsonArray items)
                throw new JsonException("Unsupported variable archive format or version.");
            var diagnostics = new List<PersistenceDiagnostic>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i] ?? throw new JsonException("Null variable.");
                SolutionDocumentValidator.ValidateVariable(item, "$.Variables[" + i + "]", types, diagnostics);
                if (!ids.Add(item["Id"]?.GetValue<string>() ?? "")) throw new JsonException("Duplicate variable identity.");
            }
            var options = SolutionSerializationOptions.Configure(sourceOptions, extension);
            var variables = JsonSerializer.Deserialize<List<GlobalVariable>>(items.ToJsonString(), options) ?? throw new JsonException("Invalid variables.");
            return new VariableArchiveCandidate(scope, bytes, variables.AsReadOnly(), diagnostics.AsReadOnly());
        }
        catch { scope.Dispose(); throw; }
    }

    /// <summary>Call synchronously while the application owns its value gate; no borrowed value is retained.</summary>
    public static byte[] Encode(IReadOnlyList<GlobalVariable> variables, JsonSerializerOptions sourceOptions, IVariableValueExtensionCodec? extension = null)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var options = SolutionSerializationOptions.Configure(sourceOptions, extension);
        return JsonSerializer.SerializeToUtf8Bytes(new { Format, FormatVersion, Variables = variables }, options);
    }

}

public sealed class VariableArchiveCandidate : IDisposable
{
    private readonly DecodedResourcesScope _scope;
    private readonly byte[] _bytes;
    internal VariableArchiveCandidate(DecodedResourcesScope scope, ReadOnlySpan<byte> bytes, IReadOnlyList<GlobalVariable> variables, IReadOnlyList<PersistenceDiagnostic> diagnostics)
        { _scope = scope; _bytes = bytes.ToArray(); Variables = variables; Diagnostics = diagnostics; }
    internal string? WriteValidated(string path, string recoveryPath, TargetExpectation? expected = null, CancellationToken cancellationToken = default)
        => SolutionFileCommit.Write(path, recoveryPath, _bytes.ToArray(), true, targetExpectation: expected, cancellationToken: cancellationToken);
    public IReadOnlyList<GlobalVariable> Variables { get; }
    public IReadOnlyList<PersistenceDiagnostic> Diagnostics { get; }
    public IDisposable TransferResources() => _scope.Transfer();
    public void Dispose() => _scope.Dispose();
}
