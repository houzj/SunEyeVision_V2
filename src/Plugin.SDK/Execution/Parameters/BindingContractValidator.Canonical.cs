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
        int? selectedIndex,int? selectedSourceCount,IReadOnlyList<Type> allowed)
    {
        var scalar = CanonicalScalar(element);
        if (scalar == null || allowed.Count == 0) return null;
        var targets = allowed.Select(CanonicalType).ToArray();
        if (targets.Any(type => type == null)) return null;
        var sourceType = selectedIndex.HasValue
            ? TypeRef.SequenceOf(scalar, selectedSourceCount.HasValue ? CountConstraint.Exactly(selectedSourceCount.Value) : CountConstraint.Any)
            : collection ? TypeRef.SequenceOf(scalar, count.HasValue ? CountConstraint.Exactly(count.Value) : CountConstraint.Any) : scalar;
        var schema = new ToolSchema(Array.Empty<Port>(), new[] { new Port("value", sourceType) });
        var source = new BindingSource(new("declaration", "source"), Guid.Empty, schema, 0);
        var scope = new BindingScopeSnapshot(new("declaration", "consumer"), Guid.Empty, 0, new[] { source }, new[] { source.Node },
            Array.Empty<KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>>());
        var reference = new OutputReference("source", "value", sourceType,
            selection: selectedIndex.HasValue ? new BindingSelection.AtConstant(selectedIndex.Value) : BindingSelection.All);
        var compiler = new ParameterBindingService();
        BindingCompilation? conditional = null;
        foreach (var target in targets)
        {
            var result = compiler.CompileBinding(reference, new Port("target", target!), scope);
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
        if (type.IsArray && type.GetArrayRank() == 1)
        { var element = CanonicalScalar(type.GetElementType()!); return element == null ? null : TypeRef.SequenceOf(element); }
        return CanonicalScalar(type);
    }
    private static TypeRef? CanonicalScalar(Type type)
    {
        if (type == typeof(bool)) return TypeRef.ScalarOf(ScalarKind.Boolean);
        if (type == typeof(string)) return TypeRef.ScalarOf(ScalarKind.Text);
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long)) return TypeRef.ScalarOf(ScalarKind.Int64);
        if (type == typeof(float) || type == typeof(double)) return TypeRef.ScalarOf(ScalarKind.Float64);
        return null;
    }
}
