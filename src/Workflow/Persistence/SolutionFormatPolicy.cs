using System.Text.Json;
using System.Text.Json.Nodes;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Disk format policy, independent from contract ABI, schema fingerprints and configuration revisions.</summary>
internal static class SolutionFormatPolicy
{
    public const int Current = 3;

    public static int ReadVersion(JsonNode root)
    {
        if (root is not JsonObject) throw InvalidVersion("A solution must be a JSON object.");
        if (!((JsonObject)root).TryGetPropertyValue("SchemaVersion", out var value)) throw InvalidVersion("A version 3 solution is required.");
        if (value is not JsonValue number || !number.TryGetValue<int>(out var version)
            || version != Current)
            throw InvalidVersion("Unsupported solution format; expected version 3.");
        return version;
    }

    public static void ValidateReadable(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw InvalidVersion("A solution must be a JSON object.");
        if (!root.TryGetProperty("SchemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != Current)
            throw InvalidVersion("Unsupported solution format; expected version 3.");
    }

    private static JsonException InvalidVersion(string message) => new(message, "$.SchemaVersion", null, null);
}
