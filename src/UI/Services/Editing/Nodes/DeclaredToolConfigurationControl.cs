using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;
using ConfigurationSetting = SunEyeVision.Plugin.Contracts.ParameterSetting;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Host adapter for the optional-editor fallback. Publication remains owned by the existing S2 edit scope.</summary>
public sealed class DeclaredToolConfigurationControl : ToolDebugControlBase, IToolEditorCommitParticipant, IToolEditorPendingState
{
    private readonly IToolPlugin _tool;
    private ToolParameters? _parameters;
    private DeclaredFieldsView? _fields;
    private IReadOnlyList<ConfigurationFieldAccessor> _accessors=Array.Empty<ConfigurationFieldAccessor>();
    public bool HasPendingEdits=>_fields?.Fields.Any(editor=>editor.HasInvalidText)==true;
    public DeclaredToolConfigurationControl(IToolPlugin tool){_tool=tool;}
    public override void SetParameters(ToolParameters parameters)
    {
        base.SetParameters(parameters);_parameters=parameters;
        if(parameters is IConfigurationFieldProvider provider)
        {
            _accessors=provider.GetConfigurationFields();
            _fields=new(_accessors.Select(field=>(field.Port,field.Presentation,ReadDraft(field))),WriteDraft,SelectReference,
                id=>new ConstantSetting(_accessors.Single(field=>field.Port.Id==id).Read()),FormatReference);
            Content=_fields;return;
        }
        var slots=new Dictionary<string,PropertyInfo>(StringComparer.Ordinal);
        var fields=new List<(Port Port,FieldPresentation Presentation,ConfigurationSetting Value)>();
        foreach(var field in (parameters as IRuntimeParameterContract)?.GetRuntimeParameters()??Array.Empty<RuntimeParameterDescriptor>())
        {
            var property=parameters.GetType().GetProperty(field.PropertyName);
            if(property is not {CanRead:true,CanWrite:true})continue;
            var type=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
            Port port;object? value;
            if(type.IsEnum)
            {
                port=new(field.PropertyName,DataType.Text,EditModes:FieldEditModes.Constant,
                    Choices:new TextChoiceConstraint(Enum.GetNames(type)));value=property.GetValue(parameters)?.ToString();
            }
            else
            {
                if(!BindingClrAdapter.TryDescribe(type,out var declaration,out _)||declaration.Shape!=ValueShape.Scalar)continue;
                port=BindingClrAdapter.DescribePort(field.PropertyName,type) with {EditModes=FieldEditModes.Constant};
                value=BindingClrAdapter.NormalizeValue(port,property.GetValue(parameters));
            }
            slots.Add(port.Id,property);
            fields.Add((port,new(port.Id,field.DisplayName,"运行参数",fields.Count,
                type.IsEnum?FieldEditorKind.Choice:FieldEditorKind.Scalar,type.IsEnum?Enum.GetNames(type):null),new ConstantSetting(value)));
        }
        _fields=new(fields,(id,setting)=>
        {
            var property=slots[id];var literal=((ConstantSetting)setting).Value;
            var value=property.PropertyType.IsEnum?Enum.Parse(property.PropertyType,(string)literal!):
                BindingClrAdapter.Materialize(fields.Single(field=>field.Port.Id==id).Port,property.PropertyType,literal);
            property.SetValue(parameters,value);
        });
        Content=_fields;
    }
    public bool TryValidateEditor(out string error)
    {
        if(_fields is not null&&!_fields.TryValidate(out error))return false;
        var validation=_parameters?.Validate();
        error=validation is null?"配置尚未初始化。":validation.IsValid?"":string.Join("；",validation.Errors);
        return error.Length==0;
    }
    public bool TryCommitEditor(out string error)=>TryValidateEditor(out error);
    public override bool ValidateParameters()=>TryValidateEditor(out _);
    protected override bool CanExecuteTool()=>_parameters is not null;
    protected override object ExecuteTool()=>ExecuteWithLiveContext(_tool,_parameters??throw new InvalidOperationException("配置尚未初始化。"));
    public override void SetDataProvider(object dataProvider)
    {base.SetDataProvider(dataProvider);if(_parameters is not null)SetParameters(_parameters);}

    private string FormatReference(OutputReference reference)
    {
        var query=DataProvider as DataSourceQueryService;
        var source=query?.FormalOutputContractReader?.Invoke(reference.NodeId)?.SingleOrDefault(output=>BindingClrAdapter.OutputPortId(output)==reference.PortId);
        var nodeName=AvailableDataSources.FirstOrDefault(source=>source.SourceNodeId==reference.NodeId)?.SourceNodeName??reference.NodeId;
        var member=reference.MemberFieldPath.Count>0?" / "+string.Join(" / ",reference.MemberFieldPath):"";
        var selection=reference.Selection switch {BindingSelection.AtConstant index=>" ["+index.Index+"]",BindingSelection.AtField=>" [绑定序号]",_=>""};
        return nodeName+" · "+(source?.DisplayName??source?.Name??"已声明字段")+member+selection;
    }

