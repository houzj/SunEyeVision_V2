using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

internal sealed record AvailableGroupParameter(string Label, SubroutineParameterTarget Target, Type ValueType)
{ public override string ToString()=>Label; }
internal sealed record GroupParameterSlot(WorkflowNodeBase Node, PropertyInfo Property);
/// <summary>One Group page edits only the existing formal document's private candidates.</summary>
internal sealed class GroupRunParameterSession
{
    private readonly ProductionEditorContext _context;
    private readonly Solution _document;
    private readonly Func<string, WorkflowNodeBase, bool> _allowed;
    public SubroutineParameters Parameters => (SubroutineParameters)_context.DraftNode.Parameters;
    public int MaximumParameters { get; }
    public GroupRunParameterSession(ProductionEditorContext context, Solution document,
        Func<string, WorkflowNodeBase, bool> allowed, int maximumParameters = 32)
    { _context=context; _document=document; _allowed=allowed; MaximumParameters=maximumParameters; }
    private WorkflowNodeBase Node(SubroutineParameterTarget target, bool edit)
    {
        var node=_document.Workflows.SingleOrDefault(w=>w.Id==target.WorkflowId)?.Nodes.SingleOrDefault(n=>n.Id==target.NodeId)
            ?? throw new InvalidOperationException("关联的内部节点已不存在。");
        if(!_allowed(target.WorkflowId,node))throw new InvalidOperationException("参数关联超出当前Group内部范围。");
        return edit?_context.GetRelatedDraft(target.WorkflowId,node,Validate):node;
    }
    private static void Validate(ToolParameters parameters)
    {
        GroupParameterCatalog.ValidateDeclaredChoices(parameters);
        if(EditorDraftAdapter.Supports(parameters))EditorDraftAdapter.ForProduction(parameters).Validate();
        else
        {
            var validation=parameters.Validate();
            if(!validation.IsValid)throw new InvalidOperationException(validation.Errors.FirstOrDefault()??"内部模块参数无效。");
        }
    }
    public IReadOnlyList<AvailableGroupParameter> Available()
    {
        var result=new List<AvailableGroupParameter>();
        foreach(var flow in _document.Workflows)
        foreach(var node in flow.Nodes.Where(n=>_allowed(flow.Id,n)))
        {
            if(node.Parameters is SubroutineParameters nested)
            {
                foreach(var item in nested.RunParameterGroups.SelectMany(g=>g.Parameters))
                {
                    try
                    {
                        var slots=Resolve(item,false);
                        result.Add(new(flow.Name+" / "+node.DispName+" / "+item.Name,
                            new(){WorkflowId=flow.Id,NodeId=node.Id,ExposedParameterId=item.Id},slots[0].Property.PropertyType));
                    }
                    catch(InvalidOperationException) { /* Broken aliases remain in the saved tree for repair, never become selectable sources. */ }
                }
            }
            else foreach(var descriptor in GroupParameterCatalog.Describe(node.Parameters))
                result.Add(new(flow.Name+" / "+node.DispName+" / "+descriptor.DisplayName,
                    new(){WorkflowId=flow.Id,NodeId=node.Id,PropertyName=descriptor.PropertyName},descriptor.ValueType));
        }
        return result;
    }
    public IReadOnlyList<GroupParameterSlot> Resolve(SubroutineRunParameter parameter,bool edit=true)
    {
        var result=new List<GroupParameterSlot>();
        var active=new HashSet<(string,string,Guid)>();
        var unique=new HashSet<(string,string,string)>();
        void Visit(SubroutineParameterTarget target)
        {
            var node=Node(target,edit);
            if(target.ExposedParameterId is { } id)
            {
                var key=(target.WorkflowId,target.NodeId,id);
                if(!active.Add(key))throw new InvalidOperationException("嵌套曝光参数存在循环关联。");
                try
                {
                    var nested=(node.Parameters as SubroutineParameters)?.RunParameterGroups.SelectMany(g=>g.Parameters).SingleOrDefault(p=>p.Id==id)
                        ??throw new InvalidOperationException("嵌套曝光参数已不存在。");
                    var ownerParameters=(SubroutineParameters)node.Parameters;
                    if(ownerParameters.SubroutineId!=node.SubroutineId)throw new InvalidOperationException("嵌套Group拥有者配置不一致。");
                    var child=_document.Workflows.SingleOrDefault(w=>w.Id==node.SubroutineId)??throw new InvalidOperationException("嵌套Group定义已不存在。");
                    foreach(var relation in nested.Targets)
                    {
                        var relatedNode=Node(relation,false);
                        if(!WorkflowOwnership.IsInternalNode(_document,child,target.WorkflowId,relation.WorkflowId,relatedNode))
                            throw new InvalidOperationException("曝光关联超出其所属嵌套Group的内部范围。");
                        Visit(relation);
                    }
                }
                finally {active.Remove(key);}
                return;
            }
            if(!GroupParameterCatalog.Describe(node.Parameters).Any(p=>p.PropertyName==target.PropertyName))
                throw new InvalidOperationException("工具不再声明此曝光参数。");
            var property=node.Parameters.GetType().GetProperty(target.PropertyName)!;
            if(unique.Add((target.WorkflowId,target.NodeId,target.PropertyName)))result.Add(new(node,property));
        }
        foreach(var relation in parameter.Targets)Visit(relation);
        if(result.Count==0)throw new InvalidOperationException("该参数没有有效关联。");
        if(result.Any(s=>s.Property.PropertyType!=result[0].Property.PropertyType))throw new InvalidOperationException("关联参数的CLR类型不一致。");
        return result;
    }
    public string TargetLabel(SubroutineParameterTarget target)
    {
        try
        {
            var node=Node(target,false);
            var label=target.ExposedParameterId is {} id
                ? (node.Parameters as SubroutineParameters)?.RunParameterGroups.SelectMany(g=>g.Parameters).SingleOrDefault(p=>p.Id==id)?.Name
                : GroupParameterCatalog.Describe(node.Parameters).SingleOrDefault(p=>p.PropertyName==target.PropertyName)?.DisplayName;
            return node.DispName+" / "+(label??"参数已缺失");
        }
        catch(InvalidOperationException){return "关联模块或参数已缺失";}
    }
    public SubroutineRunParameterGroup AddGroup()
    {
        if(Parameters.RunParameterGroups.Count>=MaximumParameters)throw new InvalidOperationException("运行参数分组已达到配置数量上限。");
        var i=0;while(Parameters.RunParameterGroups.Any(g=>g.Name=="分组"+i))i++;
        var group=new SubroutineRunParameterGroup{Name="分组"+i};Parameters.RunParameterGroups.Add(group);return group;
    }
    public SubroutineRunParameter Add(SubroutineRunParameterGroup group,AvailableGroupParameter source)
    {
        if(!Parameters.RunParameterGroups.Contains(group))throw new InvalidOperationException("分组已失效。");
        if(Parameters.RunParameterGroups.Sum(g=>g.Parameters.Count)>=MaximumParameters)throw new InvalidOperationException("运行参数已达到配置数量上限。");
        var name=source.Label.Split(" / ").Last();var alias=name;var i=1;
        while(Parameters.RunParameterGroups.SelectMany(g=>g.Parameters).Any(p=>p.Name==alias))alias=name+"_"+i++;
        var item=new SubroutineRunParameter{Name=alias};AddTarget(item,source);group.Parameters.Add(item);return item;
    }
    public void AddTarget(SubroutineRunParameter item,AvailableGroupParameter source)
    {
        var target=source.Target;
        if(item.Targets.Any(t=>t.WorkflowId==target.WorkflowId&&t.NodeId==target.NodeId&&t.PropertyName==target.PropertyName&&t.ExposedParameterId==target.ExposedParameterId))
            throw new InvalidOperationException("参数关联已经存在。");
        var copied=new SubroutineParameterTarget{WorkflowId=target.WorkflowId,NodeId=target.NodeId,PropertyName=target.PropertyName,ExposedParameterId=target.ExposedParameterId};
        item.Targets.Add(copied);
        try {if(Choices(Resolve(item,false)) is {Count:0})throw new InvalidOperationException("关联参数没有共同可选值。");}
        catch {item.Targets.Remove(copied);throw;}
    }
    public static IReadOnlyList<GroupParameterChoice>? Choices(IReadOnlyList<GroupParameterSlot> slots)
    {
        GroupParameterChoice[]? common=null;
        foreach(var slot in slots)
        {
            var descriptor=GroupParameterCatalog.Describe(slot.Node.Parameters).Single(d=>d.PropertyName==slot.Property.Name);
            if(descriptor.Choices is not {} choices)continue;
            common=common is null?choices.ToArray():common.Where(c=>choices.Any(other=>Equals(c.Value,other.Value))).ToArray();
        }
        return common;
    }
    public static bool Scalar(Type type) => type.IsEnum||type==typeof(bool)||type==typeof(string)||
        type==typeof(int)||type==typeof(long)||type==typeof(float)||type==typeof(double)||type==typeof(decimal);
    public static object? Read(GroupParameterSlot slot) => slot.Node.ParamSettings.GetSetting(slot.Property.Name) is {BindingType:BindingType.Constant} setting
        ? ConvertValue(setting.ConstantValue,slot.Property.PropertyType):slot.Property.GetValue(slot.Node.Parameters);
    public static object? ConvertValue(object? value,Type type)
    {
        if(value is System.Text.Json.JsonElement json)value=json.Deserialize(type,WorkflowSerializationOptions.Compact);
        if(type.IsEnum)
        {
            var parsed=value is not null&&type.IsInstanceOfType(value)?value:Enum.Parse(type,Convert.ToString(value,CultureInfo.CurrentCulture)??"",false);
            if(!Enum.IsDefined(type,parsed))throw new InvalidOperationException("参数枚举值未声明。");
            return parsed;
        }
        var converted=value is not null&&type.IsInstanceOfType(value)?value:Convert.ChangeType(value,type,CultureInfo.CurrentCulture);
        if(converted is double d&&!double.IsFinite(d)||converted is float f&&!float.IsFinite(f))throw new InvalidOperationException("参数值必须为有限数值。");
        return converted;
    }
    public void Write(SubroutineRunParameter item,object? value)
    {
        var slots=Resolve(item);
        if(!Scalar(slots[0].Property.PropertyType))throw new InvalidOperationException("此参数需接入专用编辑器。");
        if(slots.Any(s=>s.Node.ParamSettings.GetSetting(s.Property.Name) is {BindingType:not BindingType.Constant}))
            throw new InvalidOperationException("关联参数已有动态绑定，请先在原工具中调整绑定。");
        var next=ConvertValue(value,slots[0].Property.PropertyType);
        var choices=Choices(slots);
        if(choices is not null&&!choices.Any(c=>Equals(c.Value,next)))throw new InvalidOperationException("该值不是关联参数共同支持的选项。");
        var previous=slots.Select(s=>(Slot:s,Value:s.Property.GetValue(s.Node.Parameters),Setting:s.Node.ParamSettings.GetSetting(s.Property.Name),Constant:s.Node.ParamSettings.GetSetting(s.Property.Name)?.ConstantValue)).ToArray();
        try
        {
            foreach(var entry in previous)
            { entry.Slot.Property.SetValue(entry.Slot.Node.Parameters,next);if(entry.Setting is not null)entry.Setting.ConstantValue=next; }
        }
        catch
        {
            foreach(var entry in previous)
            {entry.Slot.Property.SetValue(entry.Slot.Node.Parameters,entry.Value);if(entry.Setting is not null)entry.Setting.ConstantValue=entry.Constant;}
            throw;
        }
    }
}
