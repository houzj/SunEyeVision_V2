using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 将绑定定义解析为一次运行快照。解析结果与设计参数对象完全分离。
/// </summary>
public sealed class RuntimeParameterSnapshotResolver
{
    private readonly IParameterResolver _resolver;

    public RuntimeParameterSnapshotResolver(IParameterResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public RuntimeParameterSnapshot Resolve(
        ParameterBindingSet bindings,
        IDictionary<string, ToolResults> nodeResults,
        IReadOnlyDictionary<string, Type>? targetTypes = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(nodeResults);

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var binding in bindings.Bindings)
        {
            var targetType = binding.TargetType
                ?? (targetTypes != null && targetTypes.TryGetValue(binding.ParameterName, out var type)
                    ? type
                    : null);
            var result = _resolver.Resolve(binding.ToSetting(), nodeResults, targetType ?? typeof(object));
            if (!result.IsSuccess)
            {
                errors.Add($"参数 {binding.ParameterName} 解析失败: {result.ErrorMessage}");
                continue;
            }

            values[binding.ParameterName] = result.Value;
        }

        return new RuntimeParameterSnapshot(values, errors);
    }
}
