using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.ComponentModel.DataAnnotations;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

public class TextBoxEditor : TypeEditor<WatermarkTextBox>
{
	protected override WatermarkTextBox CreateEditor()
	{
		return new PropertyGridEditorTextBox();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(propertyItem.PropertyDescriptor);
		if (attribute != null)
		{
			Editor.Watermark = attribute.GetPrompt();
		}
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = TextBox.TextProperty;
	}
}
