using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Workflow;

internal static class RuntimeFieldJson
{
    internal static IReadOnlyDictionary<string,ParameterSetting> Read(string toolId,JsonElement fields,bool currentValues=false)
    {
        var definition=ToolRegistry.CreateRuntime(toolId);
        var declared=definition as IToolSettingsFields??throw new JsonException("Runtime settings are not declared.");
        var ports=(currentValues?declared.SettingsSchema.Inputs:definition.Describe().Inputs.Concat(declared.SettingsSchema.Inputs))
            .ToDictionary(port=>port.Id,StringComparer.Ordinal);
        // Tool identity and declaration are available before the first binding is decoded.
        var behaviors=ParameterReferenceRules.Freeze(definition);
        var result=new Dictionary<string,ParameterSetting>(StringComparer.Ordinal);
        foreach(var property in fields.EnumerateObject())
        {
            if(!ports.TryGetValue(property.Name,out var port))throw new JsonException("Unknown Runtime field: "+property.Name);
            var behavior=behaviors.GetValueOrDefault(property.Name,ParameterReferenceBehavior.StrictExactlyOne);
            var item=property.Value;
            if(item.ValueKind!=JsonValueKind.Object||!item.TryGetProperty("Kind",out var kind))throw new JsonException("Runtime setting requires Kind.");
            if(currentValues&&kind.GetString()!="Constant")throw new JsonException("Current parameter values require pure constant values.");
            ParameterSetting setting;
            if(kind.GetString()=="Constant")
            {Exact(item,"Kind","Value");setting=new ConstantSetting(ReadValue(port,item.GetProperty("Value")));}
            else if(kind.GetString()=="Reference")
            {
                Exact(item,"Kind","Reference","Conversion","OriginId","RebindingReason");var name=item.GetProperty("Conversion").GetString();
                if(!Enum.TryParse<BindingConversion>(name,false,out var conversion)||!Enum.IsDefined(conversion)||conversion.ToString()!=name)
                    throw new JsonException("Unknown binding conversion.");
                setting=new ReferenceSetting(BindingReferenceSerializer.Deserialize(item.GetProperty("Reference").GetRawText(),behavior),conversion,item.GetProperty("OriginId").GetString(),item.GetProperty("RebindingReason").GetString());
            }
            else if(kind.GetString()=="Variable")
            {
                Exact(item,"Kind","Reference","Conversion","RebindingReason");var name=item.GetProperty("Conversion").GetString();
                if(!Enum.TryParse<BindingConversion>(name,false,out var conversion)||!Enum.IsDefined(conversion)||conversion.ToString()!=name)
                    throw new JsonException("Unknown variable conversion.");
                setting=new VariableSetting(BindingReferenceSerializer.DeserializeVariable(item.GetProperty("Reference").GetRawText(),behavior),conversion,item.GetProperty("RebindingReason").GetString());
            }
            else throw new JsonException("Persistent fields require Constant or Reference; Unset must be absent.");
            result.Add(property.Name,currentValues?setting:FieldSettings.Freeze(port,setting));
        }
        return result;
    }
    private static void Exact(JsonElement value,params string[] names)
    {
        if(!value.EnumerateObject().Select(property=>property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names))
            throw new JsonException("Runtime setting requires exactly: "+string.Join(",",names));
    }
    private static object? ReadValue(Port port,JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Null)return ValueRules.Freeze(port,null);
        object? result;
        if(port.Type.Shape==ValueShape.Record)
        {
            if(value.ValueKind!=JsonValueKind.Object)throw new JsonException("Record constant must be an object.");
            var ports=port.Type.Fields.ToDictionary(field=>field.Id,StringComparer.Ordinal);var members=new Dictionary<string,object?>();
            foreach(var item in value.EnumerateObject())
            {if(!ports.TryGetValue(item.Name,out var child))throw new JsonException("Unknown record member.");members.Add(item.Name,ReadValue(child,item.Value));}
            result=new RecordValue(port.Type,members);
        }
        else if(port.Type.Shape==ValueShape.Sequence)
        {
            if(value.ValueKind!=JsonValueKind.Array)throw new JsonException("Sequence constant must be an array.");
            result=new SequenceValue(port.Type,value.EnumerateArray().Select(item=>ReadValue(new Port("element",port.Type.Element!),item)));
        }
        else if(port.Type.Shape==ValueShape.Scalar)
            result=port.Type.Scalar switch
            {
                ScalarKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False=>value.GetBoolean(),
                ScalarKind.Text when value.ValueKind==JsonValueKind.String=>value.GetString(),
                ScalarKind.Int64 when value.ValueKind==JsonValueKind.Number=>value.GetInt64(),
                ScalarKind.Float64 when value.ValueKind==JsonValueKind.Number=>value.GetDouble(),
                _=>throw new JsonException("JSON constant differs from its declaration.")
            };
        else throw new JsonException("Invocation resources and unsupported values cannot be configuration constants.");
        return ValueRules.Freeze(port,result);
    }
    internal static void Write(Utf8JsonWriter writer,IReadOnlyDictionary<string,ParameterSetting> fields)
    {
        writer.WriteStartObject();
        foreach(var (id,setting) in fields.OrderBy(pair=>pair.Key,StringComparer.Ordinal))
        {
            writer.WritePropertyName(id);writer.WriteStartObject();
            if(setting is ConstantSetting constant){writer.WriteString("Kind","Constant");writer.WritePropertyName("Value");WriteValue(writer,constant.Value);}
            else if(setting is ReferenceSetting reference)
            {writer.WriteString("Kind","Reference");writer.WritePropertyName("Reference");writer.WriteRawValue(BindingReferenceSerializer.Serialize(reference.Reference));writer.WriteString("Conversion",reference.Conversion.ToString());writer.WriteString("OriginId",reference.OriginId);writer.WriteString("RebindingReason",reference.RebindingReason);}
            else if(setting is VariableSetting variable)
            {writer.WriteString("Kind","Variable");writer.WritePropertyName("Reference");writer.WriteRawValue(BindingReferenceSerializer.Serialize(variable.Reference));writer.WriteString("Conversion",variable.Conversion.ToString());writer.WriteString("RebindingReason",variable.RebindingReason);}
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
