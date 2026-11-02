using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Workflow;

public partial class WorkflowExecutionManager
{
    private readonly LocalImageSequenceService _localImageSequences = new();
    private sealed record LocalSourcePresentation(ImageInputSource Source, long EditVersion);
    private sealed record LocalCursor(string Definition, string? EntryId, bool PendingSelection = false, WeakReference<ImageInputSource>? SelectionOwner = null);
    private sealed record LocalCursorOwner(WeakReference<SunEyeVision.Workflow.Workflow> Root, Dictionary<string, LocalCursor> Sources,
        Dictionary<string,long> ResetVersions, Dictionary<string,string> Blocked);
    private readonly Dictionary<string, LocalCursorOwner> _localImageCursors = new(StringComparer.Ordinal);
    private long _nextLocalImageReset;
    // Caller holds _lock. A replacement root never inherits another instance's cursors.
    private LocalCursorOwner GetLocalCursorOwner(string rootId,SunEyeVision.Workflow.Workflow root)
    {
        if(!_localImageCursors.TryGetValue(rootId,out var cursors)||!cursors.Root.TryGetTarget(out var owner)||!ReferenceEquals(owner,root))
            _localImageCursors[rootId]=cursors=new(new(root),new(StringComparer.Ordinal),new(StringComparer.Ordinal),new(StringComparer.Ordinal));
        return cursors;
    }
    internal void ResetLocalImageCursor(string rootId, string sourceId, SunEyeVision.Workflow.Workflow? expectedRoot = null)
    {
        lock (_lock)
        {
            if(_workflowEngine.GetWorkflow(rootId) is not {} root || (expectedRoot is not null && !ReferenceEquals(root,expectedRoot)))
                throw new InvalidOperationException("图像源所属根流程已失效，不能重置运行游标。");
            var cursors=GetLocalCursorOwner(rootId,root);
            cursors.ResetVersions[sourceId]=checked(++_nextLocalImageReset);
            cursors.Sources.Remove(sourceId);cursors.Blocked.Remove(sourceId);
        }
    }
    // Runtime navigation is independent from configuration history and the current run's frozen input.
    internal bool TrySelectLocalImageCursor(string rootId, SunEyeVision.Workflow.Workflow expectedRoot,
        ImageInputSource source, ImageInfo image, int index, long editVersion,
        SunEyeVision.Workflow.Persistence.SolutionResourceResolver paths)
    {
        var node=expectedRoot.Nodes.SingleOrDefault(item=>item.Id==source.NodeId&&ReferenceEquals(item.InputSource,source));
        var policy=node is null?null:SunEyeVision.Workflow.ImageSourcePolicy.Read(node);
        if(policy is null||index<0||index>=source.Count||!ReferenceEquals(source.Images[index],image)||!source.CanSelectImage(index))return false;
        var plan=SunEyeVision.Workflow.ImageSourcePolicy.CreateLocalPlan(source.NodeId,policy,paths);
        var entry=plan.Entries.SingleOrDefault(item=>item.OriginalIndex==index);
        if(entry is null||entry.Address!=image.FilePath||entry.ResourceReference!=(image.SourceReference??image.FilePath))return false;
        long reset;
        lock(_lock)
        {
            if(!ReferenceEquals(_workflowEngine.GetWorkflow(rootId),expectedRoot)||source.EditVersion!=editVersion)return false;
            var cursors=GetLocalCursorOwner(rootId,expectedRoot);
            if(cursors.Blocked.ContainsKey(source.NodeId)||_nextLocalImageReset==long.MaxValue)return false;
            reset=checked(++_nextLocalImageReset);
        }
        if(!source.TryPresentRuntimeCursor(editVersion,image.Id,entry.ResourceReference,entry.Address))return false;
        lock(_lock)
        {
            if(!ReferenceEquals(_workflowEngine.GetWorkflow(rootId),expectedRoot)||!expectedRoot.Nodes.Contains(node!)
                ||!ReferenceEquals(node!.InputSource,source)||source.EditVersion!=editVersion
                ||source.CurrentIndex!=index||!ReferenceEquals(source.CurrentImage,image))return false;
            var cursors=GetLocalCursorOwner(rootId,expectedRoot);
            if(cursors.ResetVersions.GetValueOrDefault(source.NodeId)>=reset)return false;
            cursors.ResetVersions[source.NodeId]=reset;
            cursors.Sources[source.NodeId]=new(CursorDefinition(plan),entry.EntryId,true,new(source));
            return true;
        }
    }
    internal void SynchronizeLocalImageProjection(SunEyeVision.Workflow.Workflow root,ImageInputSource source,
        SunEyeVision.Workflow.Persistence.SolutionResourceResolver paths)
    {
        var version=source.EditVersion;
        var node=root.Nodes.SingleOrDefault(item=>item.Id==source.NodeId&&ReferenceEquals(item.InputSource,source))
            ??throw new InvalidOperationException("已接纳图像源的节点身份已改变。");
        var policy=SunEyeVision.Workflow.ImageSourcePolicy.Read(node)
            ??throw new InvalidOperationException("已接纳图像源缺少正式设置。");
        if(policy.SourceKind!=ImageSourceKind.Local)return;
        var projection=source.CapturePolicy();
        if(!projection.FilePaths.SequenceEqual(policy.FilePaths)||projection.CurrentIndex!=policy.CurrentIndex
            ||projection.RunMode!=policy.RunMode||projection.AutoSwitchEnabled!=policy.AutoSwitchEnabled
            ||!projection.ForRunPaths.SequenceEqual(policy.ForRunPaths))
            throw new InvalidOperationException("图像源正式设置与已安装投影不一致。");
        var plan=SunEyeVision.Workflow.ImageSourcePolicy.CreateLocalPlan(node.Id,policy,paths);
        var entry=plan.Entries.SingleOrDefault(item=>item.OriginalIndex==source.CurrentIndex);
        lock(_lock)
        {
            if(!ReferenceEquals(_workflowEngine.GetWorkflow(root.Id),root)||!ReferenceEquals(node.InputSource,source)||source.EditVersion!=version)
                throw new InvalidOperationException("图像源运行游标所属根或投影已改变。");
            var cursors=GetLocalCursorOwner(root.Id,root);
            if(cursors.Blocked.TryGetValue(node.Id,out var reason))throw new InvalidOperationException("图像源运行游标尚不可用："+reason);
            var pending=entry is not null&&cursors.Sources.TryGetValue(node.Id,out var previous)&&previous.PendingSelection
                &&previous.SelectionOwner is {} selectionOwner&&selectionOwner.TryGetTarget(out var owner)&&ReferenceEquals(owner,source);
            cursors.ResetVersions[node.Id]=checked(++_nextLocalImageReset);
            cursors.Sources[node.Id]=new(CursorDefinition(plan),entry?.EntryId,pending,pending?new(source):null);
        }
    }
    internal void BlockLocalImageCursor(string rootId,string sourceId,string reason)
    {
        lock(_lock)
        {
            // If the engine now points at a different root instance with the same ID,
            // that live instance must also be prevented from running on an unsafe cursor.
            if(_workflowEngine.GetWorkflow(rootId) is not {} liveRoot)return;
            GetLocalCursorOwner(rootId,liveRoot).Blocked[sourceId]=reason;
        }
    }
    private static string CursorDefinition(LocalImageSourcePlan plan) => System.Text.Json.JsonSerializer.Serialize(new { plan.Entries, plan.SelectedEntryId });
    private sealed record PreparedLocalImages(IReadOnlyList<LocalImageSourcePlan> Plans,
        Dictionary<string, IReadOnlyDictionary<string, object?>> Overrides,
        IReadOnlyDictionary<string,SunEyeVision.Workflow.ImageSourcePolicy> Policies,
        Dictionary<string, LocalSourcePresentation> Presentations, string DefinitionSignature,
        SunEyeVision.Workflow.Workflow RootOwner,IReadOnlyDictionary<string,long> ResetVersions,IReadOnlySet<string> RootSourceIds);

