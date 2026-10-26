using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

public sealed partial class DeclaredToolConfigurationControl
{
    private void BuildRegionSourceEditors(IConfigurationFieldProvider provider)
    {
        if(_parameters is not RoiParameters parameters||_fields is null||CurrentNode is not WorkflowNodeBase node)return;
        _inheritedRegionEditors.Clear();_inheritedRegionChecks.Clear();
        foreach(var group in provider.ConfigurationSchema?.Presentation?.Regions??Array.Empty<RegionFieldGroup>())
        {
            var collection=group.Role switch {"inspection"=>parameters.InspectionRois,"mask"=>parameters.MaskRois,_=>throw new ArgumentException("未知区域角色。")};
            var title=group.Role=="mask"?"屏蔽区域":"检测区域";
            _fields.Fields.Single(field=>field.FieldId==group.ModeFieldId).UseRegionSourceSelector();
            BuildInheritancePanels(group,collection,title);
            var rows=new StackPanel{Margin=new Thickness(0,4,0,8)};
            _fields.AddAfterField(group.ModeFieldId,rows,new(group.ModeFieldId,nameof(RegionFieldMode.InheritedGeometry)));
            foreach(var item in collection.Rois.Where(item=>item.IsSubscribed).ToArray())AddGeometry(item);
            if(!collection.Rois.Any(item=>item.IsSubscribed))AddGeometry(null);
            var add=new Button{Content="增加几何订阅",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,0,8)};
            add.Click+=(_,_)=>AddGeometry(null);rows.Children.Add(add);
            if(provider.RegionInputStorage.TryGetValue(group.Role,out var slot))
            {
                var declared=(_parameters as IInputSlotContract)?.GetInputSlots().SingleOrDefault(input=>input.PropertyName==slot);
                if(declared?.ValueType!=typeof(IReadOnlyRegion))throw new InvalidOperationException("像素区域存储必须对应已声明的Region输入槽。");
                var view=new SourceSelectionField(title+" · 像素区域",()=>Choices(SourceKind.Region),
                    ()=>Describe(node.ParamSettings.GetSetting(slot)),choice=>
                    {
                        var previous=node.ParamSettings.GetSetting(slot)?.Clone();
                        var candidate=Resolve(choice,SourceKind.Region);
                        return Change(()=>{if(candidate is null)node.ParamSettings.RemoveSetting(slot);else node.ParamSettings.SetSetting(Setting(slot,candidate));},
                            ()=>{if(previous is null)node.ParamSettings.RemoveSetting(slot);else node.ParamSettings.SetSetting(previous);},choice is not null);
                    }){Tag=group.RegionSourceFieldId};
                _fields.AddAfterField(group.ModeFieldId,view,new(group.ModeFieldId,nameof(RegionFieldMode.InheritedRegion)));RegisterSource(view,()=>Mode()==nameof(RegionFieldMode.InheritedRegion));
            }
            void AddGeometry(Roi? existing)
            {
                Roi? item=existing;
                var view=new SourceSelectionField(title+" · 几何订阅",()=>Choices(SourceKind.Geometry),()=>Describe(item?.Binding),choice=>
                {
                    var previous=item;var binding=item?.Binding;var index=item is null?-1:collection.Rois.IndexOf(item);
                    var candidate=Resolve(choice,SourceKind.Geometry);
                    return Change(()=>
                    {
                        if(candidate is null){if(item is not null)collection.Rois.Remove(item);item=null;}
                        else
                        {
                            if(item is null){item=Roi.CreateSubscribed(title+"订阅",candidate.SourceNodeId,candidate.PropertyName);collection.Rois.Add(item);}
                            item.Binding=Setting(item.Name,candidate);
                        }
                    },()=>
                    {
                        if(item is not null&&!ReferenceEquals(item,previous))collection.Rois.Remove(item);
                        item=previous;if(item is not null){item.Binding=binding;if(!collection.Rois.Contains(item))collection.Rois.Insert(Math.Min(index,collection.Rois.Count),item);}
                    },choice is not null);
                }){Tag=group.GeometrySourceFieldId};
                rows.Children.Add(view);RegisterSource(view,()=>Mode()==nameof(RegionFieldMode.InheritedGeometry));
            }
            string? Mode()=>_accessors.Single(field=>field.Port.Id==group.ModeFieldId).Read()?.ToString();
        }
    }
    private SourceChoice[] Choices(SourceKind kind)=>SdkSources(kind).Select(source=>new SourceChoice(source.Identity,source.Source.DropdownDisplayText,false)).ToArray();
    private AvailableDataSource? Resolve(SourceChoiceSelection? choice,SourceKind kind)
    {
        if(choice is null)return null;
        if(choice.Index.HasValue)throw new ArgumentException("区域来源按完整对象或几何集合消费，不能隐式截取元素。");
        return SdkSources(kind).SingleOrDefault(source=>source.Identity==choice.Identity)?.Source
            ??throw new InvalidOperationException("来源已不在当前声明与作用域中，请重新选择。");
    }
    private static string Describe(ParamSetting? binding)=>binding is null?"尚未选择来源":$"{binding.SourceNodeId} · {binding.SourceProperty}";
    private ParamSetting Setting(string path,AvailableDataSource source)
    {
        var output=(DataProvider as DataSourceQueryService)?.FormalOutputContractReader?.Invoke(source.SourceNodeId)
            ?.Single(item=>source.SourceFieldId is { } id?item.FieldId==id:item.Name==source.PropertyName)
            ??throw new InvalidOperationException("来源正式声明已经失效。");
        var port=BindingClrAdapter.DescribeOutput(output);
        var reference=new OutputReference(source.SourceNodeId,port.Id,port.Type,scopeId:source.SourceWorkflowId,storage:port.Storage);
        return new(){ParameterName=path,BindingType=BindingType.Binding,SourceNodeId=source.SourceNodeId,SourceWorkflowId=source.SourceWorkflowId,
            SourceFieldId=source.SourceFieldId,SourceProperty=source.PropertyName,ReferenceExpression=BindingReferenceSerializer.Serialize(reference)};
    }
    private string? Change(Action apply,Action rollback,bool validate)
    {
        if(!RegionScopeCurrent())return "当前编辑范围已失效。";
        var accepted=false;
        try
        {
            apply();
            if(validate&&ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext context&&!context.TryValidateEditor(out var error))return error;
            accepted=true;RefreshRegionScene();return null;
        }
        finally{if(!accepted)rollback();}
    }
}
