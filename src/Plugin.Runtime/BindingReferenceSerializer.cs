using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Versioned typed binding expression; no runtime values, result cache or editor objects are persisted.</summary>
public static class BindingReferenceSerializer
{
    private static readonly JsonSerializerOptions Options=CreateOptions();
    private sealed record Document([property:JsonRequired]int ContractMajor,[property:JsonRequired]int ContractMinor,[property:JsonRequired]OutputReference Reference);
    public static string Serialize(OutputReference reference)=>JsonSerializer.Serialize(new Document(FoundationContract.Major,FoundationContract.Minor,reference),Options);
    public static OutputReference Deserialize(string json)
        => DeserializeVersion(json, FoundationContract.Major, FoundationContract.Minor);

    /// <summary>Explicit in-memory legacy conversion. The document owner must resolve every old identity and declaration.
    /// This does not change the strict current-version reader and never writes a file.</summary>
    public static string MigrateVersion4(string json, Func<OutputReference, OutputReference> resolveLegacy)
    {
        ArgumentNullException.ThrowIfNull(resolveLegacy);
        var legacy = DeserializeVersion(json, 4, 0);
        var resolved = resolveLegacy(legacy) ?? throw new JsonException("Legacy binding has no approved mapping.");
        var current = Serialize(resolved);
        _ = Deserialize(current);
        return current;
    }

