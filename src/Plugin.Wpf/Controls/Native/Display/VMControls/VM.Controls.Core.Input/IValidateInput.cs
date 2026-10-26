using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Input;

public interface IValidateInput
{
	event InputValidationErrorEventHandler InputValidationError;

	bool CommitInput();
}