    // Called only at the UI-owned complete-run boundary. No file IO and no publishing.
    private PreparedLocalImages CaptureLocalImageInputs(string rootId, SunEyeVision.Workflow.WorkflowRunConfiguration configuration,
        SunEyeVision.Workflow.Persistence.SolutionResourceResolver resourcePaths)
    {
        var plans = new List<LocalImageSourcePlan>();
        var rootSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var presentations = new Dictionary<string, LocalSourcePresentation>();
        var overrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
        var policies = new Dictionary<string,SunEyeVision.Workflow.ImageSourcePolicy>(StringComparer.Ordinal);
        var visited = new HashSet<string>();
        void Capture(string id)
        {
            if (!visited.Add(id) || configuration.CopyWorkflow(id) is not { } frozen) return;
            var formal = _workflowEngine.GetWorkflow(id);
            foreach (var node in frozen.Nodes)
            {
                if (!node.IsEnabled) continue;
                if (!string.IsNullOrEmpty(node.SubroutineId)) Capture(node.SubroutineId);
                if (!ImageSourceResolver.IsImageSource(node.ToolType)) continue;
                var config = SunEyeVision.Workflow.ImageSourcePolicy.Read(node)
                    ?? throw new InvalidOperationException("图像源缺少正式Runtime设置。");
                policies.Add(node.Id,config);
                if (config.SourceKind == ImageSourceKind.Camera)
                {
                    overrides[node.Id] = new Dictionary<string, object?> {
                        ["input.sequence"] = config.SNInitialValue };
                    continue;
                }
                var source = formal?.Nodes.FirstOrDefault(n => n.Id == node.Id)?.InputSource as ImageInputSource;
                var plan = SunEyeVision.Workflow.ImageSourcePolicy.CreateLocalPlan(node.Id, config, resourcePaths);
                plans.Add(plan);
                if(id==rootId)rootSourceIds.Add(node.Id);
                if (source is not null) presentations.Add(node.Id, new(source, source.EditVersion));
                overrides[node.Id] = new Dictionary<string, object?> {
                    ["input.sequence"] = config.SNInitialValue,
                    ["input.local-address"] = plan.Entries.FirstOrDefault(entry => entry.EntryId == plan.SelectedEntryId)?.Address ?? string.Empty };

            }
        }
        Capture(rootId);
        var owner = _workflowEngine.GetWorkflow(rootId) ?? throw new InvalidOperationException("图像源所属根流程已移除。");
        Dictionary<string,long> resets;
        lock(_lock)
        {
            var cursors=GetLocalCursorOwner(rootId,owner);
            var blocked=plans.FirstOrDefault(plan=>cursors.Blocked.ContainsKey(plan.SourceNodeId));
            if(blocked is not null)throw new InvalidOperationException("图像源配置已接纳，但运行游标不可用："+cursors.Blocked[blocked.SourceNodeId]);
            resets=plans.ToDictionary(plan=>plan.SourceNodeId,plan=>cursors.ResetVersions.GetValueOrDefault(plan.SourceNodeId),StringComparer.Ordinal);
        }
        // Runtime cursor advancement is not a configuration edit. Reuse its sequence until
        // a real source/configuration change, then install the new definition next whole round.
        var signature = System.Text.Json.JsonSerializer.Serialize(new {
            Plans = plans.Select(p => new { p.SourceNodeId, p.Entries, p.SelectedEntryId, p.AutoAdvance, p.StopAtLast }),
            Resets=resets,
            Settings = policies.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new {pair.Key,Policy=pair.Value}) });
        return new(plans, overrides, new System.Collections.ObjectModel.ReadOnlyDictionary<string,SunEyeVision.Workflow.ImageSourcePolicy>(policies),presentations, signature, owner,resets,rootSourceIds);
    }

    private sealed class LocalImageExecution : IDisposable
    {
        private readonly LocalImageSequenceService _sequences;
        public string WorkflowId { get; }
        public long Generation { get; }
        public string DefinitionSignature { get; }
        public SunEyeVision.Workflow.Workflow RootOwner { get; }
        public IReadOnlyDictionary<string, string> CursorDefinitions { get; }
        public IReadOnlyDictionary<string,long> ResetVersions {get;}
        public Dictionary<string, IReadOnlyDictionary<string, object?>> BaseOverrides { get; }
        public Dictionary<string, LocalSourcePresentation> Presentations { get; }
        public IReadOnlyList<LocalImageRunInput> Consumed { get; private set; } = Array.Empty<LocalImageRunInput>();
        public bool Advance { get; set; } = true;
        public HashSet<string> PendingSelections { get; } = new(StringComparer.Ordinal);
        public string? ExplicitSourceId { get; set; }
        public int NavigationDirection { get; set; }
        public IReadOnlySet<string> RootSourceIds { get; }
        public long IterationVersion { get; private set; }
        public bool RoundStarted { get; private set; }
        public Func<SunEyeVision.Plugin.Contracts.RunIdentity,CancellationToken,string>? AdmissionFor(string sourceId)
        {
            var ticket=Consumed.SingleOrDefault(input=>input.SourceNodeId==sourceId);
            return ticket is null ? null : (identity,cancellation) => _sequences.AcceptRead(ticket,identity.RequestId,
                !PendingSelections.Contains(sourceId) && (Advance || (ExplicitSourceId is not null && ExplicitSourceId!=sourceId)),cancellation,
                RootSourceIds.Contains(sourceId)?NavigationDirection:0).Entry.Address;
        }
        public LocalImageReadRound FinishRound()
        {
            if(!RoundStarted)throw new InvalidOperationException("图像读取轮次尚未开始或已结算。");
            var result=_sequences.FinishReadRound(WorkflowId,Generation,IterationVersion);
            IterationVersion=checked(IterationVersion+1);RoundStarted=false;return result;
        }

        public LocalImageExecution(LocalImageSequenceService sequences, string workflowId, long generation,
            Dictionary<string, IReadOnlyDictionary<string, object?>> overrides,
            Dictionary<string, LocalSourcePresentation> presentations, string definitionSignature,
            SunEyeVision.Workflow.Workflow rootOwner, IReadOnlyDictionary<string, string> cursorDefinitions,
            IReadOnlyDictionary<string,long> resetVersions,IReadOnlySet<string> rootSourceIds)
        {
            _sequences = sequences; WorkflowId = workflowId; Generation = generation;
            BaseOverrides = overrides; Presentations = presentations; DefinitionSignature = definitionSignature;
            RootOwner = rootOwner; CursorDefinitions = cursorDefinitions;ResetVersions=resetVersions;RootSourceIds=rootSourceIds;
        }

        public Dictionary<string, IReadOnlyDictionary<string, object?>> CaptureIteration()
        {
            Consumed = _sequences.GetInputs(WorkflowId, Generation);
            if(RoundStarted)throw new InvalidOperationException("上一图像读取轮次尚未结算。");
            if(Consumed.Any(input=>input.IterationVersion!=IterationVersion))throw new InvalidOperationException("图像读取轮次身份不匹配。");
            RoundStarted=true;
            var result = BaseOverrides.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(pair.Value));
            foreach (var input in Consumed)
            {
                var values = new Dictionary<string, object?>(result[input.SourceNodeId]);
                values["input.local-address"] = input.Entry?.Address ?? string.Empty;
                result[input.SourceNodeId] = values;
            }
            return result;
        }

        public void Dispose() => _sequences.End(WorkflowId, Generation);
    }

    private Task<LocalImageExecution> BeginLocalImagesAsync(WorkflowTabViewModel workflow, CancellationToken token)
        => BeginLocalImagesAsync(workflow.RootWorkflowId,token);

    private Task<LocalImageExecution> BeginLocalImagesAsync(string workflowId, CancellationToken token)
    {
        PreparedLocalImages captured;
        lock (_lock)
            captured = _preparedRunInputs.TryGetValue(workflowId, out var inputs) ? inputs.LocalImages
                : throw new InvalidOperationException("图像输入缺少本轮正式配置快照。");
        var plans = captured.Plans;
        // Keep the entire frozen eligible list: a missing file is consumed at the read boundary.
        token.ThrowIfCancellationRequested();
        var validPlans = plans.ToArray();

        var definitions = plans.ToDictionary(plan => plan.SourceNodeId, CursorDefinition, StringComparer.Ordinal);
        var pendingSelections=new HashSet<string>(StringComparer.Ordinal);
        lock (_lock)
        {
            if (_localImageCursors.TryGetValue(workflowId, out var cursors) &&
                cursors.Root.TryGetTarget(out var owner) && ReferenceEquals(owner, captured.RootOwner))
                validPlans = validPlans.Select(plan =>
                {
                    if(!cursors.Sources.TryGetValue(plan.SourceNodeId,out var cursor)
                        ||cursors.ResetVersions.GetValueOrDefault(plan.SourceNodeId)!=captured.ResetVersions[plan.SourceNodeId]
                        ||cursor.Definition!=definitions[plan.SourceNodeId])return plan;
                    if(cursor.PendingSelection)
                    {
                        if(cursor.SelectionOwner is null||!cursor.SelectionOwner.TryGetTarget(out var source)
                            ||!captured.Presentations.TryGetValue(plan.SourceNodeId,out var presentation)
                            ||!ReferenceEquals(source,presentation.Source))return plan;
                        pendingSelections.Add(plan.SourceNodeId);
                    }
                    return plan with {SelectedEntryId=cursor.EntryId};
                }).ToArray();
        }
        var generation = _localImageSequences.Begin(workflowId, validPlans);
        var execution=new LocalImageExecution(_localImageSequences, workflowId, generation, captured.Overrides, captured.Presentations,
            captured.DefinitionSignature, captured.RootOwner, definitions,captured.ResetVersions,captured.RootSourceIds);
        execution.PendingSelections.UnionWith(pendingSelections);
        return Task.FromResult(execution);
    }

    private async Task<LocalImageCompletion> CompleteLocalImagesAsync(LocalImageExecution images, CancellationToken token)
    {
        // Acceptance is already final; cancellation and downstream status cannot undo it.
        var round=images.FinishRound();
        foreach(var receipt in round.Accepted)images.PendingSelections.Remove(receipt.Ticket.SourceNodeId);
        lock (_lock)
        {
            if (ReferenceEquals(_workflowEngine.GetWorkflow(images.WorkflowId),images.RootOwner))
            {
                var cursors=GetLocalCursorOwner(images.WorkflowId,images.RootOwner);
                foreach(var receipt in round.Accepted)
                {
                    var id=receipt.Ticket.SourceNodeId;
                    if(cursors.ResetVersions.GetValueOrDefault(id)==images.ResetVersions[id])
                        cursors.Sources[id]=new(images.CursorDefinitions[id],receipt.Entry.EntryId);
                }
            }
        }
        await InvokeOnUIAsync(() =>
        {
            if(token.IsCancellationRequested)return;
            var root=_workflowEngine.GetWorkflow(images.WorkflowId);
            if(!ReferenceEquals(root,images.RootOwner))return;
            foreach(var receipt in round.Accepted)
            {
                var id=receipt.Ticket.SourceNodeId;var entry=receipt.Entry;
                if(!images.Presentations.TryGetValue(id,out var presentation))continue;
                lock(_lock)
                    if(!_localImageCursors.TryGetValue(images.WorkflowId,out var cursors)||
                        cursors.ResetVersions.GetValueOrDefault(id)!=images.ResetVersions[id]||
                        !cursors.Sources.TryGetValue(id,out var current)||current.EntryId!=entry.EntryId)continue;
                var node=FindNodeRecursive(root,id);
                if(!ReferenceEquals(node?.InputSource,presentation.Source)||entry.OriginalIndex<0||entry.OriginalIndex>=presentation.Source.Count)continue;
                var displayed=presentation.Source.Images[entry.OriginalIndex];
                presentation.Source.TryPresentRuntimeCursor(presentation.EditVersion,displayed.Id,entry.ResourceReference,entry.Address);
            }
        });
        return round.StopAfterCurrent ? LocalImageCompletion.StopAtEnd :
            round.Accepted.Count>0 ? LocalImageCompletion.Advanced : LocalImageCompletion.Retained;
    }
}
