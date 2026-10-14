using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

/// <summary>Uses the existing ParamBinding expression syntax and output resolver, never UI objects.</summary>
public static class ProtocolBindingInput
{
    public static object Resolve(string expression, IDictionary<string, ToolResults>? results,
        IDictionary<string, object?>? variables)
    {
        ArgumentNullException.ThrowIfNull(expression);
        const string globalPrefix = "__GlobalVars__.";
        if (expression.StartsWith(globalPrefix, StringComparison.Ordinal))
        {
            var name = expression[globalPrefix.Length..];
            if (variables == null || !variables.TryGetValue(name, out var value))
                throw new InvalidOperationException($"变量 {name} 在本次快照中不可用。");
            return RequireScalar(value);
        }
        var dot = expression.IndexOf('.');
        if (dot > 0 && (Guid.TryParse(expression[..dot], out _) || results?.ContainsKey(expression[..dot]) == true || expression.StartsWith("__SubroutineInputs__.", StringComparison.Ordinal)))
        {
            var id = expression[..dot];
            if (results == null || !results.TryGetValue(id, out var result) || !result.IsSuccess)
                throw new InvalidOperationException("上游节点没有本次成功结果。");
            return RequireScalar(OutputVariableResolver.Resolve(result, expression[(dot + 1)..], id));
        }
        return expression; // Literals are strings; do not guess numbers or hexadecimal.
    }

    private static object RequireScalar(object? value)
    {
        if (value is Array array)
        {
            if (array.Length != 1) throw new FormatException("当前协议工具只接受单项输入，不自动展开列表。");
            value = array.GetValue(0);
        }
        return value ?? throw new FormatException("绑定输入缺少有效值。");
    }
}
