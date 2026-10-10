using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

public sealed record CommunicationFieldSample(Guid Id, string Name, string DataType, object Value);
public sealed record CommunicationParameterAssignment(string NodeId, string Property, string Kind, object Value);
public sealed record CommunicationEqualityResult(bool Matches, object? TriggerValue, string? Error = null);

/// <summary>Pure evaluation: never mutates solution parameters or reads a UI/cache value.</summary>
public static class CommunicationTriggerEvaluation
{
    public static CommunicationEqualityResult EvaluateUpload(CommunicationTriggerDefinition rule, CommunicationUploadSnapshot sample)
    {
        if (rule.Source != "GlobalData") return new(false, null, "非当前全局数据规则格式");
        if (!sample.IsGood) return new(false, null, "上传数据质量无效");
        var type = ScalarType(sample.DataType);
        if (type == null || !type.IsInstanceOfType(sample.Value) || !IsFinite(sample.Value))
            return new(false, null, "上传数据不是有效标量，请先配置解析或字段提取");
        if (rule.MatchMode == "不检查内容") return new(true, sample.Value);
        if (string.IsNullOrEmpty(rule.Text)) return new(false, null, "请填写触发参数；空值不表示全局放行");
        if (rule.MatchMode == "包含")
            return sample.Value is string text ? new(text.Contains(rule.Text, StringComparison.Ordinal), sample.Value)
                : new(false, null, "包含仅适用于 String，不转换原生数值");
        if (rule.MatchMode is not ("等于" or "不等于")) return new(false, null, "未知匹配方式");
        if (!TryParse(rule.Text, type, out var expected)) return new(false, null, $"触发参数不能解析为 {sample.DataType}");
        var equal = Equals(sample.Value, expected);
        return new(rule.MatchMode == "不等于" ? !equal : equal, sample.Value);
    }
    public static CommunicationEqualityResult EvaluateEquality(CommunicationTriggerDefinition rule,
        Guid eventId, IReadOnlyList<Guid> outputIds, IReadOnlyDictionary<Guid, CommunicationFieldSample> fields)
    {
        var source = rule.TriggerParameterSource ?? (rule.FieldId is { } legacy ? $"{eventId:N}.{legacy:N}" : "");
        if (source.Length == 0) return new(true, null);
        object? expected = source;
        var parts = source.Split('.');
        if (parts.Length == 2 && Guid.TryParse(parts[0], out var sourceEvent) && Guid.TryParse(parts[1], out var sourceField))
        {
            if (sourceEvent != eventId || !fields.TryGetValue(sourceField, out var field))
                return new(false, null, "触发参数绑定已失效或本次值无效");
            expected = field.Value;
        }
        var id = rule.ComparisonOutputId ?? (outputIds.Count == 1 ? outputIds[0] : (Guid?)null);
        if (id == null || !outputIds.Contains(id.Value) || !fields.TryGetValue(id.Value, out var actual))
            return new(false, expected, "请选择有效判断输出；多输出事件不能默认取第一项");
        var type = ScalarType(actual.DataType);
        if (type == null || !type.IsInstanceOfType(actual.Value) || !IsFinite(actual.Value))
            return new(false, expected, "判断输出类型不支持或本次值无效");
        object? adapted;
        if (expected is string literal)
        {
            if (!TryParse(literal, type, out adapted))
                return new(false, expected, $"触发参数不能解析为 {actual.DataType}");
        }
        else if (expected != null && type.IsInstanceOfType(expected) && IsFinite(expected))
            adapted = expected;
        else if (expected != null && IsNumeric(expected.GetType()) && IsNumeric(type))
        {
            try
            {
                adapted = Convert.ChangeType(expected, type, CultureInfo.InvariantCulture);
                var restored = Convert.ChangeType(adapted, expected.GetType(), CultureInfo.InvariantCulture);
                if (!IsFinite(adapted!) || !Equals(restored, expected))
                    return new(false, expected, "绑定数值不能无损转换为判断输出类型");
            }
            catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException)
            { return new(false, expected, "绑定数值超出判断输出类型范围"); }
        }
        else return new(false, expected, "绑定值与判断输出类型不兼容");
        return new(Equals(actual.Value, adapted), adapted);
    }

    private static bool IsNumeric(Type type) => type == typeof(short) || type == typeof(ushort) ||
        type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(double);
    private static bool IsFinite(object value) => (value is not float f || float.IsFinite(f)) &&
        (value is not double d || double.IsFinite(d));

    public static Type? ScalarType(string type) => type.ToLowerInvariant() switch
    {
        "bool" or "boolean" => typeof(bool), "string" => typeof(string),
        "byte" => typeof(byte), "sbyte" => typeof(sbyte), "int64" => typeof(long), "uint64" => typeof(ulong),
        "int" or "int32" => typeof(int), "int16" => typeof(short), "uint16" => typeof(ushort),
        "uint32" => typeof(uint), "float" or "float32" => typeof(float), "double" => typeof(double), _ => null
    };

    public static bool TryParse(string text, Type type, out object? value)
    {
        value = null;
        try
        {
            if (type != typeof(string) && type != typeof(bool) && type != typeof(short) && type != typeof(ushort) &&
                type != typeof(int) && type != typeof(uint) && type != typeof(float) && type != typeof(double) &&
                type != typeof(byte) && type != typeof(sbyte) && type != typeof(long) && type != typeof(ulong)) return false;
            value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            return value is not float f || float.IsFinite(f)
                ? value is not double d || double.IsFinite(d) : false;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { return false; }
    }

    public static bool Matches(CommunicationTriggerDefinition rule, IReadOnlyDictionary<Guid, CommunicationFieldSample> fields)
    {
        if (rule.ConditionRule == "无附加条件") return true;
        if (rule.ConditionFieldId is not { } id || !fields.TryGetValue(id, out var field) ||
            !TryParse(rule.ConditionValue, field.Value.GetType(), out var expected)) return false;
        if (field.Value is string text)
            return rule.ConditionRule switch
            {
                "等于" => text == (string)expected!, "不等于" => text != (string)expected!,
                "包含" => text.Contains((string)expected!, StringComparison.Ordinal), _ => false
            };
        if (field.Value is bool b)
            return rule.ConditionRule switch { "等于" => b == (bool)expected!, "不等于" => b != (bool)expected!, _ => false };
        var comparison = ((IComparable)field.Value).CompareTo(expected);
        return rule.ConditionRule switch
        {
            "等于" => comparison == 0, "不等于" => comparison != 0, "大于" => comparison > 0,
            "小于" => comparison < 0, "大于等于" => comparison >= 0, "小于等于" => comparison <= 0, _ => false
        };
    }

    public static IReadOnlyList<CommunicationParameterAssignment> ResolveMappings(
        CommunicationTriggerDefinition rule, IReadOnlyDictionary<Guid, CommunicationFieldSample> fields)
    {
        var result = new List<CommunicationParameterAssignment>();
        if (rule.Command is not ("执行流程" or "执行模块") && (rule.ParameterMappings?.Count ?? 0) > 0)
            throw new InvalidOperationException("此命令不支持运行参数或模块输入绑定。");
        foreach (var mapping in rule.ParameterMappings ?? new())
        {
            if (!fields.TryGetValue(mapping.SourceFieldId, out var field))
                throw new InvalidOperationException("参数绑定的接收输出已失效。");
            if (mapping.TargetKind is not ("运行参数" or "模块输入") ||
                string.IsNullOrEmpty(mapping.TargetNodeId) || string.IsNullOrEmpty(mapping.TargetProperty))
                throw new InvalidOperationException("参数绑定目标无效。");
            if (result.Any(x => x.NodeId == mapping.TargetNodeId && x.Property == mapping.TargetProperty))
                throw new InvalidOperationException("同一目标参数不能重复绑定。");
            result.Add(new(mapping.TargetNodeId, mapping.TargetProperty, mapping.TargetKind, field.Value));
        }
        return result.AsReadOnly();
    }

    public static void ValidateTarget(WorkflowNodeBase node, CommunicationParameterAssignment assignment)
    {
        var property = node.Parameters.GetType().GetProperty(assignment.Property);
        var declared = assignment.Kind == "运行参数"
            ? node.Parameters is IRuntimeParameterContract runtime && runtime.GetRuntimeParameters().Any(p => p.Bindable && p.PropertyName == assignment.Property)
            : assignment.Kind == "模块输入" && node.Parameters is IInputSlotContract input && input.GetInputSlots().Any(p => p.Bindable && p.PropertyName == assignment.Property);
        if (!declared || property?.CanWrite != true || !property.PropertyType.IsInstanceOfType(assignment.Value))
            throw new InvalidOperationException($"参数绑定目标失效或类型不匹配：{node.Name}.{assignment.Property}。");
    }
}
