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

    private sealed class LocalImageExecution : IDisposable
    {
        private readonly LocalImageSequenceService _sequences;
        public string WorkflowId { get; }
        public long Generation { get; }
        public Dictionary<string, IReadOnlyDictionary<string, object?>> BaseOverrides { get; }
        public Dictionary<string, LocalSourcePresentation> Presentations { get; }
        public IReadOnlyList<LocalImageRunInput> Consumed { get; private set; } = Array.Empty<LocalImageRunInput>();

        public LocalImageExecution(LocalImageSequenceService sequences, string workflowId, long generation,
            Dictionary<string, IReadOnlyDictionary<string, object?>> overrides,
            Dictionary<string, LocalSourcePresentation> presentations)
        {
            _sequences = sequences; WorkflowId = workflowId; Generation = generation;
            BaseOverrides = overrides; Presentations = presentations;
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
        var plans = new List<LocalImageSourcePlan>();
        var presentations = new Dictionary<string, LocalSourcePresentation>();
        var overrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
        await InvokeOnUIAsync(() =>
        {
            token.ThrowIfCancellationRequested();
            var root = _workflowEngine.GetWorkflow(workflow.RootWorkflowId)
                ?? throw new InvalidOperationException("图像输入所属流程已删除。");
            var visited = new HashSet<string>();
            void Capture(SunEyeVision.Workflow.Workflow flow)
            {
                if (!visited.Add(flow.Id)) return;
                foreach (var node in flow.Nodes)
                {
                    if (!node.IsEnabled) continue;
                    if (!string.IsNullOrEmpty(node.SubroutineId)
                        && _workflowEngine.GetWorkflow(node.SubroutineId) is { } child) Capture(child);
                    if (!ImageSourceResolver.IsImageSource(node.ToolType)
                        || node.ImageSourceConfig?.SourceKind == ImageSourceKind.Camera) continue;
                    var source = node.InputSource as ImageInputSource;
                    if (source == null)
                    {
                        source = node is WorkflowNode uiNode ? uiNode.EnsureInputSource() : new ImageInputSource(node.Id);
                        if (node.ImageSourceConfig is { } saved) source.LoadFromConfig(saved);
                        if (node is not WorkflowNode)
                        {
                            node.InputSource = source;
                            source.ConfigChanged = config =>
                            {
                                node.ImageSourceConfig ??= new() { NodeId = node.Id };
                                node.ImageSourceConfig.UpdateLocalInput(config);
                            };
                        }
                    }
                    var entries = source.Images.Select((image, index) => (image, index))
                        .Where(item => source.RunMode != ImageRunMode.运行选择 || item.image.IsForRun)
                        .Select(item => new LocalImageSequenceEntry(item.image.Id,
                            item.image.SourceReference ?? item.image.FilePath, item.image.FilePath, item.index)).ToArray();
                    plans.Add(new(node.Id, entries, source.CurrentImage?.Id,
                        source.AutoSwitchEnabled, node.ImageSourceConfig?.StopAtLastImage == true));
                    presentations.Add(node.Id, new(source, source.EditVersion));
                }
            }
            Capture(root);
            overrides = CreateImageLoadParameterOverrides(root, VisionLogger.Instance, new HashSet<string>());
        });
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
        return new(_localImageSequences, workflow.RootWorkflowId, generation, overrides, presentations);
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
