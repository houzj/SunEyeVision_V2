using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob runtime entry. One invocation only; workflow scheduling belongs to the host.</summary>
public sealed class BlobTool : IToolDefinition<BlobSettings, BlobOutput>, IToolResultPresentationProvider, IToolSettingsFields, IToolCatalogProvider
{
    public const string Id = "BlobAnalysis";
    public const string ImageInput = "input.image";
    public const string InspectionInput = "input.inspection.region";
    public const string MaskInput = "input.mask.region";
    public const string PoseInput = "input.pose";
    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog {get;}=new("Blob分析","连通域分析与特征提取","blob","区域分析",10);
    public ToolSchema ResultSchema => new(Array.Empty<Port>(), BlobSchema.Outputs);
    public ResultProjectionDescriptor ResultProjection => BlobDisplayDefinition.Instance.Table;
    public ToolSchema SettingsSchema=>BlobParameterDefinition.Instance.SettingsSchema;
    public InputValues ReadSettings(object settings)=>BlobParameterDefinition.Instance.ReadSettings(settings as BlobSettings??throw new ArgumentException("BlobSettings is required."));
    public object ApplySettings(object settings,IReadOnlyDictionary<string,object?> changes)=>BlobParameterDefinition.Instance.ApplySettings(
        settings as BlobSettings??throw new ArgumentException("BlobSettings is required."),changes);
    public ToolSchema Describe(BlobSettings committedSettings)
    {
        var inputs=new[] {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes:FieldEditModes.Reference),
        new Port(InspectionInput, TypeRef.ResourceOf(ResourceKind.Region), false, EditModes:FieldEditModes.Reference),
        new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Region), false, EditModes:FieldEditModes.Reference),
        new Port(PoseInput, SpatialDeclarations.Affine2D, false, EditModes:FieldEditModes.Constant|FieldEditModes.Reference)
        };
        return new(inputs,BlobSchema.Outputs,new ToolPresentation(inputs.Select((port,index)=>BlobParameterDefinition.Instance.InputPresentation(port,index))),
            workflowImageInputId:BlobParameterDefinition.Instance.WorkflowImageInputId);
    }
    public IReadOnlyList<Diagnostic> Validate(BlobSettings settings) => settings.Validate();
    public ToolCapabilities Capabilities(BlobSettings settings) => new(new[] { "halcon" });
    public IToolSession<BlobSettings, BlobOutput> CreateSession() => new BlobSession();
}
