using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Domain.Persistence;


namespace SunEyeVision.Persistence;

public static class DeclaredFieldJson
{
    public static IReadOnlyDictionary<string,ParameterSetting> Read(FrozenToolDeclarations definition,JsonElement fields,bool currentValues=false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if(fields.ValueKind != JsonValueKind.Object) throw new JsonException("Runtime fields must be an object.");
        CheckUniqueMembers(fields);
        var ports=(currentValues?definition.SettingsSchema.Inputs:definition.Schema.Inputs.Concat(definition.SettingsSchema.Inputs).Concat(definition.DisplaySchema.Inputs))
            .ToDictionary(port=>port.Id,StringComparer.Ordinal);
        var behaviors=definition.ReferenceBehaviors;
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
            else if(kind.GetString()=="DisplayExpression")
            {
                Exact(item,"Kind","Text","Slots");
                var slots=new List<DisplayExpressionSlot>();
                foreach(var slot in item.GetProperty("Slots").EnumerateArray())
                {
                    var token=slot.GetProperty("Token").GetString()??throw new JsonException("Expression token cannot be null.");
                    if(slot.TryGetProperty("Reference",out var source))
                    {Exact(slot,"Token","Reference","Format");slots.Add(new(token,SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Deserialize(source.GetRawText(),behavior),slot.GetProperty("Format").GetString()));}
                    else
                    {Exact(slot,"Token","SelfPort","SelfMembers","Format");slots.Add(new(token,new SunEyeVision.Plugin.Contracts.Results.Presentation.ResultFieldPath(
                        slot.GetProperty("SelfPort").GetString()!,slot.GetProperty("SelfMembers").EnumerateArray().Select(member=>member.GetString()!)),slot.GetProperty("Format").GetString()));}
                }
                setting=new DisplayExpressionSetting(item.GetProperty("Text").GetString()??throw new JsonException("Expression text cannot be null."),slots);
            }
            else if(kind.GetString()=="Reference")
            {
                Exact(item,"Kind","Reference","Conversion","OriginId","RebindingReason");var name=item.GetProperty("Conversion").GetString();
                if(!Enum.TryParse<BindingConversion>(name,false,out var conversion)||!Enum.IsDefined(conversion)||conversion.ToString()!=name)
                    throw new JsonException("Unknown binding conversion.");
                setting=new ReferenceSetting(SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Deserialize(item.GetProperty("Reference").GetRawText(),behavior),conversion,item.GetProperty("OriginId").GetString(),item.GetProperty("RebindingReason").GetString());
            }
            else if(kind.GetString()=="Variable")
            {
                Exact(item,"Kind","Reference","Conversion","RebindingReason");var name=item.GetProperty("Conversion").GetString();
                if(!Enum.TryParse<BindingConversion>(name,false,out var conversion)||!Enum.IsDefined(conversion)||conversion.ToString()!=name)
                    throw new JsonException("Unknown variable conversion.");
                setting=new VariableSetting(SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.DeserializeVariable(item.GetProperty("Reference").GetRawText(),behavior),conversion,item.GetProperty("RebindingReason").GetString());
            }
            else throw new JsonException("Persistent fields require Constant or Reference; Unset must be absent.");
            result.Add(property.Name,currentValues?setting:FieldSettings.Freeze(port,setting));
        }
        return result;
    }
    private static void CheckUniqueMembers(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in value.EnumerateObject())
            {
                if(!names.Add(property.Name))throw new JsonException("Duplicate Runtime setting: "+property.Name);
                CheckUniqueMembers(property.Value);
            }
        }
        else if(value.ValueKind==JsonValueKind.Array)
            foreach(var child in value.EnumerateArray())CheckUniqueMembers(child);
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
}
