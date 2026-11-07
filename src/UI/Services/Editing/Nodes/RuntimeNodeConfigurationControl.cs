using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Formal document draft adapter for the original shared parameter composition.</summary>
public sealed class RuntimeNodeConfigurationControl : UserControl, IToolEditorPendingState, IToolEditorCommitParticipant, IDisposable
{
    private ProductionEditorContext? _context;
    private ProductionEditorDebug? _debug;
    private SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameSource? _roiFrames;
    private Task? _debugRetirement;
    internal event Action? DebugStateChanged;
    internal bool IsDebugBusy => _debug?.IsBusy == true;
    internal bool CanDebug => !_disposed && _debug?.CanRun == true;
    internal Guid? DebugScopeId => _debug?.ScopeId;
    internal SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot? CurrentDebugSnapshot => _debug?.CurrentSnapshot;
    internal void ConfigureDebug(ProductionEditorContext context, Func<IHostInputFactory> factory, Func<bool> canRun, Action<string> log, Func<string,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?> borrowFrame, Action changed)
    {
        if (_debug is not null) throw new InvalidOperationException("参数页已拥有调试会话。");
        _debug = new ProductionEditorDebug(context,this,factory,canRun,log,()=>{DebugStateChanged?.Invoke();changed();});
        _roiFrames = new(borrowFrame,CaptureDebugInputs);
    }
    internal SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiInputLease CaptureDebugInputs(IReadOnlyCollection<string> fields) => (_debug ?? throw new InvalidOperationException("参数调试尚未就绪。")).CaptureInputs(fields);
    internal Task CloseDebugAsync() => _debugRetirement ??= _debug?.DisposeAsync().AsTask() ?? Task.CompletedTask;
    internal void StopDebug() => _debug?.Stop();
    internal void PublishDebugCompletion(SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot? result,string? error) => ExecutionCompleted?.Invoke(this,new RuntimeToolExecutionEventArgs(result,error));
    private IToolSettingsView? _view;
    private FieldSettings? _acceptedFields;
    private ImageSourceDraftContent? _imageSourceContent;
    private ReferenceWindowOwner? _referenceOwner;
    private readonly List<DeclaredFieldEditor> _inputs = new();
    /// <summary>引用来源快照按字段缓存：一次参数页构建只抓一次图，字段刷新与切页不再重走前驱与准入校验。</summary>
    private readonly Dictionary<string, ReferenceSelections> _referenceCache = new(StringComparer.Ordinal);
    private long _referenceRevision = -1;
    private long _generation, _syncedRevision;
    private bool _disposed, _capturing;
    private string _captureError = "";
    public event EventHandler<RuntimeToolExecutionEventArgs>? ExecutionCompleted;
    internal event Action? ResultDisplayEdited;
    internal event Action<NodeDefinition, long>? ResultDisplayBound;
    public bool HasPendingEdits => _view is { } view && (view.HasPendingEdits || view.HasInvalidInput || view.AcceptedRevision != _syncedRevision || _captureError.Length != 0) || _inputs.Any(input => input.HasPendingEdits);
    private ProductionEditorContext CurrentContext() => !_disposed && _context is { IsValid: true } context && ReferenceEquals(ToolEditorCommit.GetDraftContext(this), context)
        ? context : throw new InvalidOperationException("参数页所属方案、流程或节点已失效。");
    internal bool IsResultDisplayBinding(NodeDefinition node, long generation) => !_disposed && generation == _generation && _view is not null && ReferenceEquals(_context?.FormalNode, node) && ReferenceEquals(Content, _view.View);
    internal bool TryGetResultDisplayBinding(out NodeDefinition node, out long generation)
    { node = _context?.FormalNode as NodeDefinition ?? null!; generation = _generation; return node is not null && IsResultDisplayBinding(node, generation); }

