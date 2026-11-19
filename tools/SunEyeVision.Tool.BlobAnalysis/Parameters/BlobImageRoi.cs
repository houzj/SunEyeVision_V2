using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public static class BlobImageRoi
{
    public const string ModeId = "input.roi.area-type";
    public const string ImageId = "input.roi.mask-image";
    public const string OutputEnabledId = "input.roi.output-mask";
    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(ModeId, DataType.Integer, Required:false, EditModes:FieldEditModes.Constant),
        new Port(ImageId, TypeRef.ResourceOf(ResourceKind.Image), Required:false, EditModes:FieldEditModes.Reference),
        // VM OutputMaskImage is a normal RW Boolean with a false default; it has
        // no unset/clear branch, so the original switch does not show the × action.
        // The host keeps this input optional for legacy configurations. The
        // formal editor projects the VM false default and suppresses its clear
        // affordance because the VM switch has no unset state.
        new Port(OutputEnabledId, DataType.Boolean, Required:false, EditModes:FieldEditModes.Constant)
    });

    public static bool IsImageMode(ParameterSetting setting) => setting switch
    {
        UnsetSetting or ConstantSetting { Value:0L } => false,
        ConstantSetting { Value:1L } => true,
        _ => throw new ArgumentException("ROI区域类型必须是图形或图像。")
    };
}
