using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

internal sealed record VariableBindingPlan(VariableSetting Setting, Port Source, TypeRef SelectedType,
    Port Target, BindingPlan? Index, BindingVariableSource? VariableIndex,
    ParameterReferenceBehavior Behavior);

/// <summary>Compile against the invocation's frozen pure-value declarations and actual graph scope.</summary>
internal static class VariableBindingCompiler
{
    internal static VariableBindingPlan Compile(VariableSetting setting, Port target, string solutionId,
        IReadOnlyDictionary<string, Port> declarations, IBindingCompilationScope scope,
        ParameterReferenceBehavior behavior = ParameterReferenceBehavior.StrictExactlyOne)
    {
        ParameterReferenceRules.ValidateTarget(target, behavior);
        if (!string.IsNullOrEmpty(setting.RebindingReason)) throw new ArgumentException(setting.RebindingReason);
        if (solutionId != setting.Reference.SolutionId || !declarations.TryGetValue(setting.Reference.VariableId, out var source))
            throw new ArgumentException("变量不存在于本批冻结声明，或没有受支持的纯值类型。");
        var reference = setting.Reference;
        if (source.Type != reference.Type || NumericStorageRules.Effective(source.Type, source.Storage) != reference.Storage)
            throw new ArgumentException("变量类型或物理存储声明已改变，请重新绑定。");
        var type = source.Type; BindingPlan? index = null; BindingVariableSource? variableIndex = null;
        var service = new ParameterBindingService();
        if (reference.Selection is not BindingSelection.AllItems)
        {
            if (type.Shape != ValueShape.Sequence) throw new ArgumentException("变量索引需要序列声明。");
            if (reference.Selection is BindingSelection.AtConstant fixedIndex)
            {
                if (type.Count.Maximum is { } maximum && fixedIndex.Index >= maximum)
                    throw new ArgumentException("变量索引超出声明范围。");
            }
            else if (reference.Selection is BindingSelection.AtField dynamicIndex)
            {
                var targetIndex = new Port("index", TypeRef.ScalarOf(ScalarKind.Int64),
                    Storage: behavior == ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback ? NumericStorage.Int32 : NumericStorage.Int64);
                var compiled = service.CompileBinding(dynamicIndex.Reference, targetIndex, scope, behavior: behavior);
                index = compiled.Plan ?? throw new ArgumentException(compiled.Diagnostic?.Message ?? "变量动态索引未就绪。");
                if (!ParameterReferenceRules.CanUseIndexSource(index.Source.SelectedType, behavior))
                    throw new ArgumentException("变量动态索引必须是整数标量。");
            }
            else if (reference.Selection is BindingSelection.AtVariable variable)
            {
                if (!ParameterReferenceRules.CanUseIndexSource(variable.Reference.Type, behavior) ||
                    !scope.TryGetVariable(variable.Reference, out variableIndex) ||
                    variableIndex.Reference.Type != variable.Reference.Type ||
                    variableIndex.Reference.SolutionId != variable.Reference.SolutionId ||
                    variableIndex.Reference.VariableId != variable.Reference.VariableId ||
                    variableIndex.Reference.Storage != variable.Reference.Storage)
                    throw new ArgumentException("索引变量的冻结声明不存在或已改变。");
                if (variableIndex.Producer is { } producer && producer.ScopeId == scope.Consumer.ScopeId &&
                    scope.ValidateDependencies(new[] { producer }) is { } failure) throw new ArgumentException(failure.Message);
            }
            else throw new ArgumentException("不支持的变量索引类型。");
            type = type.Element!;
        }
        _ = service.CompareValueSource(new Port(source.Id, type, Storage: source.Storage), target,
            setting.Conversion, out var issue, behavior);
        if (issue is not null) throw new ArgumentException(issue.Message);
        return new VariableBindingPlan(setting, source, type, target, index, variableIndex, behavior);
    }
}
