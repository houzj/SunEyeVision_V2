using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldsView
{
    private readonly Dictionary<string,IReadOnlyList<FieldCondition>> _parentConditions=new(StringComparer.Ordinal);
    private readonly Dictionary<FrameworkElement,string> _rowGateAnchors=new();
    private readonly Dictionary<FrameworkElement,string[]> _advancedFields=new();
    private readonly HashSet<string> _inlineConstants=new(StringComparer.Ordinal);
    private Dictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>> ConfigureComposition(
        IReadOnlyDictionary<string,(Port Port,FieldPresentation Presentation,ParameterSetting Value)> declarations,
        IReadOnlyDictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>>? images,
        IReadOnlyDictionary<string,IReadOnlyList<FieldCondition>>? parents,
        IReadOnlyDictionary<string,NativeIntegerRadioOptions> radios,IReadOnlySet<string> paired,IReadOnlySet<string>? inlineConstants,
        IReadOnlySet<string>? textCoordinates=null,IReadOnlySet<string>? rawReferences=null)
    {
        var providers=images?.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
        foreach(var id in inlineConstants?.ToArray()??[])
        {
            if(!declarations.TryGetValue(id,out var field)||field.Port.Type.Shape!=ValueShape.Scalar
                ||field.Port.Type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64)||field.Port.EditModes!=(FieldEditModes.Constant|FieldEditModes.Reference))
                throw new ArgumentException("原订阅内联常量要求数值标量及常量/引用两种编辑能力。");
            if(paired.Contains(id)||radios.ContainsKey(id)||providers.ContainsKey(id)||field.Presentation.Layout?.DialogGroup is not null)
                throw new ArgumentException("内联常量不能与范围、单选、图像或样式弹层组合冲突。");
            SunEyeVision.Plugin.Wpf.Adapters.Fields.InlineConstantRules.Validate(field.Port,field.Presentation,field.Value);
            _inlineConstants.Add(id);
        }
        foreach(var field in declarations.Values)
            if(!_inlineConstants.Contains(field.Port.Id)&&textCoordinates?.Contains(field.Port.Id)!=true&&rawReferences?.Contains(field.Port.Id)!=true&&field.Presentation.Layout is {DecimalPlaces:null})
                throw new ArgumentException("不限固定小数位仅适用于原订阅同槽数值文本。");
        foreach(var id in textCoordinates??new HashSet<string>())
        {
            if(!declarations.TryGetValue(id,out var field))throw new ArgumentException("文本坐标引用未知字段。");
            if(field.Port.EditModes==(FieldEditModes.Constant|FieldEditModes.Reference))
            {
                SunEyeVision.Plugin.Wpf.Adapters.Fields.InlineConstantRules.Validate(field.Port,field.Presentation,field.Value);
                _inlineConstants.Add(id);
            }
        }
        foreach(var item in providers)
        {
            if(!declarations.TryGetValue(item.Key,out var field)||item.Value is null)throw new ArgumentException("图像选择配置引用了未知字段或空来源函数。");
            if(field.Port.Type!=TypeRef.ResourceOf(ResourceKind.Image)||field.Port.EditModes!=FieldEditModes.Reference)
                throw new ArgumentException("原图像选择器只接标量Image引用字段。");
            if(paired.Contains(item.Key)||radios.ContainsKey(item.Key)||field.Presentation.Layout?.DialogGroup is not null)
                throw new ArgumentException("图像选择器不能与范围、整数单选或样式弹层组合冲突。");
        }
        foreach(var parent in parents?.ToArray()??[])
        {
            if(!declarations.ContainsKey(parent.Key)||parent.Value is null)throw new ArgumentException("父条件引用了未知字段或空条件。");
            var conditions=parent.Value.Select(condition=>
            {
                if(condition is null||!declarations.TryGetValue(condition.FieldId,out var source)||source.Port.Type.Shape!=ValueShape.Scalar)
                    throw new ArgumentException("父条件必须引用已声明标量字段。");
                return condition with{Value=ValueRules.Freeze(source.Port,condition.Value)};
            }).ToArray();
            _parentConditions.Add(parent.Key,Array.AsReadOnly(conditions));
        }
        IReadOnlyList<FieldCondition> Gates(string id)=>_parentConditions.GetValueOrDefault(id)??Array.Empty<FieldCondition>();
        foreach(var field in declarations.Values)
            if(field.Presentation.Layout?.PairWith is {} right&&!Gates(field.Port.Id).SequenceEqual(Gates(right)))
                throw new ArgumentException("范围两端必须共享相同父条件。");
        foreach(var group in declarations.Values.Where(field=>field.Presentation.Layout?.DialogGroup is not null).GroupBy(field=>field.Presentation.Layout!.DialogGroup))
            if(group.Any(field=>!Gates(field.Port.Id).SequenceEqual(Gates(group.First().Port.Id))))throw new ArgumentException("样式组合的所有字段必须共享父条件。");
        return providers;
    }
    private void ValidateCondition(FieldCondition? condition)
    {
        if(condition is not null&&!_fieldStates.Any(state=>state.Port.Id==condition.FieldId))throw new ArgumentException("附件条件引用了未知字段。");
    }
}
