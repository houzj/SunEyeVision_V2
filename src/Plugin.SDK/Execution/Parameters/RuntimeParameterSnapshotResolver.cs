using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Contracts;

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
        IReadOnlyDictionary<string, Type>? targetTypes = null,
        IReadOnlyDictionary<string, BindingPlan>? plans = null,
        IBindingRunSnapshot? bindingSnapshot = null,
        IReadOnlyDictionary<string, Port>? declaredFieldTargets = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(nodeResults);

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var binding in bindings.Bindings)
        {
            // A persisted hint cannot override the actual writable property on this tool version.
            var targetType = targetTypes != null && targetTypes.TryGetValue(binding.ParameterName, out var type)
                ? type : binding.TargetType;
            if(plans?.TryGetValue(binding.ParameterName,out var plan)==true)
            {
                if(bindingSnapshot is null){errors.Add($"参数 {binding.ParameterName} 缺少冻结运行输入。");continue;}
                var resolved=_resolver.ResolveBinding(plan,bindingSnapshot);
                if(!resolved.Success){errors.Add($"参数 {binding.ParameterName} 解析失败: {resolved.Diagnostic?.Message}");continue;}
                try{values[binding.ParameterName]=declaredFieldTargets?.ContainsKey(binding.ParameterName)==true
                    ?ValueRules.Freeze(plan.Target,resolved.Value)
                    :BindingClrAdapter.Materialize(plan.Target,targetType??throw new InvalidOperationException("Target property is not declared."),resolved.Value);}
                catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException)
                {errors.Add($"参数 {binding.ParameterName} 物化失败: {fault.Message}");}
                continue;
            }
            if(binding.ReferenceExpression is not null)
            {errors.Add($"参数 {binding.ParameterName} 的正式绑定计划未编译，拒绝回退到名称路径。");continue;}
            var result = _resolver.Resolve(binding.ToSetting(), nodeResults, targetType ?? typeof(object));
            if (!result.IsSuccess)
            {
                errors.Add($"参数 {binding.ParameterName} 解析失败: {result.ErrorMessage}");
                continue;
            }

            try{values[binding.ParameterName] = declaredFieldTargets?.TryGetValue(binding.ParameterName,out var field)==true
                ?BindingClrAdapter.NormalizeValue(field,result.Value):result.Value;}
            catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException)
            {errors.Add($"参数 {binding.ParameterName} 声明转换失败: {fault.Message}");}
        }

        return new RuntimeParameterSnapshot(values, errors);
    }
}
