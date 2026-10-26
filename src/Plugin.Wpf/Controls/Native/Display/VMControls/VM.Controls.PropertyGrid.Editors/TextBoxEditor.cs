using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel.DataAnnotations;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

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
