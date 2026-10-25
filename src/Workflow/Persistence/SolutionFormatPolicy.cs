using System.Text.Json;
using System.Text.Json.Nodes;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Disk format policy, independent from contract ABI, schema fingerprints and configuration revisions.</summary>
internal static class SolutionFormatPolicy
{
    public const int Current = 2;
    public const int Legacy = 1;

    public static int ReadVersion(JsonNode root)
    {
        if (root is not JsonObject) throw InvalidVersion("A solution must be a JSON object.");
        if (!((JsonObject)root).TryGetPropertyValue("SchemaVersion", out var value)) return Legacy;
        if (value is not JsonValue number || !number.TryGetValue<int>(out var version)
            || version is not (Legacy or Current))
            throw InvalidVersion("Unsupported solution format; expected version 1 or 2.");
        return version;
    }

    public static void ValidateReadable(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw InvalidVersion("A solution must be a JSON object.");
        if (root.TryGetProperty("SchemaVersion", out var version)
            && (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number)
                || number is not (Legacy or Current)))
            throw InvalidVersion("Unsupported solution format; expected version 1 or 2.");
    }

    public static void PromoteCandidate(JsonNode root) => root["SchemaVersion"] = Current;
    private static JsonException InvalidVersion(string message) => new(message, "$.SchemaVersion", null, null);
}
