using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public sealed record ParameterContractViolation(string Code, string Message);

public static class ParameterContractValidator
{
    public static IReadOnlyList<ParameterContractViolation> Validate(
        ToolParameters parameters,
        bool strictComplete = false)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var violations = new List<ParameterContractViolation>();
        var runtime = parameters is IRuntimeParameterContract runtimeContract
            ? runtimeContract.GetRuntimeParameters() ?? Array.Empty<RuntimeParameterDescriptor>()
            : Array.Empty<RuntimeParameterDescriptor>();
        var inputs = parameters is IInputSlotContract inputContract
            ? inputContract.GetInputSlots() ?? Array.Empty<InputSlotDescriptor>()
            : Array.Empty<InputSlotDescriptor>();

        ValidateNames(runtime.Select(x => (x.PropertyName, x.DisplayName, "RuntimeParameter")), violations);
        ValidateNames(inputs.Select(x => (x.PropertyName, x.DisplayName, "InputSlot")), violations);

        var properties = parameters.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var descriptor in runtime)
            ValidateProperty(descriptor.PropertyName, descriptor.ValueType, descriptor.TypeVocabulary,
                properties, "RuntimeParameter", violations);
        foreach (var descriptor in inputs)
            ValidateProperty(descriptor.PropertyName, descriptor.ValueType, descriptor.TypeVocabulary,
                properties, "InputSlot", violations);

        var overlap = runtime.Select(x => x.PropertyName)
            .Intersect(inputs.Select(x => x.PropertyName), StringComparer.Ordinal);
        foreach (var name in overlap)
            violations.Add(new("PARAM_ROLE_OVERLAP",
                $"属性 '{name}' 同时声明为运行参数和输入槽位，必须明确唯一角色。"));

        if (strictComplete && (parameters is IRuntimeParameterContract || parameters is IInputSlotContract))
        {
            var declared = runtime.Select(x => x.PropertyName)
                .Concat(inputs.Select(x => x.PropertyName))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var property in properties.Values)
            {
                if (property.Name is nameof(ToolParameters.Version)
                    or nameof(ToolParameters.RebindingRequired)
                    or nameof(ToolParameters.BindingValidationPending)
                    or nameof(ToolParameters.BindingSourceIdentities)
                    or nameof(ToolParameters.Context)
                    or nameof(ToolParameters.NodeResults)
                    or nameof(ToolParameters.ResolvedInputImage))
                    continue;
                if (property.IsDefined(typeof(JsonIgnoreAttribute), true))
                    continue;
                if (!declared.Contains(property.Name))
                    violations.Add(new("PARAM_UNDECLARED",
                        $"公开属性 '{property.Name}' 未声明输入槽位或运行参数角色。"));
            }
        }

        return violations;
    }

    public static void EnsureValid(ToolParameters parameters, bool strictComplete = false)
    {
        var violations = Validate(parameters, strictComplete);
        if (violations.Count == 0)
            return;
        throw new InvalidOperationException(
            "参数契约校验失败：" + string.Join("；", violations.Select(x => $"[{x.Code}] {x.Message}")));
    }

    private static void ValidateNames(
        IEnumerable<(string Name, string DisplayName, string Role)> entries,
        ICollection<ParameterContractViolation> violations)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                violations.Add(new("PARAM_NAME_EMPTY", $"{entry.Role} 的属性名不能为空。"));
            else if (!seen.Add(entry.Name))
                violations.Add(new("PARAM_NAME_DUPLICATE", $"{entry.Role} 属性 '{entry.Name}' 重复声明。"));
            if (string.IsNullOrWhiteSpace(entry.DisplayName))
                violations.Add(new("PARAM_DISPLAY_EMPTY", $"{entry.Role} 属性 '{entry.Name}' 缺少中文显示名。"));
        }
    }

    private static void ValidateProperty(
        string name,
        Type? declaredType,
        string? vocabulary,
        IReadOnlyDictionary<string, PropertyInfo> properties,
        string role,
        ICollection<ParameterContractViolation> violations)
    {
        if (!properties.TryGetValue(name, out var property))
        {
            violations.Add(new("PARAM_PROPERTY_MISSING", $"{role} 清单中的属性 '{name}' 不存在。"));
            return;
        }
        if (!property.CanRead)
            violations.Add(new("PARAM_PROPERTY_UNREADABLE", $"{role} 属性 '{name}' 不可读。"));
        var actualValueType = UnwrapParameterType(property.PropertyType);
        if (declaredType != null && !declaredType.IsAssignableFrom(actualValueType)
            && !actualValueType.IsAssignableFrom(declaredType))
            violations.Add(new("PARAM_TYPE_MISMATCH",
                $"{role} 属性 '{name}' 的声明类型 '{declaredType.Name}' 与实际类型 '{actualValueType.Name}' 不匹配。"));
        if (vocabulary != null && !string.Equals(
                SdkTypeVocabulary.ToTypeVocabulary(UnwrapParameterType(property.PropertyType)),
                vocabulary, StringComparison.OrdinalIgnoreCase))
            violations.Add(new("PARAM_VOCABULARY_MISMATCH",
                $"{role} 属性 '{name}' 的类型词汇 '{vocabulary}' 与实际类型不匹配。"));
    }

    private static Type UnwrapParameterType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ParamValue<>))
            return type.GetGenericArguments()[0];
        return type;
    }
}
