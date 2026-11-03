using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

public class PropertyGridEditorTimeSpanUpDown : TimeSpanUpDown
{
	static PropertyGridEditorTimeSpanUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorTimeSpanUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorTimeSpanUpDown)));
	}
}
