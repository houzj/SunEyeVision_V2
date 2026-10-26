using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public class PropertyGridEditorCollectionControl : CollectionControlButton
{
	static PropertyGridEditorCollectionControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorCollectionControl), new FrameworkPropertyMetadata(typeof(PropertyGridEditorCollectionControl)));
	}
}
