using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 一次运行解析后的只读参数值。算法只读取该快照，不修改设计期参数或绑定定义。
/// </summary>
public sealed class RuntimeParameterSnapshot
{
    public RuntimeParameterSnapshot(
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyList<string>? errors = null)
    {
        Values = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(values, StringComparer.Ordinal));
        Errors = new ReadOnlyCollection<string>((errors ?? Array.Empty<string>()).ToList());
    }

    public IReadOnlyDictionary<string, object?> Values { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public bool TryGet<T>(string parameterName, out T? value)
    {
        if (Values.TryGetValue(parameterName, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public T GetRequired<T>(string parameterName)
    {
        if (TryGet<T>(parameterName, out var value) && value is not null)
            return value;
        throw new InvalidOperationException(
            $"运行参数快照中缺少参数 '{parameterName}' 或类型不是 {typeof(T).Name}。");
    }
}
