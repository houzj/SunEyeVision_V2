using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Compatibility = SunEyeVision.Plugin.Contracts.BindingCompatibility;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public static partial class BindingContractValidator
{
    // Existing CLR declaration adapters use the same pure compiler for supported scalar/sequence contracts.
    // No runtime value or previous result determines the declaration. Legacy geometric accessors remain
    // on their existing path until their explicit coordinate/field adapters are migrated.
    private static BindingValidation? CompileDeclaredAdaptation(Type element,bool collection,int? count,
        int? selectedIndex,int? selectedSourceCount,IReadOnlyList<Type> allowed,BindingConversion conversion=BindingConversion.Automatic)
    {
        var scalar = CanonicalScalar(element);
        if (scalar == null || allowed.Count == 0) return null;
        if(allowed.Any(type=>type==typeof(object)))return new(Compatibility.Unresolved,"目标类型尚未声明完整，等待校验");
        var targets = allowed.Select(CanonicalType).ToArray();
        if (targets.Any(type => type == null)) return null;
        var sourceType = selectedIndex.HasValue
            ? TypeRef.SequenceOf(scalar, selectedSourceCount.HasValue ? CountConstraint.Exactly(selectedSourceCount.Value) : CountConstraint.Any)
            : collection ? TypeRef.SequenceOf(scalar, count.HasValue ? CountConstraint.Exactly(count.Value) : CountConstraint.Any) : scalar;
        BindingClrAdapter.TryDescribe(element,out _,out var sourceStorage);
        var schema = new ToolSchema(Array.Empty<Port>(), new[] { new Port("value", sourceType,Storage:sourceStorage) });
        var source = new BindingSource(new("declaration", "source"), Guid.Empty, schema, 0);
        var scope = new BindingScopeSnapshot(new("declaration", "consumer"), Guid.Empty, 0, new[] { source }, new[] { source.Node },
            Array.Empty<KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>>());
        var reference = new OutputReference("source", "value", sourceType,
            selection: selectedIndex.HasValue ? new BindingSelection.AtConstant(selectedIndex.Value) : BindingSelection.All,storage:sourceStorage);
        var compiler = new ParameterBindingService();
        BindingCompilation? conditional = null;
        for(var targetIndex=0;targetIndex<targets.Length;targetIndex++)
        {
            BindingClrAdapter.TryDescribe(allowed[targetIndex],out _,out var targetStorage);
            var result = compiler.CompileBinding(reference, new Port("target", targets[targetIndex]!,Nullable:Nullable.GetUnderlyingType(allowed[targetIndex])!=null,Storage:targetStorage), scope,conversion);
            if (result.Compatibility == Compatibility.Compatible)
                return new(Compatibility.Compatible, "适配", collection ? element.MakeArrayType() : element);
            if (result.Compatibility == Compatibility.Conditional) conditional = result;
        }
        return conditional != null
            ? new(Compatibility.Conditional, "可绑定；每轮按同一绑定计划验证数量、索引及数值精度", collection ? element.MakeArrayType() : element)
            : new(Compatibility.Incompatible, "来源声明不符合目标数量或元素类型；需要显式转换或重新绑定");
    }
    private static TypeRef? CanonicalType(Type type)
    {
        return BindingClrAdapter.TryDescribe(type,out var declaration,out _)?declaration:null;
    }
    private static TypeRef? CanonicalScalar(Type type)
    {
        return BindingClrAdapter.TryDescribe(type,out var declaration,out _)&&declaration.Shape==ValueShape.Scalar?declaration:null;
    }
}
