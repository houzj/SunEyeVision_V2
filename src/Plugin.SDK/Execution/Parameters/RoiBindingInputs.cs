using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>The persisted ROI model supplies its own finite binding slots, independently of controls.</summary>
public static class RoiBindingInputs
{
    public sealed record Slot(string Key,Roi Roi,string? Parameter,ParamSetting? Setting)
    {
        public Port Target(OutputReference source)
        {
            if(Parameter is not null)return new(Key,TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)));
            if(source.MemberFieldPath.Count!=0)throw new ArgumentException("ROI_SOURCE_PATH: 整区域来源必须是正式几何输出或其明确元素。");
            var element=source.Type.Shape==ValueShape.Sequence?source.Type.Element!:source.Type;
            if(element!=RoiShapeValueAdapter.Declaration&&element.Shape!=ValueShape.Geometry)
                throw new ArgumentException("ROI_SOURCE_TYPE: 整区域只接受已声明的几何，不能从文本或任意记录推断。");
            return new(Key,TypeRef.SequenceOf(element));
        }
    }
    public static IReadOnlyList<Slot> Describe(ToolParameters parameters)
    {
        var slots=new List<Slot>();
        var collections=parameters is RoiParameters regions
            ?new[]{(Name:nameof(regions.InspectionRois),Collection:regions.InspectionRois),(Name:nameof(regions.MaskRois),Collection:regions.MaskRois)}
            :Array.Empty<(string Name,RoiCollection Collection)>();
        foreach(var (name,collection) in collections)
        {
            foreach(var roi in collection.Rois.Where(r=>r.IsEnabled))
            {
                var key=name+":"+roi.Id;
                if(parameters is IBindingInputActivityProvider activity&&!activity.IsBindingInputActive(key))continue;
                if(roi.IsSubscribed)Add(new(key,roi,null,roi.Binding));
                if(roi.IsComposed)
                    foreach(var parameter in RoiComposeResolver.RequiredParameters(roi.DeclaredShapeType))
                        Add(new(key+":"+parameter,roi,parameter,roi.ParameterBindings?.GetValueOrDefault(parameter)));
            }
        }
        return slots.AsReadOnly();
        void Add(Slot slot)
        {if(parameters is not IBindingInputActivityProvider activity||activity.IsBindingInputActive(slot.Key))slots.Add(slot);}
    }
    public static IReadOnlyList<ParamSetting> Settings(ToolParameters parameters)=>Describe(parameters)
        .Where(slot=>slot.Setting?.BindingType==BindingType.Binding).Select(slot=>
        {var setting=slot.Setting!.Clone();setting.ParameterName=slot.Key;return setting;}).ToArray();
    public static void Apply(ToolParameters parameters,RuntimeParameterSnapshot snapshot,IReadOnlyDictionary<string,BindingPlan>? plans)
    {
        if(!snapshot.IsValid)return;
        foreach(var slot in Describe(parameters))
        {
            if(slot.Setting?.BindingType!=BindingType.Binding)continue;
            if(!snapshot.Values.TryGetValue(slot.Key,out var value)||plans?.TryGetValue(slot.Key,out var plan)!=true)
                throw new InvalidOperationException("ROI_BINDING_PLAN: 区域绑定没有本次执行的正式解析计划："+slot.Key);
            // The declaration decides scalar broadcasting; a one-element source array remains an array.
            if(slot.Parameter is not null&&plan.Source.SelectedType.Shape!=ValueShape.Sequence)
                value=value is double[] {Length:1} numbers?numbers[0]:throw new InvalidOperationException("ROI_BINDING_SCALAR: 标量解析必须恰有一个值。");
            slot.Roi.SetRuntimeBinding(slot.Parameter,value,plan.Source.Source.Node.NodeId);
        }
    }
}
