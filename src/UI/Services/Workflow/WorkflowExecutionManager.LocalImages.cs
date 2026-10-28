using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Workflow;

public partial class WorkflowExecutionManager
{
    private readonly LocalImageSequenceService _localImageSequences = new();
    private sealed record LocalSourcePresentation(ImageInputSource Source, long EditVersion);
    private sealed record LocalCursor(string Definition, string? EntryId);
    private sealed record LocalCursorOwner(WeakReference<SunEyeVision.Workflow.Workflow> Root, Dictionary<string, LocalCursor> Sources,
        Dictionary<string,long> ResetVersions);
    private readonly Dictionary<string, LocalCursorOwner> _localImageCursors = new(StringComparer.Ordinal);
    private long _nextLocalImageReset;
    // Caller holds _lock. A replacement root never inherits another instance's cursors.
    private LocalCursorOwner GetLocalCursorOwner(string rootId,SunEyeVision.Workflow.Workflow root)
    {
        if(!_localImageCursors.TryGetValue(rootId,out var cursors)||!cursors.Root.TryGetTarget(out var owner)||!ReferenceEquals(owner,root))
            _localImageCursors[rootId]=cursors=new(new(root),new(StringComparer.Ordinal),new(StringComparer.Ordinal));
        return cursors;
    }
    internal void ResetLocalImageCursor(string rootId, string sourceId)
    {
        lock (_lock)
        {
            if(_workflowEngine.GetWorkflow(rootId) is not {} root)return;
            var cursors=GetLocalCursorOwner(rootId,root);
            cursors.ResetVersions[sourceId]=checked(++_nextLocalImageReset);
            cursors.Sources.Remove(sourceId);
        }
    }
    private static string CursorDefinition(LocalImageSourcePlan plan) => System.Text.Json.JsonSerializer.Serialize(new { plan.Entries, plan.SelectedEntryId });
    private sealed record PreparedLocalImages(IReadOnlyList<LocalImageSourcePlan> Plans,
        Dictionary<string, IReadOnlyDictionary<string, object?>> Overrides,
        Dictionary<string, LocalSourcePresentation> Presentations, string DefinitionSignature,
        SunEyeVision.Workflow.Workflow RootOwner,IReadOnlyDictionary<string,long> ResetVersions);

