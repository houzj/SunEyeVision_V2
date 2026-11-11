using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>
/// Inputs borrowed from the host's frozen invocation. The host owns their lifetime;
/// constructing this view does not copy or dispose image/region resources.
/// </summary>
public sealed record BlobInput(IReadOnlyImage Image, IReadOnlyRegion? Inspection,
    IReadOnlyRegion? Mask, RecordValue? Pose,RecordValue? LocalRoi=null,bool IsInheritedRoi=false,IReadOnlyImage? ImageMask=null,
    bool OutputMask=false,RecordValue? Fixture=null,bool IsInheritedMask=false)
{
    public static BlobInput Read(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var outputMask = values.Contains(BlobImageRoi.OutputEnabledId) && values.Get<bool>(BlobImageRoi.OutputEnabledId);
        if (values.Contains(BlobImageRoi.ModeId) && values.Get<long>(BlobImageRoi.ModeId) == 1)
            return new(values.Get<IReadOnlyImage>(BlobTool.ImageInput), null, null, null,
                ImageMask: values.Get<IReadOnlyImage>(BlobImageRoi.ImageId), OutputMask:outputMask);
        var fixture = BlobFixtureInputs.Read(values);
        var localRoi = values.Contains(BlobLocalRoi.InputId) ? values.Get<RecordValue>(BlobLocalRoi.InputId) : BlobLocalRoi.Empty;
        BlobLocalRoi.Validate(localRoi);
        var roi = BlobRoiInheritance.Materialize(values, localRoi);
        return new(values.Get<IReadOnlyImage>(BlobTool.ImageInput), null, null,
            fixture is null ? null : FixtureDeclarations.ToAffine2D(fixture,BlobSchema.ImageFrame),
            BlobMaskInheritance.Materialize(values,roi),
            values.Contains(BlobRoiInheritance.ModeId)&&values.Get<string>(BlobRoiInheritance.ModeId)!=BlobRoiInheritance.Local,
            OutputMask:outputMask,Fixture:fixture,
            IsInheritedMask:values.Contains(BlobMaskInheritance.ModeId)&&values.Get<bool>(BlobMaskInheritance.ModeId));
    }
}
