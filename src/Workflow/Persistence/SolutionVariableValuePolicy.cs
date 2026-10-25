using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Candidate-only variable conversion. The caller supplies the existing SDK's canonical type authority.</summary>
internal static class SolutionVariableValuePolicy
{
    public static void Prepare(JsonNode variable, string path, bool legacy, Func<string, string> canonicalType,
        Action<string, string, string> report)
    {
        if (variable["Value"] is null) return;
        var wrapper = variable["Value"] as JsonObject
            ?? throw new JsonException("Value wrapper must be an object.", path + ".Value", null, null);
        var type = wrapper["Type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type) || !wrapper.ContainsKey("Value"))
            throw new JsonException("Invalid variable wrapper.", path + ".Value", null, null);
        foreach (var property in wrapper)
            if (property.Key is not ("Type" or "Value"))
                throw new JsonException("Unknown variable value field.", path + ".Value." + property.Key, null, null);
        var declared = variable["Type"]?.GetValue<string>()
            ?? throw new JsonException("Variable Type is required.", path + ".Type", null, null);
        var canonical = canonicalType(type);
        if (canonicalType(declared) != canonical)
            throw new JsonException("Variable Type disagrees with value Type.", path + ".Value.Type", null, null);
        var normalized = canonical switch
        {
            "System.Int64" => "Integer", "System.Double" => "Double",
            "System.String" => "String", "System.Boolean" => "Boolean", _ => type
        };
        if (!legacy && (normalized != type || normalized != declared))
            throw new JsonException("Legacy type aliases require explicit format 1 migration.", path + ".Type", null, null);
        if (legacy && (normalized != type || normalized != declared))
        {
            wrapper["Type"] = normalized;
            variable["Type"] = normalized;
            report("COMPAT_TYPE_ALIAS", path + ".Type", "Known legacy type alias normalized in candidate only.");
        }
        if (wrapper["Value"] is null)
            report("COMPAT_TYPED_NULL", path + ".Value", "Typed null retained as an empty value.");
        else if (normalized is "Integer" or "Double" or "String" or "Boolean" && wrapper["Value"] is not JsonArray)
        {
            if (!legacy) throw new JsonException("Format 2 values require the declared tuple representation.", path + ".Value.Value", null, null);
            wrapper["Value"] = new JsonArray(wrapper["Value"]!.DeepClone());
            report("COMPAT_SCALAR", path + ".Value.Value", "V1 scalar promoted to a single-element tuple in candidate only.");
        }
    }
}
