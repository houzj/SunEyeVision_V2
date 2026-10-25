using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>One neutral producer definition; editor and legacy entry points consume this same definition.</summary>
public sealed class BlobDefinition : IToolDefinition<BlobSettings, BlobOutput>
{
    public const string Id = "BlobAnalysis";
    public const string ImageInput = "input.image";
    public const string InspectionInput = "input.inspection.region";
    public const string MaskInput = "input.mask.region";
    public const string PoseInput = "input.pose";
    public string ToolId => Id;
    public ToolSchema Describe(BlobSettings committedSettings) => new(new[] {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(InspectionInput, TypeRef.ResourceOf(ResourceKind.Region), false),
        new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Region), false),
        new Port(PoseInput, SpatialDeclarations.Affine2D, false)
    }, BlobSchema.Outputs);
    public IReadOnlyList<Diagnostic> Validate(BlobSettings settings) => settings.Validate();
    public ToolCapabilities Capabilities(BlobSettings settings) => new(new[] { "halcon" });
    public IToolSession<BlobSettings, BlobOutput> CreateSession() => new BlobSession();
}
