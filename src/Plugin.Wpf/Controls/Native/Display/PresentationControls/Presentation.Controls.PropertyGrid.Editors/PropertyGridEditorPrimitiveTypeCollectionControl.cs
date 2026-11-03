using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

public class PropertyGridEditorPrimitiveTypeCollectionControl : PrimitiveTypeCollectionControl
{
	static PropertyGridEditorPrimitiveTypeCollectionControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorPrimitiveTypeCollectionControl), new FrameworkPropertyMetadata(typeof(PropertyGridEditorPrimitiveTypeCollectionControl)));
	}
}