    private static OutputReference DeserializeVersion(string json, int major, int minor)
    {
        try
        {
        using (var parsed = JsonDocument.Parse(json)) RejectDuplicateProperties(parsed.RootElement);
        var document=JsonSerializer.Deserialize<Document>(json,Options)??throw new JsonException("Binding document is missing.");
        if(document.ContractMajor!=major||document.ContractMinor!=minor)throw new JsonException("Binding contract version must match the explicitly requested version; implicit migration is forbidden.");
        return document.Reference??throw new JsonException("Binding reference is missing.");
        }
        catch(Exception fault) when(fault is KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { throw new JsonException("Binding reference contains an invalid or missing declaration.",fault); }
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var options=new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new TypeConverter());options.Converters.Add(new ReferenceConverter());return options;
    }
    private static void RejectDuplicateProperties(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate binding property: " + property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (item.ValueKind == JsonValueKind.Array)
            foreach (var child in item.EnumerateArray()) RejectDuplicateProperties(child);
    }
    private static void RequireProperties(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new JsonException("Expected binding object.");
        var expected = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
            if (!expected.Remove(property.Name)) throw new JsonException("Unknown binding property: " + property.Name);
        if (expected.Count > 0) throw new JsonException("Missing binding property: " + expected.First());
    }
    private sealed class TypeConverter:JsonConverter<TypeRef>
    {
        public override TypeRef Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options)
        {
            using var document=JsonDocument.ParseValue(ref reader);var root=document.RootElement;
            TypeRef ReadType(JsonElement item)=>JsonSerializer.Deserialize<TypeRef>(item.GetRawText(),options)??throw new JsonException("Type declaration is missing.");
            NumericStorage? ReadStorage(JsonElement item)
            {
                var stored=item.GetProperty("storage");
                if(stored.ValueKind==JsonValueKind.Null)return null;
                return Enum.TryParse<NumericStorage>(stored.GetString(),out var result)&&Enum.IsDefined(result)?result:throw new JsonException("Unknown numeric storage constraint.");
            }
            T Kind<T>(string name) where T:struct,Enum
            {
                if(!Enum.TryParse<T>(root.GetProperty(name).GetString(),out var kind)||!Enum.IsDefined(kind))throw new JsonException("Unknown declaration kind.");
                return kind;
            }
            try
            {
                var shape = Kind<ValueShape>("shape");
                RequireProperties(root, shape switch
                {
                    ValueShape.Scalar => new[] { "shape", "scalar" },
                    ValueShape.Sequence => new[] { "shape", "element", "minimum", "maximum" },
                    ValueShape.Record => new[] { "shape", "recordId", "fields" },
                    ValueShape.Geometry => new[] { "shape", "geometry", "frameId", "domain", "unit" },
                    ValueShape.Resource => new[] { "shape", "resource" },
                    _ => throw new JsonException("Unknown declaration shape.")
                });
                if (shape == ValueShape.Record)
                    foreach (var field in root.GetProperty("fields").EnumerateArray())
                        RequireProperties(field, "id", "type", "required", "nullable", "storage");
                return shape switch
                {
                    ValueShape.Scalar=>TypeRef.ScalarOf(Kind<ScalarKind>("scalar")),
                    ValueShape.Sequence=>TypeRef.SequenceOf(ReadType(root.GetProperty("element")),new(root.GetProperty("minimum").GetInt32(),root.GetProperty("maximum").ValueKind==JsonValueKind.Null?null:root.GetProperty("maximum").GetInt32())),
                    ValueShape.Record=>TypeRef.RecordOf(root.GetProperty("recordId").GetString()!,root.GetProperty("fields").EnumerateArray().Select(field=>new Port(field.GetProperty("id").GetString()!,ReadType(field.GetProperty("type")),field.GetProperty("required").GetBoolean(),field.GetProperty("nullable").GetBoolean(),ReadStorage(field))).ToArray()),
                    ValueShape.Geometry=>TypeRef.GeometryOf(Kind<GeometryKind>("geometry"),new CoordinateFrame(root.GetProperty("frameId").GetString()!,Kind<CoordinateDomain>("domain"),Kind<CoordinateUnit>("unit"))),
                    ValueShape.Resource=>TypeRef.ResourceOf(Kind<ResourceKind>("resource")),
                    _=>throw new JsonException("Unsupported declaration shape.")
                };
            }
            catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            {throw new JsonException("Invalid binding type declaration.",fault);}
        }
        public override void Write(Utf8JsonWriter writer,TypeRef value,JsonSerializerOptions options)
        {
            writer.WriteStartObject();writer.WriteString("shape",value.Shape.ToString());
            switch(value.Shape)
            {
                case ValueShape.Scalar:writer.WriteString("scalar",value.Scalar!.Value.ToString());break;
                case ValueShape.Sequence:
                    writer.WritePropertyName("element");JsonSerializer.Serialize(writer,value.Element,options);writer.WriteNumber("minimum",value.Count.Minimum);
                    if(value.Count.Maximum is int maximum)writer.WriteNumber("maximum",maximum);else writer.WriteNull("maximum");break;
                case ValueShape.Record:
                    writer.WriteString("recordId",value.RecordId);writer.WriteStartArray("fields");
                    foreach(var field in value.Fields)
                    {writer.WriteStartObject();writer.WriteString("id",field.Id);writer.WritePropertyName("type");JsonSerializer.Serialize(writer,field.Type,options);writer.WriteBoolean("required",field.Required);writer.WriteBoolean("nullable",field.Nullable);if(field.Storage is NumericStorage storage)writer.WriteString("storage",storage.ToString());else writer.WriteNull("storage");writer.WriteEndObject();}
                    writer.WriteEndArray();break;
                case ValueShape.Geometry:
                    writer.WriteString("geometry",value.Geometry!.Value.ToString());writer.WriteString("frameId",value.Frame!.Id);writer.WriteString("domain",value.Frame.Domain.ToString());writer.WriteString("unit",value.Frame.Unit.ToString());break;
                case ValueShape.Resource:writer.WriteString("resource",value.Resource!.Value.ToString());break;
            }
            writer.WriteEndObject();
        }
    }
    private sealed class ReferenceConverter:JsonConverter<OutputReference>
    {
        public override OutputReference Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options)
        {
            using var document=JsonDocument.ParseValue(ref reader);var root=document.RootElement;
            RequireProperties(root, "nodeId", "fieldId", "scopeId", "storage", "expectedType", "memberFieldPath", "selection");
            var type=JsonSerializer.Deserialize<TypeRef>(root.GetProperty("expectedType").GetRawText(),options)??throw new JsonException("Expected declaration is missing.");
            var selection=root.GetProperty("selection");
            RequireProperties(selection, selection.GetProperty("kind").GetString() switch
            {
                "All" => new[] { "kind" },
                "AtConstant" => new[] { "kind", "index" },
                "AtField" => new[] { "kind", "reference" },
                _ => throw new JsonException("Unknown binding selection.")
            });
            BindingSelection selected=selection.GetProperty("kind").GetString() switch
            {
                "All"=>BindingSelection.All,
                "AtConstant"=>new BindingSelection.AtConstant(selection.GetProperty("index").GetInt32()),
                "AtField"=>new BindingSelection.AtField(JsonSerializer.Deserialize<OutputReference>(selection.GetProperty("reference").GetRawText(),options)??throw new JsonException("Index reference is missing.")),
                _=>throw new JsonException("Unknown binding selection.")
            };
            return new(root.GetProperty("nodeId").GetString()!,root.GetProperty("fieldId").GetString()!,type,
                root.GetProperty("memberFieldPath").EnumerateArray().Select(item=>item.GetString()!).ToArray(),selected,
                root.GetProperty("scopeId").ValueKind==JsonValueKind.Null?null:root.GetProperty("scopeId").GetString(),
                root.GetProperty("storage").ValueKind==JsonValueKind.Null?null:
                    Enum.TryParse<NumericStorage>(root.GetProperty("storage").GetString(),out var storage)&&Enum.IsDefined(storage)?storage:throw new JsonException("Invalid numeric storage constraint."));
        }
        public override void Write(Utf8JsonWriter writer,OutputReference value,JsonSerializerOptions options)
        {
            writer.WriteStartObject();writer.WriteString("nodeId",value.NodeId);writer.WriteString("fieldId",value.PortId);writer.WriteString("scopeId",value.ScopeId);
            if(value.Storage is NumericStorage storage)writer.WriteString("storage",storage.ToString());else writer.WriteNull("storage");
            writer.WritePropertyName("expectedType");JsonSerializer.Serialize(writer,value.Type,options);
            writer.WriteStartArray("memberFieldPath");foreach(var member in value.MemberFieldPath)writer.WriteStringValue(member);writer.WriteEndArray();
            writer.WriteStartObject("selection");
            switch(value.Selection)
            {
                case BindingSelection.AllItems:writer.WriteString("kind","All");break;
                case BindingSelection.AtConstant index:writer.WriteString("kind","AtConstant");writer.WriteNumber("index",index.Index);break;
                case BindingSelection.AtField index:writer.WriteString("kind","AtField");writer.WritePropertyName("reference");JsonSerializer.Serialize(writer,index.Reference,options);break;
                default:throw new JsonException("Unknown binding selection.");
            }
            writer.WriteEndObject();writer.WriteEndObject();
        }
    }
}