    private ConfigurationSetting ReadDraft(ConfigurationFieldAccessor field)
    {
        if(CurrentNode is WorkflowNodeBase node&&node.ParamSettings.GetSetting(field.StoragePath)?.ReferenceExpression is { } expression)
            return new ReferenceSetting(BindingReferenceSerializer.Deserialize(expression));
        return new ConstantSetting(field.Read());
    }
    private void WriteDraft(string id,ConfigurationSetting value)
    {
        if(ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext {IsValid:false})throw new InvalidOperationException("当前编辑范围已失效。");
        var field=_accessors.Single(field=>field.Port.Id==id);
        if(value is ConstantSetting constant)field.Write(constant.Value);
        else if(value is not ReferenceSetting)throw new ArgumentException("此常量字段不支持清除。");
        if(CurrentNode is WorkflowNodeBase node)
        {
            if(value is ReferenceSetting reference)node.ParamSettings.SetSetting(ToSetting(field,reference.Reference));
            else node.ParamSettings.RemoveSetting(field.StoragePath);
            node.Parameters.RebindingRequired.Remove(field.StoragePath);node.Parameters.BindingValidationPending.Remove(field.StoragePath);
        }
    }
    private ParamSetting ToSetting(ConfigurationFieldAccessor field,OutputReference reference)
    {
        var source=(DataProvider as DataSourceQueryService)?.FormalOutputContractReader?.Invoke(reference.NodeId)
            ?.SingleOrDefault(output=>BindingClrAdapter.OutputPortId(output)==reference.PortId);
        return new ParamSetting {ParameterName=field.StoragePath,BindingType=BindingType.Binding,
            ReferenceExpression=BindingReferenceSerializer.Serialize(reference),SourceNodeId=reference.NodeId,
            SourceWorkflowId=reference.ScopeId,SourceFieldId=source?.FieldId,SourceProperty=source?.Name??reference.PortId};
    }
    private ReferenceSetting? SelectReference(Port target)
    {
        var candidates=new List<DeclaredReferenceCandidate>();
        if(DataProvider is DataSourceQueryService query&&CurrentNode is WorkflowNodeBase node)
        {
            var names=query.GetAvailableDataSources(node.Id,null,true).Where(source=>!source.SourceNodeId.StartsWith("__",StringComparison.Ordinal))
                .GroupBy(source=>source.SourceNodeId).ToDictionary(group=>group.Key,group=>group.First().SourceNodeName);
            foreach(var id in query.BindingCandidateNodeIds?.Invoke(node.Id)??names.Keys.ToArray())
                foreach(var output in query.FormalOutputContractReader?.Invoke(id)??Array.Empty<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable>())
                {
                    if(output.Kind!=SunEyeVision.Plugin.SDK.Execution.Results.OutputVariableKind.Data)continue;
                    Port source;
                    try{source=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
                    var label=(names.GetValueOrDefault(id)??id)+" · "+(output.DisplayName??output.Name);
                    candidates.Add(new(label,id,query.WorkflowId,source,Array.Empty<string>()));
                    AddMembers(source.Type,Array.Empty<string>(),label,id,source,query.WorkflowId);
                }
        }
        void AddMembers(TypeRef type,IReadOnlyList<string> path,string label,string nodeId,Port root,string? scope)
        {
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape!=ValueShape.Record)return;
            foreach(var field in type.Fields)
            {
                var next=path.Append(field.Id).ToArray();var nextLabel=label+" / "+field.Id;
                candidates.Add(new(nextLabel,nodeId,scope,root,next));AddMembers(field.Type,next,nextLabel,nodeId,root,scope);
            }
        }
        var picker=new ReferenceSelectionControl(candidates,reference=>
        {
            if(CurrentNode is not WorkflowNodeBase current||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext context)
                return "当前节点编辑范围尚未就绪。";
            var field=_accessors.Single(field=>field.Port.Id==target.Id);var prior=current.ParamSettings.GetSetting(field.StoragePath)?.Clone();
            try
            {
                current.ParamSettings.SetSetting(ToSetting(field,reference));
                return context.TryValidateEditor(out var error)?null:error;
            }
            finally{if(prior is null)current.ParamSettings.RemoveSetting(field.StoragePath);else current.ParamSettings.SetSetting(prior);}
        });
        var panel=new DockPanel();var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(12)};
        DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);panel.Children.Add(picker);
        var window=new Window {Title="选择绑定来源",Width=650,Height=520,Content=panel,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Window.GetWindow(this)};
        ReferenceSetting? selected=null;
        var accept=new Button {Content="确定",IsDefault=true,MinWidth=80,Margin=new Thickness(4)};var cancel=new Button {Content="取消",IsCancel=true,MinWidth=80,Margin=new Thickness(4)};
        accept.Click+=(_,_)=>{if(picker.TryGetReference(out var reference)){selected=new(reference!);window.DialogResult=true;}};
        buttons.Children.Add(accept);buttons.Children.Add(cancel);window.ShowDialog();return selected;
    }
}

public static class ToolConfigurationEditors
{
    public static FrameworkElement? CreateControl(IToolPlugin tool)
    {
        if(ToolRegistry.CreateEditorFactory(tool) is { } factory)return factory.CreateDebugControl(tool);
        var id=tool.GetType().GetCustomAttributes(typeof(ToolAttribute),false).OfType<ToolAttribute>().SingleOrDefault()?.Id;
        return ToolRegistry.GetComponentMetadata().Any(c=>c.ToolId==id&&c.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime)
            ?new DeclaredToolConfigurationControl(tool):null;
    }
}
