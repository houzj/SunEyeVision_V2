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
    {
        var document=JsonSerializer.Deserialize<Document>(json,Options)??throw new JsonException("Binding document is missing.");
        if(document.ContractMajor!=FoundationContract.Major||document.ContractMinor!=FoundationContract.Minor)throw new JsonException("Binding contract version must match the current major and minor; old documents are not migrated.");
        return document.Reference??throw new JsonException("Binding reference is missing.");
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var options=new JsonSerializerOptions();options.Converters.Add(new TypeConverter());options.Converters.Add(new ReferenceConverter());return options;
    }
    private sealed class TypeConverter:JsonConverter<TypeRef>
    {
        public override TypeRef Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options)
        {
            using var document=JsonDocument.ParseValue(ref reader);var root=document.RootElement;
            TypeRef ReadType(JsonElement item)=>JsonSerializer.Deserialize<TypeRef>(item.GetRawText(),options)??throw new JsonException("Type declaration is missing.");
            T Kind<T>(string name) where T:struct,Enum
            {
                if(!Enum.TryParse<T>(root.GetProperty(name).GetString(),out var kind)||!Enum.IsDefined(kind))throw new JsonException("Unknown declaration kind.");
                return kind;
            }
            try
            {
                return Kind<ValueShape>("shape") switch
                {
                    ValueShape.Scalar=>TypeRef.ScalarOf(Kind<ScalarKind>("scalar")),
                    ValueShape.Sequence=>TypeRef.SequenceOf(ReadType(root.GetProperty("element")),new(root.GetProperty("minimum").GetInt32(),root.GetProperty("maximum").ValueKind==JsonValueKind.Null?null:root.GetProperty("maximum").GetInt32())),
                    ValueShape.Record=>TypeRef.RecordOf(root.GetProperty("recordId").GetString()!,root.GetProperty("fields").EnumerateArray().Select(field=>new Port(field.GetProperty("id").GetString()!,ReadType(field.GetProperty("type")),field.GetProperty("required").GetBoolean(),field.GetProperty("nullable").GetBoolean())).ToArray()),
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
                    {writer.WriteStartObject();writer.WriteString("id",field.Id);writer.WritePropertyName("type");JsonSerializer.Serialize(writer,field.Type,options);writer.WriteBoolean("required",field.Required);writer.WriteBoolean("nullable",field.Nullable);writer.WriteEndObject();}
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
            var type=JsonSerializer.Deserialize<TypeRef>(root.GetProperty("expectedType").GetRawText(),options)??throw new JsonException("Expected declaration is missing.");
            var selection=root.GetProperty("selection");
            BindingSelection selected=selection.GetProperty("kind").GetString() switch
            {
                "All"=>BindingSelection.All,
                "AtConstant"=>new BindingSelection.AtConstant(selection.GetProperty("index").GetInt32()),
                "AtField"=>new BindingSelection.AtField(JsonSerializer.Deserialize<OutputReference>(selection.GetProperty("reference").GetRawText(),options)??throw new JsonException("Index reference is missing.")),
                _=>throw new JsonException("Unknown binding selection.")
            };
            return new(root.GetProperty("nodeId").GetString()!,root.GetProperty("fieldId").GetString()!,type,
                root.GetProperty("memberFieldPath").EnumerateArray().Select(item=>item.GetString()!).ToArray(),selected,
                root.GetProperty("scopeId").ValueKind==JsonValueKind.Null?null:root.GetProperty("scopeId").GetString());
        }
        public override void Write(Utf8JsonWriter writer,OutputReference value,JsonSerializerOptions options)
        {
            writer.WriteStartObject();writer.WriteString("nodeId",value.NodeId);writer.WriteString("fieldId",value.PortId);writer.WriteString("scopeId",value.ScopeId);
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