    // Called only at the UI-owned complete-run boundary. No file IO and no publishing.
    private PreparedLocalImages CaptureLocalImageInputs(string rootId, SunEyeVision.Workflow.WorkflowRunConfiguration configuration,
        SunEyeVision.Workflow.Persistence.SolutionResourceResolver resourcePaths)
    {
        var plans = new List<LocalImageSourcePlan>();
        var presentations = new Dictionary<string, LocalSourcePresentation>();
        var overrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
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
                if (config?.SourceKind == ImageSourceKind.Camera)
                {
                    overrides[node.Id] = new Dictionary<string, object?> {
                        ["SourceKind"] = ImageSourceKind.Camera, ["CameraDeviceId"] = config.CameraDeviceId,
                        ["CameraTimeoutMs"] = config.CameraTimeoutMs, ["OutputFormat"] = config.OutputFormat,
                        ["SequenceNumber"] = config.SNInitialValue, ["FilePath"] = string.Empty };
                    continue;
                }
                var source = formal?.Nodes.FirstOrDefault(n => n.Id == node.Id)?.InputSource as ImageInputSource;
                var plan = CaptureLocalImagePlan(node.Id, config, resourcePaths);
                plans.Add(plan);
                if (source is not null) presentations.Add(node.Id, new(source, source.EditVersion));
                overrides[node.Id] = new Dictionary<string, object?> {
                    ["SourceKind"] = ImageSourceKind.Local, ["OutputFormat"] = config?.OutputFormat ?? ImageOutputFormat.Original,
                    ["SequenceNumber"] = config?.SNInitialValue ?? 1L,
                    ["FilePath"] = plan.Entries.FirstOrDefault(entry => entry.EntryId == plan.SelectedEntryId)?.Address ?? string.Empty };

            }
        }
        Capture(rootId);
        var owner = _workflowEngine.GetWorkflow(rootId) ?? throw new InvalidOperationException("图像源所属根流程已移除。");
        Dictionary<string,long> resets;
        lock(_lock)
        {
            var cursors=GetLocalCursorOwner(rootId,owner);
            resets=plans.ToDictionary(plan=>plan.SourceNodeId,plan=>cursors.ResetVersions.GetValueOrDefault(plan.SourceNodeId),StringComparer.Ordinal);
        }
        // Runtime cursor advancement is not a configuration edit. Reuse its sequence until
        // a real source/configuration change, then install the new definition next whole round.
        var signature = System.Text.Json.JsonSerializer.Serialize(new {
            Plans = plans.Select(p => new { p.SourceNodeId, p.Entries, p.SelectedEntryId, p.AutoAdvance, p.StopAtLast }),
            Resets=resets,
            Settings = overrides.Select(p => new { p.Key, Values = p.Value.Where(v => v.Key != "FilePath").ToArray() }) });
        return new(plans, overrides, presentations, signature, owner,resets);
    }

    internal static LocalImageSourcePlan CaptureLocalImagePlan(string nodeId, SunEyeVision.Workflow.ImageSourcePolicy? config,
        SunEyeVision.Workflow.Persistence.SolutionResourceResolver resourcePaths)
    {
        config ??= new() { NodeId = nodeId };
        if (config.NodeId != nodeId || config.FilePaths is null || config.ForRunPaths is null ||
            config.RunMode is not (0 or 1) || config.CurrentIndex < -1 || config.CurrentIndex >= config.FilePaths.Count ||
            !Enum.IsDefined(config.SourceKind) || !Enum.IsDefined(config.OutputFormat) || config.CameraTimeoutMs <= 0 || config.SNInitialValue < 0)
            throw new InvalidOperationException("正式图像源身份、运行模式或选择无效。");
        var selected = config.ForRunPaths.ToHashSet(StringComparer.Ordinal);
        // Stable within an unchanged frozen list, including repeated references. A view's
        // generated item IDs and runtime cursor never define a run's input or generation.
        var all = config.FilePaths.Select((reference, index) => new LocalImageSequenceEntry(
            $"{index}:{reference}", reference, resourcePaths.ResolveAddress("ImageSourceSettings.FileReferences", reference), index)).ToArray();
        var entries = all.Where(entry => config.RunMode == 0 || selected.Contains(entry.ResourceReference)).ToArray();
        return new(nodeId, Array.AsReadOnly(entries), config.CurrentIndex < 0 ? null : all[config.CurrentIndex].EntryId,
            config.AutoSwitchEnabled, config.StopAtLastImage);
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

        public LocalImageExecution(LocalImageSequenceService sequences, string workflowId, long generation,
            Dictionary<string, IReadOnlyDictionary<string, object?>> overrides,
            Dictionary<string, LocalSourcePresentation> presentations, string definitionSignature,
            SunEyeVision.Workflow.Workflow rootOwner, IReadOnlyDictionary<string, string> cursorDefinitions,
            IReadOnlyDictionary<string,long> resetVersions)
        {
            _sequences = sequences; WorkflowId = workflowId; Generation = generation;
            BaseOverrides = overrides; Presentations = presentations; DefinitionSignature = definitionSignature;
            RootOwner = rootOwner; CursorDefinitions = cursorDefinitions;ResetVersions=resetVersions;
        }

        public Dictionary<string, IReadOnlyDictionary<string, object?>> CaptureIteration()
        {
            Consumed = _sequences.GetInputs(WorkflowId, Generation);
            var result = BaseOverrides.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(pair.Value));
            foreach (var input in Consumed)
            {
                var values = new Dictionary<string, object?>(result[input.SourceNodeId]);
                values["FilePath"] = input.Entry?.Address ?? string.Empty;
                result[input.SourceNodeId] = values;
            }
            return result;
        }

        public void Dispose() => _sequences.End(WorkflowId, Generation);
    }

    private async Task<LocalImageExecution> BeginLocalImagesAsync(WorkflowTabViewModel workflow, CancellationToken token)
    {
        PreparedLocalImages captured;
        lock (_lock)
            captured = _preparedRunInputs.TryGetValue(workflow.RootWorkflowId, out var inputs) ? inputs.LocalImages
                : throw new InvalidOperationException("图像输入缺少本轮正式配置快照。");
        var plans = captured.Plans;
        // File/network checks run away from the dispatcher. Original references stay in the plan.
        var validPlans = await Task.Run(() => plans.Select(plan =>
        {
            token.ThrowIfCancellationRequested();
            return plan with { Entries = Array.AsReadOnly(plan.Entries.Where(entry => System.IO.File.Exists(entry.Address)).ToArray()) };
        }).ToArray(), token);
        token.ThrowIfCancellationRequested();
        var empty = validPlans.FirstOrDefault(plan => plan.Entries.Count == 0 && plan.SelectedEntryId == null);
        if (empty != null)
            throw new InvalidOperationException($"图像来源 {empty.SourceNodeId} 没有可运行的有效图片，请检查勾选和文件状态。");
        var definitions = plans.ToDictionary(plan => plan.SourceNodeId, CursorDefinition, StringComparer.Ordinal);
        lock (_lock)
        {
            if (_localImageCursors.TryGetValue(workflow.RootWorkflowId, out var cursors) &&
                cursors.Root.TryGetTarget(out var owner) && ReferenceEquals(owner, captured.RootOwner))
                validPlans = validPlans.Select(plan => cursors.Sources.TryGetValue(plan.SourceNodeId, out var cursor) &&
                    cursors.ResetVersions.GetValueOrDefault(plan.SourceNodeId)==captured.ResetVersions[plan.SourceNodeId]&&
                    cursor.Definition == definitions[plan.SourceNodeId] ? plan with { SelectedEntryId = cursor.EntryId } : plan).ToArray();
        }
        var generation = _localImageSequences.Begin(workflow.RootWorkflowId, validPlans);
        return new(_localImageSequences, workflow.RootWorkflowId, generation, captured.Overrides, captured.Presentations,
            captured.DefinitionSignature, captured.RootOwner, definitions,captured.ResetVersions);
    }

    private async Task<LocalImageCompletion> CompleteLocalImagesAsync(LocalImageExecution images,
        bool success, bool advance, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = _localImageSequences.Complete(images.WorkflowId, images.Generation, images.Consumed, success, advance);
        if (completion is LocalImageCompletion.Stale) throw new InvalidOperationException("图像运行序列已失效，未推进。");
        if (!success) return completion;
        var next = _localImageSequences.GetInputs(images.WorkflowId, images.Generation);
        lock (_lock)
        {
            token.ThrowIfCancellationRequested();
            if (ReferenceEquals(_workflowEngine.GetWorkflow(images.WorkflowId), images.RootOwner))
            {
                var cursors=GetLocalCursorOwner(images.WorkflowId,images.RootOwner);
                foreach (var input in next)
                    if(cursors.ResetVersions.GetValueOrDefault(input.SourceNodeId)==images.ResetVersions[input.SourceNodeId])
                        cursors.Sources[input.SourceNodeId] = new(images.CursorDefinitions[input.SourceNodeId], input.Entry?.EntryId);
            }
        }
        await InvokeOnUIAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            // Recheck session + iteration at dispatch time. User edits independently gate presentation.
            var current = _localImageSequences.GetInputs(images.WorkflowId, images.Generation);
            if (!current.SequenceEqual(next)) return;
            var root = _workflowEngine.GetWorkflow(images.WorkflowId);
            if (!ReferenceEquals(root,images.RootOwner)) return;
            foreach (var input in next)
                if (input.Entry is { } entry && images.Presentations.TryGetValue(input.SourceNodeId, out var presentation))
                {
                    lock(_lock)
                        if(!_localImageCursors.TryGetValue(images.WorkflowId,out var cursors)||
                            cursors.ResetVersions.GetValueOrDefault(input.SourceNodeId)!=images.ResetVersions[input.SourceNodeId])continue;
                    var node = FindNodeRecursive(root, input.SourceNodeId);
                    if (!ReferenceEquals(node?.InputSource, presentation.Source)) continue;
                    if (entry.OriginalIndex < 0 || entry.OriginalIndex >= presentation.Source.Count) continue;
                    var displayed = presentation.Source.Images[entry.OriginalIndex];
                    presentation.Source.TryPresentRuntimeCursor(presentation.EditVersion,
                        displayed.Id, entry.ResourceReference, entry.Address);
                }
        });
        return completion;
    }
}