    public void BindDraft(NodeConfigurationEditDraft draft)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var assemblyTrace = ToolWindowAssemblyTrace.Begin("BindDraft", draft.Node.Id.ToString());
        var context = ToolEditorCommit.GetDraftContext(this) as ProductionEditorContext ?? throw new InvalidOperationException("参数页未绑定正式编辑范围。");
        if (!context.IsValid || !ReferenceEquals(context.PreparedDraft, draft)) throw new InvalidOperationException("草稿不属于当前参数页。");
        _debug?.InvalidateDraft(); ReleaseView(); _context = context;
        var generation = ++_generation;
        bool Current() => !_disposed && generation == _generation && context.IsValid && ReferenceEquals(_context, context);
        var admission = context.Session.AdmitPresentation(draft.WorkflowId, draft.Node.Id);
        using var presentationTrace = ToolWindowAssemblyTrace.Begin("CreatePresentation", draft.Node.Id.ToString());
        _referenceOwner = new ReferenceWindowOwner(this, Current, context.ReferencePreference, context.ReferencePreference.PreferExpanded);
        ReferenceSelections Selection(Port port)
        {
            if (!Current()) throw new InvalidOperationException("引用选择已失效。");
            var revision = context.Session.ChangeRevision;
            if (revision != _referenceRevision) { _referenceCache.Clear(); _referenceRevision = revision; }
            if (_referenceCache.TryGetValue(port.Id, out var cached)) return cached;
            var captured = context.Session.CaptureReferenceSelections(draft.WorkflowId, draft.Node, port.Id);
            _referenceCache[port.Id] = captured;
            return captured;
        }
        IReadOnlyList<DeclaredReferenceCandidate> Candidates(ReferenceSelections selection)
        {
            var result = new List<DeclaredReferenceCandidate>();
            foreach (var source in selection.Outputs)
                foreach (var output in source.Schema.Outputs)
                {
                    var candidates=DeclaredReferenceCandidateBuilder.Build(source.Schema,output,source.Actual.Id,draft.WorkflowId,ModuleName(source.Actual),ReferenceText.ModuleNumber(source.Actual.GlobalIndex));
                    var manual=selection.FieldKind==ReferenceSelectionFieldKind.Settings&&selection.TargetPort.Type.Shape==ValueShape.Scalar
                        &&selection.TargetPort.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64;
                    result.AddRange(manual?candidates.Where(candidate=>
                    {
                        var reference=candidate.Reference(BindingSelection.All);
                        return source.ManualSources.Any(eligible=>eligible.PortId==reference.PortId&&eligible.MemberPath.SequenceEqual(reference.MemberFieldPath,StringComparer.Ordinal));
                    }):candidates);
                }
            return result.AsReadOnly();
        }
        var references = new ReferenceFieldOptions(port =>
        {
            if((port.EditModes&(FieldEditModes.Reference|FieldEditModes.DisplayExpression))==0)return ReferenceCandidates.Empty;
            var selection = Selection(port); var sources = Candidates(selection);
            return new ReferenceCandidates(sources, sources.Where(candidate => ParameterReferenceRules.CanUseIndexSource(candidate.SelectedType, selection.ReferenceBehavior)),
                selection.Variables.Select(variable => new VariableSelectionItem(variable.Name, variable.Group, variable.Index, variable.Description, "", variable.Reference)), selection.ReferenceBehavior);
        }, ValidateProposal:(port,setting)=>ValidateChanges(new Dictionary<string,ParameterSetting>{{port.Id,setting}}), EditableIndex: true, WindowOwner: _referenceOwner,
            ReadCurrentValue: port => CurrentContext().ReadCurrentParameter(port.Id),
            ReadCurrentValues: () => CurrentContext().ReadCurrentParameters(),
            ReadBehavior: port => (port.EditModes&(FieldEditModes.Reference|FieldEditModes.DisplayExpression))==0
                ?ParameterReferenceBehavior.StrictExactlyOne:Selection(port).ReferenceBehavior);
        var inputSchema = draft.InputSchema;
        // Resolve actual capability before constructing input content. Only one input view is built.
        var declaration = context.Session.GetNodeHostInputDeclaration(draft.WorkflowId,draft.Node);
        FrameworkElement? inputContent=null;
        Func<DeclaredFieldsView,FrameworkElement>? inputFactory=null;
        FrameworkElement CreateInputContent(DeclaredFieldsView pageHost)
        {
            if(!Current())throw new InvalidOperationException("输入页创建期间编辑会话已失效。");
            var panel=new DeclaredInputFieldsView(inputSchema,
                port=>CurrentContext().PreparedDraft.Configuration.Fields.Read(port.Id),
                (port,value)=>{if(!Current())throw new InvalidOperationException("输入编辑已失效。");var edits=new Dictionary<string,ParameterSetting>{{port.Id,value}};var error=ValidateChanges(edits);if(error is not null)throw new InvalidOperationException(error);CurrentContext().PreparedDraft.ApplyFields(edits);},
                references,FormatReference,port=>Candidates(Selection(port)));
            _inputs.Clear();_inputs.AddRange(panel.Fields);return panel;
        }
        if(declaration is { CapabilityId:"image-source",Version:1 })
            inputContent=_imageSourceContent=new ImageSourceDraftContent(context.Session,
                context.Session.GetWorkflows().Single(flow=>flow.Id==draft.WorkflowId&&!flow.IsSubroutine),draft.Node,
                ()=>Current()?context.PreparedDraft:null,Current);
        else inputFactory=CreateInputContent;
        var textExpressions=draft.DisplaySchema.Inputs.Where(port=>(port.EditModes & FieldEditModes.DisplayExpression)!=0).ToDictionary(port=>port.Id,port=>
            new SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionOptions(()=>
            {
                var selection=Selection(port);var revision=context.Session.ChangeRevision;
                var candidates=Candidates(selection).Where(candidate=>
                {
                    var type=candidate.SelectedType;while(type.Shape==ValueShape.Sequence)type=type.Element!;
                    return type.Shape==ValueShape.Scalar&&type.Scalar is ScalarKind.Int64 or ScalarKind.Float64 or ScalarKind.Text;
                }).ToArray();
                return new SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionSnapshot(candidates,
                    candidate=>candidate.NodeDisplayName+"."+candidate.OutputDisplayName+(candidate.MemberDisplayNames.Count==0?"":"."+string.Join(".",candidate.MemberDisplayNames)),
                    ()=>Current()&&context.Session.ChangeRevision==revision);
            }),StringComparer.Ordinal);
        var editorPorts = draft.SettingsSchema.Inputs.Concat(draft.DisplaySchema.Inputs).ToArray();
        IToolSettingsView? candidate = null;
        try
        {
            candidate = DesktopToolPresentation.CreateSettingsView(admission, draft.Configuration.Fields.Select(editorPorts),
                new ToolSettingsViewOptions(references, FormatReference, inputSchema.Presentation?.Fields.FirstOrDefault()?.Layout?.Page, inputContent, ValidateComposition:group=>ValidateChanges(group.Changes), TextExpressions:textExpressions, RoiFrames:_roiFrames, InputContentFactory:inputFactory,
                    PageBuilt:(page,_,_)=>ToolWindowAssemblyTrace.Event("PageBuilt", $"{draft.Node.Id}:{page}")));
            if (!Current()) throw new InvalidOperationException("参数页创建期间节点已失效。");
            _view = candidate; _acceptedFields=candidate.CaptureAcceptedFields(); _syncedRevision = candidate.AcceptedRevision; _captureError = "";
            candidate.Changed += Changed; Content = candidate.View;
            ResultDisplayBound?.Invoke(draft.Node, generation);
        }
        catch
        {
            candidate?.Dispose();
            if (candidate is null && inputContent is IDisposable disposable) disposable.Dispose();
            _inputs.Clear(); _view = null; _referenceOwner?.Dispose(); _referenceOwner = null;
            throw;
        }
    }
    private string FormatReference(OutputReference reference)
    {
        var context = CurrentContext();
        var source = context.Session.GetNodes(context.PreparedDraft.WorkflowId).SingleOrDefault(node => node.Id == reference.NodeId);
        if (source is null) throw new InvalidOperationException("引用的来源节点已不在当前工作流中。");
        var schema = context.Session.GetNodeOutputSchema(context.PreparedDraft.WorkflowId, source.Id)
            ?? throw new InvalidOperationException("引用的来源节点输出声明已不可用。");
        var display = schema.FindOutputDisplay(reference.PortId);
        var caption = string.IsNullOrWhiteSpace(display?.DisplayName) ? reference.PortId : display.DisplayName;
        return ReferenceText.Format(ReferenceText.ModuleNumber(source.GlobalIndex), ModuleName(source), caption,
            reference.MemberFieldPath, DeclaredReferenceCandidateBuilder.MemberNames(display, reference.MemberFieldPath));
    }
    private static string ModuleName(NodeDefinition node)
        => string.IsNullOrWhiteSpace(node.DispName) ? node.Name : node.DispName;
    private void Changed()
    {
        if (_disposed || _capturing || _view is not { } view) return;
        try
        {
            if (view.AcceptedRevision != _syncedRevision)
            {
                var fields = view.CaptureAcceptedFields();
                ApplyAcceptedFields(fields);
                _syncedRevision = view.AcceptedRevision; _captureError = "";
            }
        }
        catch (Exception error) { _captureError = error.Message; }
        _debug?.InvalidateDraft();
        ResultDisplayEdited?.Invoke();
    }
    public bool TryCapture(out string error)
    {
        if (_capturing) { error = "参数捕获不能重入。"; return false; }
        _capturing = true;
        try
        {
            var context = CurrentContext(); var generation = _generation;
            foreach (var input in _inputs)
                if (!input.TryCommitPending()) { error = input.Error; return false; }
            if (_view is not { } view) throw new InvalidOperationException("参数视图尚未就绪。");
            if (!view.TryCapture(out var fields, out error)) return false;
            if (generation != _generation || !ReferenceEquals(context, CurrentContext())) throw new InvalidOperationException("捕获期间参数页已重新绑定。");
            ApplyAcceptedFields(fields);
            _syncedRevision = view.AcceptedRevision; _captureError = ""; error = ""; return true;
        }
        catch (Exception failure) { error = _captureError = failure.Message; return false; }
        finally { _capturing = false; }
    }
    internal void AdoptRebasedDraft(NodeConfigurationEditDraft draft)
    {
        if(!ReferenceEquals(CurrentContext().PreparedDraft,draft))throw new InvalidOperationException("回执草稿不属于当前视图。");
        _debug?.InvalidateDraft();
        _imageSourceContent?.RebaseDraft(draft);
    }
    private void ApplyAcceptedFields(FieldSettings fields)
    {
        static string Identity(string id,ParameterSetting value)
        {
            using var stream=new System.IO.MemoryStream();
            using(var writer=new System.Text.Json.Utf8JsonWriter(stream))ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{id,value}});
            return Convert.ToBase64String(stream.ToArray());
        }
        var previous=_acceptedFields;
        var changes=(_view?.AcceptedFieldOrder??Array.Empty<string>()).Concat(fields.Declarations.Keys).Distinct(StringComparer.Ordinal).Where(id=>previous is null||!previous.Declarations.ContainsKey(id)
            ||Identity(id,previous.Read(id))!=Identity(id,fields.Read(id))).ToDictionary(id=>id,fields.Read,StringComparer.Ordinal);
        // Accepted controls do not get to overwrite unrelated newer fields after a receipt.
        if(changes.Count!=0)
        {
            var context=CurrentContext();var order=BindingOrder(context.PreparedDraft,changes);
            context.Host.ValidateFieldProposals(context.PreparedDraft,changes,order);
            context.PreparedDraft.ApplyFields(changes,order);
        }
        _acceptedFields=fields;
    }
    private string? ValidateChanges(IReadOnlyDictionary<string,ParameterSetting> changes)
    {
        try
        {
            var context=CurrentContext();context.Host.ValidateFieldProposals(context.PreparedDraft,changes,BindingOrder(context.PreparedDraft,changes));return null;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException){return error.Message;}
    }
    private static IReadOnlyList<string> BindingOrder(NodeConfigurationEditDraft draft,IReadOnlyDictionary<string,ParameterSetting> changes)
    {
        static string Identity(string id,ParameterSetting value)
        {
            using var stream=new System.IO.MemoryStream();using(var writer=new System.Text.Json.Utf8JsonWriter(stream))
                ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{id,value}});
            return Convert.ToBase64String(stream.ToArray());
        }
        var current=draft.Configuration;var fields=current.Fields.Apply(changes);
        var parameters=draft.SettingsSchema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        bool Bound(string id)=>parameters.Contains(id)&&fields.Read(id) is ReferenceSetting or VariableSetting;
        var retained=current.ParameterBindingOrder.Where(id=>Bound(id)&&Identity(id,current.Fields.Read(id))==Identity(id,fields.Read(id))).ToList();
        foreach(var id in changes.Keys.Concat(fields.Capture().Keys).Distinct(StringComparer.Ordinal))if(Bound(id)&&!retained.Contains(id,StringComparer.Ordinal))retained.Add(id);
        return retained;
    }
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
    {
        VerifyAccess();var context=CurrentContext();var generation=_generation;
        var view=_view??throw new InvalidOperationException("参数视图尚未就绪。");
        var result=view.ReadNumericModels(scope,fieldId);
        if(generation!=_generation||!ReferenceEquals(view,_view)||!ReferenceEquals(context,CurrentContext()))
            throw new InvalidOperationException("数值回读期间参数页已重新绑定。");
        return result;
    }
    public bool TryValidateEditor(out string error) => CurrentContext().TryValidateEditor(out error);
    public bool TryCommitEditor(out string error) => CurrentContext().TryCommitEditor(out error);
    public Task RunDebugAsync() => (_debug ?? throw new InvalidOperationException("参数调试尚未接入实际会话。")).RunAsync();
    internal void RefreshRoiFrame() => _roiFrames?.Refresh();
    private void ReleaseView()
    {
        ToolWindowAssemblyTrace.Event("ReleaseView", (_context?.FormalNode as NodeDefinition)?.Id.ToString() ?? "none");
        var view = _view; _view = null; _acceptedFields=null; _imageSourceContent=null; Content = null; _inputs.Clear();
        try { if (view is not null) { view.Changed -= Changed; view.Dispose(); } }
        finally { _referenceOwner?.Dispose(); _referenceOwner = null; _referenceCache.Clear(); _referenceRevision = -1; }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _generation++; _ = CloseDebugAsync(); ReleaseView(); _context = null; }
}
