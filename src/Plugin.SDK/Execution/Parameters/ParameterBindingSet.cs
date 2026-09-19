using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 一个工具节点的全部参数绑定。该对象属于设计配置，不保存本次运行解析后的值。
/// </summary>
public sealed class ParameterBindingSet
{
    private readonly IReadOnlyList<ParameterBindingDefinition> _bindings;

    public ParameterBindingSet(IEnumerable<ParameterBindingDefinition>? bindings = null)
    {
        var items = (bindings ?? Enumerable.Empty<ParameterBindingDefinition>()).ToList();
        var duplicate = items
            .GroupBy(item => item.ParameterName, StringComparer.Ordinal)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicate != null)
            throw new ArgumentException($"参数绑定名称重复或为空: {duplicate.Key}", nameof(bindings));

        _bindings = new ReadOnlyCollection<ParameterBindingDefinition>(items);
    }

    public IReadOnlyList<ParameterBindingDefinition> Bindings => _bindings;

    public bool TryGet(string parameterName, out ParameterBindingDefinition? binding)
    {
        binding = _bindings.FirstOrDefault(item =>
            string.Equals(item.ParameterName, parameterName, StringComparison.Ordinal));
        return binding != null;
    }

    public static ParameterBindingSet Empty { get; } = new();

    public static ParameterBindingSet FromSettings(ParamSettingContainer? container)
        => new(container?.Settings.Select(ParameterBindingDefinition.FromSetting));
}
