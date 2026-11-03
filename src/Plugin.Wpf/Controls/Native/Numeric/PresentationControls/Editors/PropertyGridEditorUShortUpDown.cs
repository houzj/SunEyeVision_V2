using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;

internal class PropertyGridEditorUShortUpDown : UShortUpDown
{
	static PropertyGridEditorUShortUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorUShortUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorUShortUpDown)));
	}
}
