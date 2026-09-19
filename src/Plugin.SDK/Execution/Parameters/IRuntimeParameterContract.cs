using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public interface IRuntimeParameterContract
{
    IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters();
}
