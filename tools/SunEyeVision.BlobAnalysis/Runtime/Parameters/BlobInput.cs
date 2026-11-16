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
    public static BlobInput Read(InputValues values,Action<string>? timingLog=null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var outputMask = values.Contains(BlobImageRoi.OutputEnabledId) && values.Get<bool>(BlobImageRoi.OutputEnabledId);
        if (values.Contains(BlobImageRoi.ModeId) && values.Get<long>(BlobImageRoi.ModeId) == 1)
        {
            var result = new BlobInput(values.Get<IReadOnlyImage>(BlobTool.ImageInput), null, null, null,
                ImageMask: values.Get<IReadOnlyImage>(BlobImageRoi.ImageId), OutputMask:outputMask);
            BlobInputTimingProbe.Log(timingLog,"image-mask-input");
            return result;
        }
        var fixture = BlobFixtureInputs.Read(values);
        var localRoi = values.Contains(BlobLocalRoi.InputId) ? values.Get<RecordValue>(BlobLocalRoi.InputId) : BlobLocalRoi.Empty;
        BlobLocalRoi.Validate(localRoi);
        var roi = BlobRoiInheritance.Materialize(values, localRoi);
        var output = new BlobInput(values.Get<IReadOnlyImage>(BlobTool.ImageInput), null, null,
            fixture is null ? null : FixtureDeclarations.ToAffine2D(fixture,BlobSchema.ImageFrame),
            BlobMaskInheritance.Materialize(values,roi),
            values.Contains(BlobRoiInheritance.ModeId)&&values.Get<string>(BlobRoiInheritance.ModeId)!=BlobRoiInheritance.Local,
            OutputMask:outputMask,Fixture:fixture,
            IsInheritedMask:values.Contains(BlobMaskInheritance.ModeId)&&values.Get<bool>(BlobMaskInheritance.ModeId));
        BlobInputTimingProbe.Log(timingLog,"normal-input");
        return output;
    }
}

file static class BlobInputTimingProbe
{
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("SUNEYEVISION_TIMING_PROBE"), "1", StringComparison.Ordinal);

    public static void Log(Action<string>? log,string branch)
    {
        if(!Enabled || log is null)return;
        log($"[ExecTimingProbe] tool=blob-entry stage=input-branch branch={branch}");
    }
}
