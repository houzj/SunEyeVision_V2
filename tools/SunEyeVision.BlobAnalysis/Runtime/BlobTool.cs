using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob runtime entry. One invocation only; workflow scheduling belongs to the host.</summary>
public sealed class BlobTool : IToolDefinition<BlobSettings, BlobOutput>, IToolResultPresentationProvider
{
    public const string Id = "BlobAnalysis";
    public const string ImageInput = "input.image";
    public const string InspectionInput = "input.inspection.region";
    public const string MaskInput = "input.mask.region";
    public const string PoseInput = "input.pose";
    public string ToolId => Id;
    public ToolSchema ResultSchema => new(Array.Empty<Port>(), BlobSchema.Outputs);
    public ResultProjectionDescriptor ResultProjection => BlobDisplayDefinition.Instance.Table;
    public ToolSchema Describe(BlobSettings committedSettings) => new(new[] {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes:FieldEditModes.Reference),
        new Port(InspectionInput, TypeRef.ResourceOf(ResourceKind.Region), false, EditModes:FieldEditModes.Reference),
        new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Region), false, EditModes:FieldEditModes.Reference),
        new Port(PoseInput, SpatialDeclarations.Affine2D, false, EditModes:FieldEditModes.Constant|FieldEditModes.Reference)
    }, BlobSchema.Outputs, workflowImageInputId: BlobParameterDefinition.Instance.WorkflowImageInputId);
    public IReadOnlyList<Diagnostic> Validate(BlobSettings settings) => settings.Validate();
    public ToolCapabilities Capabilities(BlobSettings settings) => new(new[] { "halcon" });
    public IToolSession<BlobSettings, BlobOutput> CreateSession() => new BlobSession();
}
