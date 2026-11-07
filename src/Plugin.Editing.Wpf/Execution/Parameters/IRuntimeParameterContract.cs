using System.Collections.Generic;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;

public interface IRuntimeParameterContract
{
    IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters();
}
