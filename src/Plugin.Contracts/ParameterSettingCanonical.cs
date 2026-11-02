using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>Versioned typed binding expression; no runtime values, result cache or editor objects are persisted.</summary>
public static class ParameterSettingCanonical
{
    private static readonly JsonSerializerOptions Options=CreateOptions();
    private static readonly JsonSerializerOptions FirstItemOptions=CreateOptions(ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback);
    private static JsonSerializerOptions OptionsFor(ParameterReferenceBehavior behavior)=>behavior switch
    {ParameterReferenceBehavior.StrictExactlyOne=>Options,ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback=>FirstItemOptions,_=>throw new ArgumentException("Unknown reference behavior.")};
    private sealed record Document([property:JsonRequired]int ContractMajor,[property:JsonRequired]int ContractMinor,[property:JsonRequired]OutputReference Reference);
    private sealed record VariableDocument([property:JsonRequired]int ContractMajor,[property:JsonRequired]int ContractMinor,
        [property:JsonRequired]string SolutionId,[property:JsonRequired]string VariableId,[property:JsonRequired]TypeRef ExpectedType,[property:JsonRequired]string? Storage,
        [property:JsonRequired]string Selection,[property:JsonRequired]int? Index,[property:JsonRequired]OutputReference? IndexReference,
        [property:JsonRequired]VariableDocument? IndexVariable);
    public static string Serialize(VariableReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return JsonSerializer.Serialize(ToVariableDocument(reference),Options);
    }
    private static VariableDocument ToVariableDocument(VariableReference reference)
    {
        var kind=reference.Selection switch{BindingSelection.AllItems=>"All",BindingSelection.AtConstant=>"AtConstant",BindingSelection.AtField=>"AtField",BindingSelection.AtVariable=>"AtVariable",_=>throw new ArgumentException("Unknown variable selection.")};
        return new(FoundationContract.Major,FoundationContract.Minor,reference.SolutionId,reference.VariableId,reference.Type,
            reference.Storage?.ToString(),kind,(reference.Selection as BindingSelection.AtConstant)?.Index,(reference.Selection as BindingSelection.AtField)?.Reference,
            reference.Selection is BindingSelection.AtVariable variable?ToVariableDocument(variable.Reference):null);
    }
    public static VariableReference DeserializeVariable(string json,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
    {
        try
        {
            using(var parsed=JsonDocument.Parse(json))RejectDuplicateProperties(parsed.RootElement);
            var document=JsonSerializer.Deserialize<VariableDocument>(json,OptionsFor(behavior))??throw new JsonException("Variable reference is missing.");
            return FromVariableDocument(document,behavior);
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {throw new JsonException("Variable reference contains an invalid declaration.",error);}
    }
    private static VariableReference FromVariableDocument(VariableDocument document,ParameterReferenceBehavior behavior)
    {
            if(document.ContractMajor!=FoundationContract.Major||document.ContractMinor!=FoundationContract.Minor)
                throw new JsonException("Variable binding contract version differs.");
            NumericStorage? storage=document.Storage is null?null:
                Enum.TryParse<NumericStorage>(document.Storage,false,out var stored)&&Enum.IsDefined(stored)&&stored.ToString()==document.Storage
                    ?stored:throw new JsonException("Variable storage declaration is invalid.");
            BindingSelection selection=document.Selection switch {
                "All" when document.Index is null&&document.IndexReference is null&&document.IndexVariable is null=>BindingSelection.All,
                "AtConstant" when document.Index is {} index&&document.IndexReference is null&&document.IndexVariable is null=>new BindingSelection.AtConstant(index),
                "AtField" when document.Index is null&&document.IndexReference is {} field&&document.IndexVariable is null=>new BindingSelection.AtField(field,behavior),
                "AtVariable" when document.Index is null&&document.IndexReference is null&&document.IndexVariable is {} variable=>new BindingSelection.AtVariable(FromVariableDocument(variable,behavior),behavior),
                _=>throw new JsonException("Variable selection fields differ from their declared kind.")};
            return new(document.SolutionId,document.VariableId,document.ExpectedType,selection,storage);
    }
    public static string Serialize(OutputReference reference)=>JsonSerializer.Serialize(new Document(FoundationContract.Major,FoundationContract.Minor,reference),Options);
    public static OutputReference Deserialize(string json,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
        => DeserializeVersion(json, FoundationContract.Major, FoundationContract.Minor,behavior);

    private static OutputReference DeserializeVersion(string json, int major, int minor,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
    {
        try
        {
        using (var parsed = JsonDocument.Parse(json)) RejectDuplicateProperties(parsed.RootElement);
        var document=JsonSerializer.Deserialize<Document>(json,OptionsFor(behavior))??throw new JsonException("Binding document is missing.");
        if(document.ContractMajor!=major||document.ContractMinor!=minor)throw new JsonException("Binding contract version must match the explicitly requested version; implicit migration is forbidden.");
        return document.Reference??throw new JsonException("Binding reference is missing.");
        }
        catch(Exception fault) when(fault is KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { throw new JsonException("Binding reference contains an invalid or missing declaration.",fault); }
    }
    private static JsonSerializerOptions CreateOptions(ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
    {
        var options=new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new TypeConverter());options.Converters.Add(new ReferenceConverter(behavior));return options;
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
    private sealed class ReferenceConverter(ParameterReferenceBehavior behavior):JsonConverter<OutputReference>
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
                "AtVariable" => new[] { "kind", "variable" },
                _ => throw new JsonException("Unknown binding selection.")
            });
            BindingSelection selected=selection.GetProperty("kind").GetString() switch
            {
                "All"=>BindingSelection.All,
                "AtConstant"=>new BindingSelection.AtConstant(selection.GetProperty("index").GetInt32()),
                "AtField"=>new BindingSelection.AtField(JsonSerializer.Deserialize<OutputReference>(selection.GetProperty("reference").GetRawText(),options)??throw new JsonException("Index reference is missing."),behavior),
                "AtVariable"=>new BindingSelection.AtVariable(DeserializeVariable(selection.GetProperty("variable").GetRawText(),behavior),behavior),
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
                case BindingSelection.AtVariable index:writer.WriteString("kind","AtVariable");writer.WritePropertyName("variable");writer.WriteRawValue(Serialize(index.Reference));break;
                default:throw new JsonException("Unknown binding selection.");
            }
            writer.WriteEndObject();writer.WriteEndObject();
        }
    }

    public static void Write(Utf8JsonWriter writer,IReadOnlyDictionary<string,ParameterSetting> fields)
    {
        writer.WriteStartObject();
        foreach(var (id,setting) in fields.OrderBy(pair=>pair.Key,StringComparer.Ordinal))
        {
            writer.WritePropertyName(id);writer.WriteStartObject();
            if(setting is ConstantSetting constant){writer.WriteString("Kind","Constant");writer.WritePropertyName("Value");WriteValue(writer,constant.Value);}
            else if(setting is DisplayExpressionSetting expression)
            {
                writer.WriteString("Kind","DisplayExpression");writer.WriteString("Text",expression.Text);writer.WriteStartArray("Slots");
                foreach(var slot in expression.Slots)
                {
                    writer.WriteStartObject();writer.WriteString("Token",slot.Token);writer.WriteString("Format",slot.Format);
                    if(slot.Reference is {} source){writer.WritePropertyName("Reference");writer.WriteRawValue(Serialize(source));}
                    else{writer.WriteString("SelfPort",slot.Self!.PortId);writer.WriteStartArray("SelfMembers");foreach(var member in slot.Self.MemberFieldPath)writer.WriteStringValue(member);writer.WriteEndArray();}
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            else if(setting is ReferenceSetting reference)
            {writer.WriteString("Kind","Reference");writer.WritePropertyName("Reference");writer.WriteRawValue(Serialize(reference.Reference));writer.WriteString("Conversion",reference.Conversion.ToString());writer.WriteString("OriginId",reference.OriginId);writer.WriteString("RebindingReason",reference.RebindingReason);}
            else if(setting is VariableSetting variable)
            {writer.WriteString("Kind","Variable");writer.WritePropertyName("Reference");writer.WriteRawValue(Serialize(variable.Reference));writer.WriteString("Conversion",variable.Conversion.ToString());writer.WriteString("RebindingReason",variable.RebindingReason);}
            else throw new JsonException("Persistent Unset must be absent.");
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
    private static void WriteValue(Utf8JsonWriter writer,object? value)
    {
        if(value is null){writer.WriteNullValue();return;}
        if(value is RecordValue record)
        {writer.WriteStartObject();foreach(var item in record.CopyFields()){writer.WritePropertyName(item.Key);WriteValue(writer,item.Value);}writer.WriteEndObject();return;}
        if(value is SequenceValue sequence){writer.WriteStartArray();foreach(var item in sequence)WriteValue(writer,item);writer.WriteEndArray();return;}
        if(value is double[] array){writer.WriteStartArray();foreach(var item in array)writer.WriteNumberValue(item);writer.WriteEndArray();return;}
        switch(value)
        {case bool flag:writer.WriteBooleanValue(flag);break;case string text:writer.WriteStringValue(text);break;case long integer:writer.WriteNumberValue(integer);break;case double number when double.IsFinite(number):writer.WriteNumberValue(number);break;default:throw new JsonException("Unsupported configuration constant.");}
    }
}
