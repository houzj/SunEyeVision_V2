using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Panels;

namespace SunEyeVision.UI.Services.Workflow;

public partial class WorkflowExecutionManager
{
    private readonly LocalImageSequenceService _localImageSequences = new();
    private sealed record LocalSourcePresentation(ImageInputSource Source, long EditVersion);
    private sealed record PreparedLocalImages(IReadOnlyList<LocalImageSourcePlan> Plans,
        Dictionary<string, IReadOnlyDictionary<string, object?>> Overrides,
        Dictionary<string, LocalSourcePresentation> Presentations, string DefinitionSignature);

    // Called only at the UI-owned complete-run boundary. No file IO and no publishing.
    private PreparedLocalImages CaptureLocalImageInputs(string rootId, SunEyeVision.Workflow.WorkflowRunConfiguration configuration)
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
                var config = node.ImageSourceConfig;
                if (config?.SourceKind == ImageSourceKind.Camera)
                {
                    overrides[node.Id] = new Dictionary<string, object?> {
                        ["SourceKind"] = ImageSourceKind.Camera, ["CameraDeviceId"] = config.CameraDeviceId,
                        ["CameraTimeoutMs"] = config.CameraTimeoutMs, ["OutputFormat"] = config.OutputFormat,
                        ["SequenceNumber"] = config.SNInitialValue, ["FilePath"] = string.Empty };
                    continue;
                }
                var source = formal?.Nodes.FirstOrDefault(n => n.Id == node.Id)?.InputSource as ImageInputSource;
                if (source is null)
                {
                    source = new ImageInputSource(node.Id);
                    if (config is not null) source.LoadFromConfig(config);
                }
                var entries = source.Images.Select((image, index) => (image, index))
                    .Where(item => source.RunMode != ImageRunMode.运行选择 || item.image.IsForRun)
                    .Select(item => new LocalImageSequenceEntry(item.image.Id,
                        item.image.SourceReference ?? item.image.FilePath, item.image.FilePath, item.index)).ToArray();
                plans.Add(new(node.Id, entries, source.CurrentImage?.Id, source.AutoSwitchEnabled, config?.StopAtLastImage == true));
                presentations.Add(node.Id, new(source, source.EditVersion));
                overrides[node.Id] = new Dictionary<string, object?> {
                    ["SourceKind"] = ImageSourceKind.Local, ["OutputFormat"] = config?.OutputFormat ?? ImageOutputFormat.Original,
                    ["SequenceNumber"] = config?.SNInitialValue ?? 1L, ["FilePath"] = source.CurrentImage?.FilePath ?? string.Empty };
            }
        }
        Capture(rootId);
        // Runtime cursor advancement is not a configuration edit. Reuse its sequence until
        // a real source/configuration change, then install the new definition next whole round.
        var signature = System.Text.Json.JsonSerializer.Serialize(new {
            Plans = plans.Select(p => new { p.SourceNodeId, p.Entries, p.AutoAdvance, p.StopAtLast }),
            Settings = overrides.Select(p => new { p.Key, Values = p.Value.Where(v => v.Key != "FilePath").ToArray() }),
            Views = presentations.Select(p => new { p.Key, p.Value.EditVersion }) });
        return new(plans, overrides, presentations, signature);
    }


    private sealed class LocalImageExecution : IDisposable
    {
        private readonly LocalImageSequenceService _sequences;
        public string WorkflowId { get; }
        public long Generation { get; }
        public string DefinitionSignature { get; }
        public Dictionary<string, IReadOnlyDictionary<string, object?>> BaseOverrides { get; }
        public Dictionary<string, LocalSourcePresentation> Presentations { get; }
        public IReadOnlyList<LocalImageRunInput> Consumed { get; private set; } = Array.Empty<LocalImageRunInput>();

        public LocalImageExecution(LocalImageSequenceService sequences, string workflowId, long generation,
            Dictionary<string, IReadOnlyDictionary<string, object?>> overrides,
            Dictionary<string, LocalSourcePresentation> presentations, string definitionSignature)
        {
            _sequences = sequences; WorkflowId = workflowId; Generation = generation;
            BaseOverrides = overrides; Presentations = presentations; DefinitionSignature = definitionSignature;
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
        var generation = _localImageSequences.Begin(workflow.RootWorkflowId, validPlans);
        return new(_localImageSequences, workflow.RootWorkflowId, generation, captured.Overrides, captured.Presentations, captured.DefinitionSignature);
    }

    private async Task<LocalImageCompletion> CompleteLocalImagesAsync(LocalImageExecution images,
        bool success, bool advance, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = _localImageSequences.Complete(images.WorkflowId, images.Generation, images.Consumed, success, advance);
        if (completion is LocalImageCompletion.Stale) throw new InvalidOperationException("图像运行序列已失效，未推进。");
        if (!success) return completion;
        var next = _localImageSequences.GetInputs(images.WorkflowId, images.Generation);
        await InvokeOnUIAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            // Recheck session + iteration at dispatch time. User edits independently gate presentation.
            var current = _localImageSequences.GetInputs(images.WorkflowId, images.Generation);
            if (!current.SequenceEqual(next)) return;
            var root = _workflowEngine.GetWorkflow(images.WorkflowId);
            if (root == null) return;
            foreach (var input in next)
                if (input.Entry is { } entry && images.Presentations.TryGetValue(input.SourceNodeId, out var presentation))
                {
                    var node = FindNodeRecursive(root, input.SourceNodeId);
                    if (!ReferenceEquals(node?.InputSource, presentation.Source)) continue;
                    presentation.Source.TryPresentRuntimeCursor(presentation.EditVersion,
                        entry.EntryId, entry.ResourceReference, entry.Address);
                }
        });
        return completion;
    }
}
