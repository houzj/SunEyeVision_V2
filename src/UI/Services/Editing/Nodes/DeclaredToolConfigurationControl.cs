using SunEyeVision.Plugin.Wpf.Adapters.References;
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
public sealed partial class DeclaredToolConfigurationControl : ToolDebugControlBase, IToolEditorCommitParticipant, IToolEditorPendingState
{
    private readonly IToolPlugin _tool;
    private ToolParameters? _parameters;
    private long _configurationGeneration;
    private bool _acceptingFields;
    private bool _refreshAcceptedFields;
    public string? LastAcceptedRefreshError {get;private set;}
    private DeclaredFieldsView? _fields;
    private IReadOnlyList<ConfigurationFieldAccessor> _accessors=Array.Empty<ConfigurationFieldAccessor>();
    public bool HasPendingEdits=>_fields?.HasInvalidActiveFields==true||HasPendingRegions||HasPendingSources||HasPendingInheritedRegions;
    public override Size PreferredDebugWindowSize=>new(420,500);
    public DeclaredToolConfigurationControl(IToolPlugin tool)
    {
        _tool=tool;Loaded+=(_,_)=>
        {
            AttachRegionLifetime();
            if(_parameters is IConfigurationFieldProvider&&Window.GetWindow(this) is SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow window)
            {
                window.ApplyCompactParameterLayout();
                if(!window.Resources.Contains("O06.NativeResourcesInstalled"))
                {
                    window.Resources.MergedDictionaries.Add(SunEyeVision.Plugin.Wpf.EditorResources.Create());
                    window.Resources["O06.NativeResourcesInstalled"]=true;
                }
                foreach(var name in new[]{"ContinuousRunButton","RunButton","ConfirmButton"})
                    if(window.FindName(name) is Button button)button.SetResourceReference(StyleProperty,"Orange_ButtonStyle");
            }
        };
    }
    public override void SetParameters(ToolParameters parameters)
    {
        _configurationGeneration++;_refreshAcceptedFields=false;
        _fields?.Dispose();_fields=null;
        _roiViewport?.CancelGesture();
        foreach(var part in _regionParts)part.View.Dispose();
        _regionParts.Clear();_sourceFields.Clear();_sourceActivity.Clear();
        foreach(var part in _inheritedRegionEditors)part.Editor.Dispose();
        _inheritedRegionEditors.Clear();_inheritedRegionChecks.Clear();
        base.SetParameters(parameters);_parameters=parameters;
        var generation=_configurationGeneration;
        if(parameters is IConfigurationFieldProvider provider)
        {
            _accessors=provider.GetConfigurationFields();
            _fields=new(_accessors.Select(field=>(field.Port,field.Presentation,ReadDraft(field))),
                proposals=>WriteDraft(proposals,generation),CreateReferenceOptions() with {ValidateProposal=null},
                id=>new ConstantSetting(_accessors.Single(field=>field.Port.Id==id).Read()),FormatReference);
            foreach(var field in _fields.Fields)field.Changed+=OnAcceptedFieldNotification;
            SetEditorContent(_fields);BuildRegionEditors(provider);BuildSourceEditors(provider);return;
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
        _fields=new(fields,proposals=>AcceptParameterCandidate(generation,(candidate,bindings)=>
        {
            foreach(var (id,setting) in proposals)
            {
                if(!slots.TryGetValue(id,out var property)||setting is not ConstantSetting constant)
                    throw new ArgumentException("未声明字段或不支持的参数提案。");
                var port=fields.Single(field=>field.Port.Id==id).Port;
                var literal=ValueRules.Freeze(port,constant.Value);
                var value=property.PropertyType.IsEnum?Enum.Parse(property.PropertyType,(string)literal!):
                    BindingClrAdapter.Materialize(port,property.PropertyType,literal);
                property.SetValue(candidate,value);bindings.RemoveSetting(id);
            }
        }));
        foreach(var field in _fields.Fields)field.Changed+=OnAcceptedFieldNotification;
        SetEditorContent(_fields);
    }
    public bool TryValidateEditor(out string error)
    {
        if(HasPendingSources){error=ActiveSourceFields.First(view=>view.HasPendingEdits).Error; if(string.IsNullOrEmpty(error))error="请先应用来源选择，或撤销尚未应用的选择。";return false;}
        if(!ValidateRegionEditors(out error))return false;
        if(!ValidateInheritedRegionEditors(out error))return false;
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
        var nodeName=query?.GetBindingCandidateNodeNames((CurrentNode as WorkflowNodeBase)?.Id??"").GetValueOrDefault(reference.NodeId)??reference.NodeId;
        var member=reference.MemberFieldPath.Count>0?" / "+string.Join(" / ",DeclaredReferenceCandidateBuilder.MemberNames(source,reference.MemberFieldPath)):"";
        var selection=reference.Selection switch {BindingSelection.AtConstant index=>" ["+index.Index+"]",BindingSelection.AtField=>" [绑定序号]",_=>""};
        return nodeName+" · "+(source?.DisplayName??source?.Name??"已声明字段")+member+selection;
    }

    private ConfigurationSetting ReadDraft(ConfigurationFieldAccessor field)
    {
        if(CurrentNode is WorkflowNodeBase node&&node.ParamSettings.GetSetting(field.StoragePath)?.ReferenceExpression is { } expression)
            return new ReferenceSetting(BindingReferenceSerializer.Deserialize(expression));
        return new ConstantSetting(field.Read());
    }
    public override void SetCurrentNode(object node)
    {
        _configurationGeneration++;
        base.SetCurrentNode(node);
    }
    private void OnAcceptedFieldNotification()
    {
        if(!_refreshAcceptedFields)return;
        _refreshAcceptedFields=false;
        try{RefreshRegionScene();LastAcceptedRefreshError=null;}
        catch(Exception error){LastAcceptedRefreshError=error.Message;}
    }
    private void WriteDraft(IReadOnlyDictionary<string,ConfigurationSetting> proposals,long generation)
        =>AcceptParameterCandidate(generation,(candidate,bindings)=>
        {
            if(candidate is not IConfigurationFieldProvider provider)throw new InvalidOperationException("参数声明已改变。");
            var fields=provider.GetConfigurationFields();
            foreach(var (id,value) in proposals)
            {
                var field=fields.SingleOrDefault(field=>field.Port.Id==id)??throw new ArgumentException("未声明参数字段："+id);
                if(value is ConstantSetting constant){field.Write(ValueRules.Freeze(field.Port,constant.Value));bindings.RemoveSetting(field.StoragePath);}
                else if(value is ReferenceSetting reference)bindings.SetSetting(ToSetting(field,reference.Reference));
                else throw new ArgumentException("此字段不支持清除。");
                candidate.RebindingRequired.Remove(field.StoragePath);candidate.BindingValidationPending.Remove(field.StoragePath);
            }
        });
    private void AcceptParameterCandidate(long generation,
        Action<ToolParameters,ParamSettingContainer> prepare)
    {
        if(_acceptingFields)throw new InvalidOperationException("参数整组接纳正在进行，不能重入。");
        var original=_parameters??throw new InvalidOperationException("配置尚未初始化。");
        var fields=_fields;
        if(CurrentNode is not WorkflowNodeBase node||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext context)
            throw new InvalidOperationException("当前私有参数编辑范围尚未就绪。");
        bool SameBinding()=>generation==_configurationGeneration&&ReferenceEquals(_fields,fields)&&
            ReferenceEquals(CurrentNode,node)&&ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context);
        bool Current()=>SameBinding()&&ReferenceEquals(_parameters,original)&&context.IsValid&&ReferenceEquals(context.DraftNode,node);
        if(!Current()||!ReferenceEquals(node.Parameters,original))throw new InvalidOperationException("当前参数编辑范围已失效。");
        _acceptingFields=true;
        try
        {
            var candidate=ToolExecutionPreparation.CopyParameters(original);
            var priorBindings=node.ParamSettings;var nextBindings=priorBindings.Clone();
            prepare(candidate,nextBindings);
            var validation=candidate.Validate();
            if(!validation.IsValid)throw new ArgumentException(string.Join("；",validation.Errors));
            var nextAccessors=candidate is IConfigurationFieldProvider provider?provider.GetConfigurationFields():Array.Empty<ConfigurationFieldAccessor>();
            if(!Current()||!ReferenceEquals(node.Parameters,original)||!ReferenceEquals(node.ParamSettings,priorBindings))
                throw new InvalidOperationException("准备参数时编辑范围已改变。");
            try
            {
                node.Parameters=candidate;node.ParamSettings=nextBindings;
                context.Host.Validate(context.PreparedDraft);
                if(!Current()||!ReferenceEquals(node.Parameters,candidate)||!ReferenceEquals(node.ParamSettings,nextBindings))
                    throw new InvalidOperationException("校验参数时编辑范围已改变。");
            }
            finally
            {
                // Validation can synchronously rebind even the same node. Restore only our own temporary slots.
                if(SameBinding()||!ReferenceEquals(CurrentNode,node))
                {
                    if(ReferenceEquals(node.Parameters,candidate))node.Parameters=original;
                    if(ReferenceEquals(node.ParamSettings,nextBindings))node.ParamSettings=priorBindings;
                }
            }
            if(!Current()||!ReferenceEquals(node.Parameters,original)||!ReferenceEquals(node.ParamSettings,priorBindings))
                throw new InvalidOperationException("接纳参数时编辑范围已改变。");
            node.Parameters=candidate;node.ParamSettings=nextBindings;
            // Model installation is accepted. Its synchronous notification may have rebound the view;
            // an old acceptance must never overwrite the new page's parameter owner.
            if(!SameBinding()||!context.IsValid||!ReferenceEquals(context.DraftNode,node)||
                !ReferenceEquals(node.Parameters,candidate)||!ReferenceEquals(node.ParamSettings,nextBindings))return;
            _parameters=candidate;_accessors=nextAccessors;ReplacePreviewParameterOwner(candidate);
            _refreshAcceptedFields=true;
        }
        finally{_acceptingFields=false;}
    }
    private ParamSetting ToSetting(ConfigurationFieldAccessor field,OutputReference reference)
        =>ToSetting(field.StoragePath,reference);
    private ParamSetting ToSetting(string path,OutputReference reference)
    {
        var source=(DataProvider as DataSourceQueryService)?.FormalOutputContractReader?.Invoke(reference.NodeId)
            ?.SingleOrDefault(output=>BindingClrAdapter.OutputPortId(output)==reference.PortId);
        return new ParamSetting {ParameterName=path,BindingType=BindingType.Binding,
            ReferenceExpression=BindingReferenceSerializer.Serialize(reference),SourceNodeId=reference.NodeId,
            SourceWorkflowId=reference.ScopeId,SourceFieldId=source?.FieldId,SourceProperty=source?.Name??reference.PortId};
    }
    private ReferenceFieldOptions CreateReferenceOptions(Func<OutputReference,Port>? roiTarget=null)
    {
        var generation=_configurationGeneration;var node=CurrentNode;var context=ToolEditorCommit.GetDraftContext(this);
        bool Current()
        {
            // The host attaches its private context after constructing and binding the view.
            // Adopt that first context, then keep its identity for this binding generation.
            var actual=ToolEditorCommit.GetDraftContext(this);context??=actual;
            return generation==_configurationGeneration&&ReferenceEquals(CurrentNode,node)&&
                ReferenceEquals(actual,context)&&RegionScopeCurrent();
        }
        void EnsureCurrent(){if(!Current())throw new InvalidOperationException("当前来源编辑范围已失效。");}
        return new(_=>{EnsureCurrent();var candidates=ReadReferenceCandidates();EnsureCurrent();return new(candidates,
                candidates.Where(candidate=>candidate.Output.Type.Shape!=ValueShape.Sequence&&candidate.SelectedType==DataType.Integer));},
            (target,proposal)=>
            {
                EnsureCurrent();var error=ValidateReferenceProposal(target,proposal,roiTarget);EnsureCurrent();return error;
            });
    }
    private IReadOnlyList<DeclaredReferenceCandidate> ReadReferenceCandidates()
    {
        var candidates=new List<DeclaredReferenceCandidate>();
        if(DataProvider is DataSourceQueryService query&&CurrentNode is WorkflowNodeBase node)
        {
            var names=query.GetBindingCandidateNodeNames(node.Id);
            foreach(var id in names.Keys)
                foreach(var output in query.FormalOutputContractReader?.Invoke(id)??Array.Empty<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable>())
                {
                    if(output.Kind!=SunEyeVision.Plugin.SDK.Execution.Results.OutputVariableKind.Data)continue;
                    try{candidates.AddRange(DeclaredReferenceCandidateBuilder.Build(output,id,query.WorkflowId,names.GetValueOrDefault(id,"可绑定节点")));}
                    catch(ArgumentException){continue;}
                }
        }
        return candidates.AsReadOnly();
    }
    private string? ValidateReferenceProposal(Port target,ConfigurationSetting proposal,Func<OutputReference,Port>? roiTarget)
    {
        if(proposal is not ReferenceSetting selected)return null;
        var reference=selected.Reference;var candidates=ReadReferenceCandidates();
            if(roiTarget is not null)
            {
                if(!RegionScopeCurrent()||CurrentNode is not WorkflowNodeBase consumer)return "当前节点编辑范围已失效。";
                if(DataProvider is not DataSourceQueryService currentQuery)return "当前来源声明不可用。";
                var scopeId=currentQuery.WorkflowId??"";
                var visible=currentQuery.BindingCandidateNodeIds?.Invoke(consumer.Id)??candidates.Select(candidate=>candidate.NodeId).Distinct().ToArray();
                IEnumerable<Port> CurrentPorts(string id)
                {
                    foreach(var output in currentQuery.FormalOutputContractReader?.Invoke(id)??Array.Empty<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable>())
                    {
                        if(output.Kind!=SunEyeVision.Plugin.SDK.Execution.Results.OutputVariableKind.Data)continue;
                        Port port;
                        try{port=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
                        yield return port;
                    }
                }
                var sources=visible.Where(id=>id!=consumer.Id).Select(id=>new BindingSource(new(scopeId,id),Guid.Empty,
                    new ToolSchema(Array.Empty<Port>(),CurrentPorts(id)),0)).ToArray();
                var scope=new BindingScopeSnapshot(new(scopeId,consumer.Id),Guid.Empty,0,sources,sources.Select(source=>source.Node),Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
                var result=new ParameterBindingService().CompileBinding(reference,roiTarget(reference),scope);
                return result.Plan is not null?null:result.Diagnostic?.Message??"来源声明与区域参数不匹配。";
            }
            if(CurrentNode is not WorkflowNodeBase current||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext context)
                return "当前节点编辑范围尚未就绪。";
            var field=_accessors.Single(field=>field.Port.Id==target.Id);var prior=current.ParamSettings;
            current.ParamSettings=prior.Clone();
            try
            {
                current.ParamSettings.SetSetting(ToSetting(field,reference));
                return context.TryValidateEditor(out var error)?null:error;
            }
            finally{current.ParamSettings=prior;}
    }

}

public static class ToolConfigurationEditors
{
    public static FrameworkElement? CreateControl(IToolPlugin tool)
    {
        if(ToolRegistry.CreateEditorFactory(tool)?.CreateDebugControl(tool) is { } specialized)return specialized;
        var id=tool.GetType().GetCustomAttributes(typeof(ToolAttribute),false).OfType<ToolAttribute>().SingleOrDefault()?.Id;
        return ToolRegistry.GetComponentMetadata().Any(c=>c.ToolId==id&&c.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime)
            ?new DeclaredToolConfigurationControl(tool):null;
    }
}
