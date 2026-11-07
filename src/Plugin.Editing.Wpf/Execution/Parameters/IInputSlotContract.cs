using System.Collections.Generic;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;

public interface IInputSlotContract
{
    IReadOnlyList<InputSlotDescriptor> GetInputSlots();
}
