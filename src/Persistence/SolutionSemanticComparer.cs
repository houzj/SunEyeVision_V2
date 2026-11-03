using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace SunEyeVision.Persistence;

/// <summary>Persistent content comparison: object order is irrelevant; array order is significant.</summary>
public static class SolutionSemanticComparer
{
    public static string Canonicalize(string json)
    {
        var root = JsonNode.Parse(json) ?? throw new System.Text.Json.JsonException("Empty solution snapshot.");
        return Sort(root)!.ToJsonString();
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new System.Collections.Generic.KeyValuePair<string, JsonNode?>(p.Key, Sort(p.Value)))),
        JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
        _ => node?.DeepClone()
    };

    public static bool Equivalent(string left, string right) => Canonicalize(left) == Canonicalize(right);
}
