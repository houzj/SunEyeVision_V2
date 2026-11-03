using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;

public class PropertyGridEditorShortUpDown : ShortUpDown
{
	static PropertyGridEditorShortUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorShortUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorShortUpDown)));
	}
}
