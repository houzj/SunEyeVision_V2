using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using System.Runtime.CompilerServices;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private readonly ConditionalWeakTable<IReadOnlyStoredRaster, Dictionary<(string Id, long Revision), Guid>> _variableImageFrames = new();

    /// <summary>Capture the same frozen frame as execution without starting a run or creating a publication.</summary>
    public (string Name, FrozenImageResource Image, Func<bool> Current)? CaptureVariableImageForDisplay(VariableReference reference)
    {
        lock (_gate)
        lock (GlobalVariable.ValueGate)
        {
            bool SessionCurrent() => !_closed && !_closing && !_owner.IsRetired && _directory.IsActive
                && Document.Id == _documentId && reference.SolutionId == _documentId;
            if (!SessionCurrent() || reference.Type != TypeRef.ResourceOf(ResourceKind.Image)) return null;
            var matches = Document.GlobalVariables.Where(variable => variable.Id == reference.VariableId).ToArray();
            if (matches.Length != 1) return null;
            var variable = matches[0];
            if (variable.Type != "Image" || variable.ProducerBinding is not null || variable.Value is not IReadOnlyStoredRaster raster)
                return null;
            var revision = variable.ValueRevision;
            bool Current()
            {
                lock (_gate)
                lock (GlobalVariable.ValueGate)
                    return SessionCurrent() && Document.GlobalVariables.Count(item => item.Id == reference.VariableId) == 1
                        && Document.GlobalVariables.Contains(variable) && variable.Id == reference.VariableId
                        && variable.Type == "Image" && variable.ProducerBinding is null
                        && variable.ValueRevision == revision && ReferenceEquals(variable.Value, raster);
            }
            return (variable.Name, CaptureVariableImage(variable, raster), Current);
        }
    }

    private (ExecutionVariableValues Values, List<ExecutionGlobalProducerOperation> Producers) CaptureRuntimeVariables(
        WorkflowDefinition[] flows, GlobalVariable[] definitions, IReadOnlySet<string> referencedImages)
    {
        var names = definitions.ToDictionary(variable => variable.Id, variable => variable.Name, StringComparer.Ordinal);
        var sources = new List<BindingVariableSource>(); var initial = new Dictionary<string, object?>(StringComparer.Ordinal);
        var issues = new Dictionary<string, Diagnostic>(StringComparer.Ordinal);
        var producers = new List<ExecutionGlobalProducerOperation>();
        var images = new Dictionary<string, FrozenImageResource>(StringComparer.Ordinal);
        try
        {
            foreach (var variable in definitions)
            {
                if (variable.Type == "Image")
                {
                    var reference = new VariableReference(_documentId, variable.Id, TypeRef.ResourceOf(ResourceKind.Image));
                    sources.Add(new BindingVariableSource(reference, null));
                    if (!referencedImages.Contains(variable.Id)) continue;
                    try
                    {
                        if (variable.ProducerBinding is not null)
                            throw new InvalidOperationException("图像变量不支持生产绑定。");
                        if (variable.Value is not IReadOnlyStoredRaster raster)
                            throw new InvalidOperationException("图像变量未赋值。");
                        images.Add(variable.Id, CaptureVariableImage(variable, raster));
                    }
                    catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
                    {
                        issues.Add(variable.Id, new Diagnostic("variable-image", error.Message, variable.Id));
                    }
                    continue;
                }
                if (!GlobalProducerBindingRules.TryDescribeTarget(variable.Type, out var port, out var valueType, out _)) continue;
                BindingNodeId? producer = null;
                if (variable.ProducerBinding is { } binding)
                {
                    var reason = variable.RebindingReason ?? variable.BindingValidationPending;
                    if (string.IsNullOrEmpty(reason))
                    {
                        var validation = GlobalProducerBindingRules.Validate(binding, variable,
                            id => flows.SingleOrDefault(flow => flow.Id == id)?.Nodes,
                            node => AdmitConfiguration(node, node.Configuration).Schema,
                            () => Document.Id == _documentId && flows.Length == Document.Workflows.Count &&
                                flows.Where((flow, index) => !ReferenceEquals(flow, Document.Workflows[index])).Any() == false);
                        if (validation.State is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved) reason = validation.Reason;
                        else
                        {
                            var reference = binding.SourceReference;
                            producer = new(reference.ScopeId!, reference.NodeId);
                            producers.Add(new ExecutionGlobalProducerOperation(variable.Id, variable.Name, reference, port, valueType));
                        }
                    }
                    if (!string.IsNullOrEmpty(reason)) issues.Add(variable.Id, new Diagnostic("variable-unavailable", reason, variable.Id));
                }
                sources.Add(new BindingVariableSource(new VariableReference(_documentId, variable.Id, port.Type, storage: port.Storage), producer));
                if (!issues.ContainsKey(variable.Id)) initial.Add(variable.Id, variable.Value);
            }
            return (new ExecutionVariableValues(_documentId, sources, names, initial, issues, images), producers);
        }
        catch
        {
            foreach (var image in images.Values) image.Dispose();
            throw;
        }
    }

    private FrozenImageResource CaptureVariableImage(GlobalVariable variable, IReadOnlyStoredRaster raster)
    {
        using var reader = raster.AcquireReader();
        var stored = reader.Layout;
        var format = stored.Format switch
        {
            StoredRasterFormat.Gray8 => PixelLayout.Gray8,
            StoredRasterFormat.Bgr24 => PixelLayout.Bgr24,
            StoredRasterFormat.Bgra32 => PixelLayout.Bgra32,
            _ => throw new ArgumentException("图像变量位深尚未接入执行图像布局。")
        };
        var layout = new ImageLayout(stored.Width, stored.Height, stored.RowStrideBytes, format);
        var pixels = new byte[layout.BufferLength];
        var runs = new RegionRun[layout.Height];
        for (var row = 0; row < layout.Height; row++)
        {
            var bytes = reader.GetRow(row);
            if (bytes.Length != layout.RowStrideBytes) throw new ArgumentException("图像变量像素行长度不符合存储声明。");
            bytes.CopyTo(pixels.AsSpan(row * layout.RowStrideBytes, layout.RowStrideBytes));
            runs[row] = new(row, 0, layout.Width - 1);
        }
        var frames = _variableImageFrames.GetOrCreateValue(raster);
        var key = (variable.Id, variable.ValueRevision);
        if (!frames.TryGetValue(key, out var frame)) frames.Add(key, frame = Guid.NewGuid());
        return new FrozenImageResource(layout, new ImageFrameAddress(frame, "variable:" + variable.Id),
            RoiDeclarations.ImageFrame, pixels, runs);
    }

    private static IReadOnlySet<string> ReferencedVariableImages(IEnumerable<ExecutionWorkflowNode> nodes) =>
        nodes.SelectMany(node => node.Node.CaptureExecutionFields())
            .Select(pair => pair.Value).OfType<VariableSetting>()
            .Where(setting => setting.Reference.Type == TypeRef.ResourceOf(ResourceKind.Image))
            .Select(setting => setting.Reference.VariableId).ToHashSet(StringComparer.Ordinal);
}
