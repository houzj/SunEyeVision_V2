using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

public class TextBlockEditor : TypeEditor<TextBlock>
{
	protected override TextBlock CreateEditor()
	{
		return new PropertyGridEditorTextBlock();
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = TextBlock.TextProperty;
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
	}
}
