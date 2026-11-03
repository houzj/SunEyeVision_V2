using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Persistence.Variables;

/// <summary>Property-specific value wrapper; the host supplies complex value codecs explicitly.</summary>
public sealed class GlobalVariableValueConverter : JsonConverter<object?>
{
    private readonly IVariableValueExtensionCodec? _extension;
    public GlobalVariableValueConverter(IVariableValueExtensionCodec? extension = null) => _extension = extension;
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Type",out var type) || type.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(type.GetString()))
            throw new JsonException("Variable wrapper requires a non-empty Type.");
        if (root.EnumerateObject().Any(p => p.Name is not ("Type" or "Value"))) throw new JsonException("Unknown variable value wrapper field.");
        if (!root.TryGetProperty("Value",out var value)) throw new JsonException("Variable wrapper requires Value.");
        var name = type.GetString()!;
        var pure = PureFamily(name);
        if (pure is null && !(_extension?.SupportsType(name) ?? false)) throw new JsonException("Unknown or unavailable variable type: " + name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        try
        {
            return pure switch
            {
                "Integer" => Parse(value, e => e.GetInt64()),
                "Double" => Parse(value, e => e.GetDouble()),
                "String" => Parse(value, e => e.GetString()!),
                "Boolean" => Parse(value, e => e.GetBoolean()),
                _ => _extension!.Read(name,value,options)
            };
        }
        catch (Exception ex) when (ex is not JsonException) { throw new JsonException("Invalid variable value for " + name,ex); }
    }
    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        string name; object? serialized;
        switch (value)
        {
            case int x: name="Integer"; serialized=(long)x; break;
            case long x: name="Integer"; serialized=x; break;
            case int[] x: name="Integer"; serialized=x.Select(Convert.ToInt64).ToArray(); break;
            case long[] x: name="Integer"; serialized=x; break;
            case float x: name="Double"; serialized=(double)x; break;
            case double x: name="Double"; serialized=x; break;
            case float[] x: name="Double"; serialized=x.Select(Convert.ToDouble).ToArray(); break;
            case double[] x: name="Double"; serialized=x; break;
            case string x: name="String"; serialized=x; break;
            case string[] x: name="String"; serialized=x; break;
            case bool x: name="Boolean"; serialized=x; break;
            case bool[] x: name="Boolean"; serialized=x; break;
            default:
                if (_extension is null || !_extension.TryGetToken(value,out name))
                    throw new NotSupportedException("Variable value has no available persistence codec: " + value.GetType().FullName);
                writer.WriteStartObject(); writer.WriteString("Type",name); writer.WritePropertyName("Value");
                _extension.WriteValue(writer,name,value,options); writer.WriteEndObject(); return;
        }
        writer.WriteStartObject(); writer.WriteString("Type",name); writer.WritePropertyName("Value");
        if (serialized is null) writer.WriteNullValue(); else JsonSerializer.Serialize(writer,serialized,serialized.GetType(),options);
        writer.WriteEndObject();
    }
    private static object Parse<T>(JsonElement value, Func<JsonElement,T> convert) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(convert).ToArray() : convert(value)!;
    private static string? PureFamily(string name)
    {
        // The declared variable type vocabulary has one authority; Float carriers remain
        // unsupported by the existing persisted reader, and Boolean[] is not a V3 type tag.
        if (name.Equals("Boolean[]", StringComparison.OrdinalIgnoreCase) ||
            !VariableValueDeclarations.TryGetPort(name, out var port) || port.Storage == NumericStorage.Float32)
            return null;
        var type = port.Type.Shape == ValueShape.Sequence ? port.Type.Element! : port.Type;
        return type.Scalar switch
        {
            ScalarKind.Int64 => "Integer",
            ScalarKind.Float64 => "Double",
            ScalarKind.Text => "String",
            ScalarKind.Boolean => "Boolean",
            _ => null
        };
    }
}


