using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public sealed record GroupParameterChoice(object Value,string DisplayName)
{ public override string ToString()=>DisplayName; }
public sealed record GroupParameterDescriptor(string PropertyName, string DisplayName, Type ValueType,
    IReadOnlyList<GroupParameterChoice>? Choices = null);
/// <summary>Additional exposure declarations owned by a tool, including enum and complex editor parameters.</summary>
public interface IGroupParameterContract
{
    IReadOnlyList<GroupParameterDescriptor> GetGroupParameters();
}
public static class GroupParameterCatalog
{
    public static IReadOnlyList<GroupParameterDescriptor> Describe(ToolParameters parameters)
    {
        var declared = new List<GroupParameterDescriptor>();
        if (parameters is IRuntimeParameterContract runtime)
            declared.AddRange(runtime.GetRuntimeParameters().Where(d => CanExpose(parameters, d.PropertyName)).Select(d => DescribeProperty(parameters, d.PropertyName, d.DisplayName, d.ValueType)));
        if (parameters is IInputSlotContract inputs)
            declared.AddRange(inputs.GetInputSlots().Where(d => CanExpose(parameters, d.PropertyName)).Select(d => DescribeProperty(parameters, d.PropertyName, d.DisplayName, d.ValueType)));
        if (parameters is IGroupParameterContract group) declared.AddRange(group.GetGroupParameters());
        var result = new List<GroupParameterDescriptor>();
        foreach (var item in declared)
        {
            var verified = DescribeProperty(parameters, item.PropertyName, item.DisplayName, item.ValueType) with { Choices=item.Choices };
            if(item.Choices is { } choices && (choices.Count==0 || choices.Any(c=>c is null || c.Value is null || !item.ValueType.IsInstanceOfType(c.Value) || string.IsNullOrWhiteSpace(c.DisplayName)) ||
                choices.Select(c=>c.Value).Distinct().Count()!=choices.Count))throw new InvalidOperationException("Group参数选项声明无效。");
            var old = result.SingleOrDefault(d => d.PropertyName == verified.PropertyName);
            if (old is not null && old.ValueType != verified.ValueType) throw new InvalidOperationException("Group参数声明类型冲突。");
            if (old is null) result.Add(verified);
            else if(item.Choices is not null)result[result.IndexOf(old)]=verified;
        }
        return result;
    }
    public static void ValidateDeclaredChoices(ToolParameters parameters)
    {
        foreach(var descriptor in Describe(parameters).Where(d=>d.Choices is not null))
        {
            var value=parameters.GetType().GetProperty(descriptor.PropertyName)!.GetValue(parameters);
            if(!descriptor.Choices!.Any(c=>Equals(c.Value,value)))throw new InvalidOperationException(descriptor.DisplayName+"不是工具声明的选项。");
        }
    }
    private static bool CanExpose(ToolParameters parameters, string key)
    {
        var p = parameters.GetType().GetProperty(key);
        return p?.CanRead == true && p.SetMethod?.IsPublic == true && p.GetCustomAttribute<JsonIgnoreAttribute>() is null;
    }
    private static GroupParameterDescriptor DescribeProperty(ToolParameters parameters, string key, string display, Type? type)
    {
        var property = parameters.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanRead || property.SetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0 ||
            property.GetCustomAttribute<JsonIgnoreAttribute>() is not null || type is not null && type != property.PropertyType)
            throw new InvalidOperationException($"Group参数声明与持久化CLR属性不一致：{key}");
        return new(key, display, property.PropertyType);
    }
}
