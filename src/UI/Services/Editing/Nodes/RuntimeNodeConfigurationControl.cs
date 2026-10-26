using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Workflow;
using ConfigurationSetting=SunEyeVision.Plugin.Contracts.ParameterSetting;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Document adapter for the actual Runtime settings view. No SDK parameter
/// model or algorithm is created. The existing document participant owns publication.</summary>
public sealed class RuntimeNodeConfigurationControl : UserControl,IToolEditorPendingState,IToolEditorCommitParticipant,IDisposable
{
    private WorkflowNodeBase _node=null!;
    private IToolSettingsView? _view;
    private DataSourceQueryService? _query;
    private readonly List<DeclaredFieldEditor> _inputs=new();
    private bool _syncing,_disposed;
    private long _generation;
    public bool HasPendingEdits=>_view?.HasInvalidInput==true||_inputs.Any(editor=>editor.HasInvalidText);
    public RuntimeNodeConfigurationControl(WorkflowNodeBase node)
    {
        BindDraft(node.Clone());
        Loaded+=(_,_)=>
        {
            if(Window.GetWindow(this) is Plugin.SDK.UI.Windows.DefaultDebugWindow window)
            {window.ApplyCompactParameterLayout();window.Resources.MergedDictionaries.Add(Plugin.Wpf.EditorResources.Create());}
        };
    }
    public void SetDataProvider(DataSourceQueryService query)=>_query=query;
    public void BindDraft(WorkflowNodeBase node)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var generation=++_generation;
        if(_view is not null){_view.Changed-=Changed;Content=null;_view.Dispose();}
        _node=node;_inputs.Clear();
        var configuration=node.RuntimeConfiguration??throw new ArgumentException("Runtime configuration is required.");
        var settings=configuration.SettingsSchema??throw new ArgumentException("Runtime settings metadata is required.");
        var references=node.ParamSettings.Settings.Where(s=>settings.Inputs.Any(p=>p.Id==s.ParameterName)&&s.ReferenceExpression is not null)
            .ToDictionary(s=>s.ParameterName,s=>new ReferenceSetting(BindingReferenceSerializer.Deserialize(s.ReferenceExpression!)),StringComparer.Ordinal);
        var schema=configuration.Schema;
        if(schema.Presentation is null)throw new ArgumentException("Runtime input presentation metadata is required.");
        var panel=new StackPanel();
        foreach(var group in schema.Presentation.Fields.GroupBy(f=>f.Layout!.Section))
        {
            panel.Children.Add(new TextBlock{Text=group.Key,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,10)});
            foreach(var field in group)
            {
                var port=schema.Inputs.Single(p=>p.Id==field.FieldId);
                var saved=node.ParamSettings.GetSetting(port.Id);
                ConfigurationSetting value=saved?.ReferenceExpression is { } expression?new ReferenceSetting(BindingReferenceSerializer.Deserialize(expression)):new UnsetSetting();
                var editor=new DeclaredFieldEditor(port with{Required=port.Required&&schema.WorkflowImageInputId!=port.Id,EditModes=FieldEditModes.Reference},
                    field.DisplayName,value,setting=>WriteInput(port,setting,generation),target=>SelectReference(target,generation),formatReference:FormatReference,
                    presentation:field,allowUnboundReference:!port.Required||schema.WorkflowImageInputId==port.Id);
                _inputs.Add(editor);panel.Children.Add(editor);
            }
        }
        _view=ToolRegistry.CreateSettingsView(configuration.ToolId,configuration.CaptureSettings(),
            new(references,target=>SelectReference(target,generation),FormatReference,schema.Presentation.Fields.First().Layout!.Page,panel));
        _view.Changed+=Changed;Content=_view.View;
    }
    private void Changed(){if(!_syncing)TryCapture(out _);}
    public bool TryValidateEditor(out string error)
    {
        if(ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
        {error="当前编辑会话尚未就绪或已经失效。";return false;}
        if(!TryCapture(out error))return false;
        try{context.Host.Validate(context.PreparedDraft);return true;}
        catch(InvalidOperationException exception){error=exception.Message;return false;}
    }
    public bool TryCommitEditor(out string error)
    {
        if(!TryValidateEditor(out error))return false;
        return ((ProductionEditorContext)ToolEditorCommit.GetDraftContext(this)!).TryCommitEditor(out error);
    }
    public bool TryCapture(out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_inputs.FirstOrDefault(f=>f.HasInvalidText) is { } invalid){error=invalid.Error;return false;}
        if(_view is null){error="配置尚未初始化。";return false;}
        if(!_view.TryCapture(out var settings,out var references,out error))return false;
        _syncing=true;
        try
        {
            var next=_node.RuntimeConfiguration!.WithSettings(settings);
            var bindings=_node.ParamSettings.Clone();
            foreach(var port in next.SettingsSchema!.Inputs)
                if(references.TryGetValue(port.Id,out var reference))bindings.SetSetting(ToSetting(port.Id,reference.Reference));
                else bindings.RemoveSetting(port.Id);
            _node.RuntimeConfiguration=next;_node.ParamSettings=bindings;error="";return true;
        }
        catch(Exception exception)when(exception is ArgumentException or InvalidOperationException){error=exception.Message;return false;}
        finally{_syncing=false;}
    }
    private ParamSetting ToSetting(string id,OutputReference reference)
    {
        var expression=BindingReferenceSerializer.Serialize(reference);var prior=_node.ParamSettings.GetSetting(id);
        return new(){ParameterName=id,BindingType=BindingType.Binding,ReferenceExpression=expression,SourceNodeId=reference.NodeId,
            SourceWorkflowId=reference.ScopeId,SourceProperty=reference.PortId,
            Conversion=prior?.ReferenceExpression==expression?prior.Conversion:BindingConversion.Automatic};
    }
    private void WriteInput(Port port,ConfigurationSetting setting,long generation)
    {
        if(_disposed||generation!=_generation)throw new InvalidOperationException("来源编辑已失效，请使用当前参数页。");
        if(ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true})
            throw new InvalidOperationException("当前编辑范围尚未就绪或已经失效。");
        if(setting is ReferenceSetting reference)_node.ParamSettings.SetSetting(ToSetting(port.Id,reference.Reference));
        else if(setting is UnsetSetting)_node.ParamSettings.RemoveSetting(port.Id);
        else throw new ArgumentException("运行输入必须选择已声明来源。");
    }
    private string FormatReference(OutputReference reference)
    {
        var source=_query?.GetAvailableDataSources(_node.Id,null,true).FirstOrDefault(s=>s.SourceNodeId==reference.NodeId);
        var output=_query?.FormalOutputContractReader?.Invoke(reference.NodeId)?.FirstOrDefault(p=>BindingClrAdapter.OutputPortId(p)==reference.PortId);
        return (source?.SourceNodeName??"已选择来源")+" · "+(output?.DisplayName??output?.Name??"已声明字段")
            +(reference.MemberFieldPath.Count==0?"":" / "+string.Join(" / ",reference.MemberFieldPath));
    }
    private ReferenceSetting? SelectReference(Port target,long generation)
    {
        if(_disposed||generation!=_generation||_query is null||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)return null;
        var candidates=new List<DeclaredReferenceCandidate>();
        var names=_query.GetAvailableDataSources(_node.Id,null,true).GroupBy(s=>s.SourceNodeId).ToDictionary(g=>g.Key,g=>g.First().SourceNodeName);
        foreach(var id in _query.BindingCandidateNodeIds?.Invoke(_node.Id)??names.Keys.ToArray())
        foreach(var output in _query.FormalOutputContractReader?.Invoke(id)??Array.Empty<OutputVariable>())
        {
            if(output.Kind!=OutputVariableKind.Data)continue;
            Port port;try{port=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
            var label=names.GetValueOrDefault(id,"可绑定节点")+" · "+(output.DisplayName??output.Name);
            candidates.Add(new(label,id,_query.WorkflowId,port,Array.Empty<string>()));
            AddMembers(port.Type,Array.Empty<string>(),label,id,port);
        }
        void AddMembers(TypeRef type,IReadOnlyList<string> path,string label,string id,Port root)
        {
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape!=ValueShape.Record)return;
            foreach(var member in type.Fields)
            {var next=path.Append(member.Id).ToArray();var name=label+" / "+member.Id;candidates.Add(new(name,id,_query.WorkflowId,root,next));AddMembers(member.Type,next,name,id,root);}
        }
        var picker=new ReferenceSelectionControl(candidates,reference=>
        {
            if(_disposed||generation!=_generation||!context.IsValid)return "来源编辑已失效，请重新打开选择窗口。";
            var prior=_node.ParamSettings.GetSetting(target.Id)?.Clone();
            try{_node.ParamSettings.SetSetting(ToSetting(target.Id,reference));context.Host.Validate(context.PreparedDraft);return null;}
            catch(Exception error)when(error is ArgumentException or InvalidOperationException){return error.Message;}
            finally{if(prior is null)_node.ParamSettings.RemoveSetting(target.Id);else _node.ParamSettings.SetSetting(prior);}
        });
        var body=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(12)};
        DockPanel.SetDock(buttons,Dock.Bottom);body.Children.Add(buttons);body.Children.Add(picker);
        var window=new Window{Title="选择绑定来源",Width=650,Height=520,Content=body,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner};
        ReferenceSetting? result=null;var accept=new Button{Content="确定",IsDefault=true,MinWidth=80,Margin=new Thickness(4)};
        accept.Click+=(_,_)=>{if(picker.TryGetReference(out var reference)){result=new(reference!);window.DialogResult=true;}};
        buttons.Children.Add(accept);buttons.Children.Add(new Button{Content="取消",IsCancel=true,MinWidth=80,Margin=new Thickness(4)});
        window.ShowDialog();return !_disposed&&generation==_generation&&context.IsValid?result:null;
    }
    public void Dispose()
    {if(_disposed)return;_disposed=true;_generation++;if(_view is not null){_view.Changed-=Changed;Content=null;_view.Dispose();}_inputs.Clear();}
}
