using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Candidate-only variable conversion. The caller supplies the existing SDK's canonical type authority.</summary>
internal static class SolutionVariableValuePolicy
{
    public static void Prepare(JsonNode variable, string path, Func<string, string> canonicalType,
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
        if(declared is "Integer[]" or "Double[]" or "String[]" or "Boolean[]")
        {
            var element=declared[..^2];
            if(type!=element||canonicalType(type)!=canonicalType(element))
                throw new JsonException("Explicit sequence declaration disagrees with the canonical value element type.",path+".Value.Type",null,null);
            if(wrapper["Value"] is not null&&wrapper["Value"] is not JsonArray)
                throw new JsonException("Explicit sequence variables require an array value; scalar promotion is forbidden.",path+".Value.Value",null,null);
            return;
        }
        var canonical = canonicalType(type);
        if (canonicalType(declared) != canonical)
            throw new JsonException("Variable Type disagrees with value Type.", path + ".Value.Type", null, null);
        var normalized = canonical switch
        {
            "System.Int64" => "Integer", "System.Double" => "Double",
            "System.String" => "String", "System.Boolean" => "Boolean", _ => type
        };
        if (normalized != type || normalized != declared)
            throw new JsonException("Variable type is not in the current canonical format.", path + ".Type", null, null);
        if (wrapper["Value"] is null)
            report("TYPED_NULL", path + ".Value", "Typed null retained as an empty value.");
        // Cardinality is part of the actual value. The converter validates scalar or
        // one-dimensional array contents; loading never promotes a scalar to a tuple.
    }
}
