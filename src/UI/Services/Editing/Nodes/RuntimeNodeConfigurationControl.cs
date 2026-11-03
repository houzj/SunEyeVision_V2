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
    private IToolSettingsView? _view;
    private ReferenceWindowOwner? _referenceOwner;
    private readonly List<DeclaredFieldEditor> _inputs = new();
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
    { node = (NodeDefinition)_context!.FormalNode; generation = _generation; return _context is not null && IsResultDisplayBinding(node, generation); }

    public void BindDraft(NodeConfigurationEditDraft draft)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var context = ToolEditorCommit.GetDraftContext(this) as ProductionEditorContext ?? throw new InvalidOperationException("参数页未绑定正式编辑范围。");
        if (!context.IsValid || !ReferenceEquals(context.PreparedDraft, draft)) throw new InvalidOperationException("草稿不属于当前参数页。");
        ReleaseView(); _context = context;
        var generation = ++_generation;
        bool Current() => !_disposed && generation == _generation && context.IsValid && ReferenceEquals(_context, context);
        var admission = context.Session.AdmitPresentation(draft.WorkflowId, draft.Node.Id);
        _referenceOwner = new ReferenceWindowOwner(this, Current, context.ReferencePreference, context.ReferencePreference.PreferExpanded);
        ReferenceSelections Selection(Port port)
        { if (!Current()) throw new InvalidOperationException("引用选择已失效。"); return context.Session.CaptureReferenceSelections(draft.WorkflowId, draft.Node, port.Id); }
        IReadOnlyList<DeclaredReferenceCandidate> Candidates(ReferenceSelections selection)
        {
            var result = new List<DeclaredReferenceCandidate>();
            foreach (var source in selection.Outputs)
                foreach (var output in source.Schema.Outputs)
                    result.AddRange(DeclaredReferenceCandidateBuilder.Build(source.Schema, output, source.Actual.Id, draft.WorkflowId, source.Actual.Name));
            return result.AsReadOnly();
        }
        var references = new ReferenceFieldOptions(port =>
        {
            var selection = Selection(port); var sources = Candidates(selection);
            return new ReferenceCandidates(sources, sources.Where(candidate => ParameterReferenceRules.CanUseIndexSource(candidate.SelectedType, selection.ReferenceBehavior)),
                selection.Variables.Select(variable => new VariableSelectionItem(variable.Name, variable.Group, variable.Index, variable.Description, "", variable.Reference)), selection.ReferenceBehavior);
        }, EditableIndex: true, WindowOwner: _referenceOwner,
            ReadCurrentValue: port => CurrentContext().ReadCurrentParameter(port.Id),
            ReadBehavior: port => Selection(port).ReferenceBehavior);
        var inputSchema = draft.InputSchema;
        var panel = new DeclaredInputFieldsView(inputSchema,
            port => CurrentContext().PreparedDraft.Configuration.Fields.Read(port.Id),
            (port, value) => { if (!Current()) throw new InvalidOperationException("输入编辑已失效。"); CurrentContext().PreparedDraft.ApplyFields(new Dictionary<string, ParameterSetting> { [port.Id] = value }); },
            references, FormatReference, _ => inputSchema.Inputs.Where(port => (port.EditModes & FieldEditModes.Reference) != 0).SelectMany(port => Candidates(Selection(port))).ToArray());
        FrameworkElement inputContent = panel;
        // The host capability is read from the actual admitted node; tool-name strings carry no authority.
        var declaration = context.Session.GetNodeHostInputDeclaration(draft.WorkflowId, draft.Node);
        if (declaration is not null)
        {
            panel.Dispose();
            inputContent = new ImageSourceDraftContent(context.Session, context.Session.GetWorkflows().Single(flow => flow.Id == draft.WorkflowId && !flow.IsSubroutine), draft.Node, () => Current() ? context.PreparedDraft : null, Current);
        }
        else _inputs.AddRange(panel.Fields);
        var editorPorts = draft.SettingsSchema.Inputs.Concat(draft.DisplaySchema.Inputs).ToArray();
        IToolSettingsView? candidate = null;
        try
        {
            candidate = DesktopToolPresentation.CreateSettingsView(admission, draft.Configuration.Fields.Select(editorPorts),
                new ToolSettingsViewOptions(references, FormatReference, inputSchema.Presentation?.Fields.FirstOrDefault()?.Layout?.Page, inputContent));
            if (!Current()) throw new InvalidOperationException("参数页创建期间节点已失效。");
            _view = candidate; _syncedRevision = candidate.AcceptedRevision; _captureError = "";
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
        return (source?.Name ?? "已选择来源") + " · " + reference.PortId;
    }
    private void Changed()
    {
        if (_disposed || _capturing || _view is not { } view) return;
        try
        {
            if (view.AcceptedRevision != _syncedRevision)
            {
                var fields = view.CaptureAcceptedFields();
                CurrentContext().PreparedDraft.ApplyFields(fields.Declarations.Keys.ToDictionary(id => id, fields.Read, StringComparer.Ordinal));
                _syncedRevision = view.AcceptedRevision; _captureError = "";
            }
        }
        catch (Exception error) { _captureError = error.Message; }
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
            context.PreparedDraft.ApplyFields(fields.Declarations.Keys.ToDictionary(id => id, fields.Read, StringComparer.Ordinal));
            _syncedRevision = view.AcceptedRevision; _captureError = ""; error = ""; return true;
        }
        catch (Exception failure) { error = _captureError = failure.Message; return false; }
        finally { _capturing = false; }
    }
    public bool TryValidateEditor(out string error) => CurrentContext().TryValidateEditor(out error);
    public bool TryCommitEditor(out string error) => CurrentContext().TryCommitEditor(out error);
    public Task RunDebugAsync()
    {
        const string error = "正式节点调试输入和执行接缝尚未接入，当前不可执行。";
        ExecutionCompleted?.Invoke(this, new RuntimeToolExecutionEventArgs(null, error));
        return Task.FromException(new InvalidOperationException(error));
    }
    internal void RefreshRoiFrame() { }
    private void ReleaseView()
    {
        var view = _view; _view = null; Content = null; _inputs.Clear();
        try { if (view is not null) { view.Changed -= Changed; view.Dispose(); } }
        finally { _referenceOwner?.Dispose(); _referenceOwner = null; }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _generation++; ReleaseView(); _context = null; }
}
