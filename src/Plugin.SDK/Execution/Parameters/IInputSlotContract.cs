using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public interface IInputSlotContract
{
    IReadOnlyList<InputSlotDescriptor> GetInputSlots();
}
