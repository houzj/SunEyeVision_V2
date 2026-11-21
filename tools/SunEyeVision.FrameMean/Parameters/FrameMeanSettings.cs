using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>The sole persisted source definition; one frozen value set per invocation.</summary>
public sealed record FrameMeanSettings : IToolSettings<FrameMeanSettings>
{
    /// <summary>原厂 ProcStep 默认值 "1"（IMVSFrameMeanModu.xml DefaultValue 证据）。</summary>
    public string ProcStep { get; init; } = "1";

    public FrameMeanSettings Freeze()=>this;

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors=new List<Diagnostic>();
        if(!FrameMeanFields.ProcStepKeys.Contains(ProcStep,StringComparer.Ordinal))
            errors.Add(new("FRAME_MEAN_SETTINGS","处理步骤无效。",FrameMeanFields.ProcStep));
        return errors.AsReadOnly();
    }
}
