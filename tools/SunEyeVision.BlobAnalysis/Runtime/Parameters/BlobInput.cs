using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>
/// Inputs borrowed from the host's frozen invocation. The host owns their lifetime;
/// constructing this view does not copy or dispose image/region resources.
/// </summary>
public sealed record BlobInput(IReadOnlyImage Image, IReadOnlyRegion? Inspection,
    IReadOnlyRegion? Mask, RecordValue? Pose,RecordValue? LocalRoi=null,bool IsInheritedRoi=false)
{
    public static BlobInput Read(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(values.Get<IReadOnlyImage>(BlobTool.ImageInput),
            values.Contains(BlobTool.InspectionInput) ? values.Get<IReadOnlyRegion>(BlobTool.InspectionInput) : null,
            values.Contains(BlobTool.MaskInput) ? values.Get<IReadOnlyRegion>(BlobTool.MaskInput) : null,
            values.Contains(BlobTool.PoseInput) ? values.Get<RecordValue>(BlobTool.PoseInput) : null,
            BlobRoiInheritance.Materialize(values,values.Contains(BlobLocalRoi.InputId)?values.Get<RecordValue>(BlobLocalRoi.InputId):null),
            values.Contains(BlobRoiInheritance.ModeId)&&values.Get<string>(BlobRoiInheritance.ModeId)!=BlobRoiInheritance.Local);
    }
}
