using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

public interface IItemsSource
{
	ItemCollection GetValues();
}
