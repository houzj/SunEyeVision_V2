using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;

public class DoubleUpDownEditor : UpDownEditor<DoubleUpDown, double?>
{
	protected override DoubleUpDown CreateEditor()
	{
		return new PropertyGridEditorDoubleUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		Editor.AllowInputSpecialValues = AllowedSpecialValues.Any;
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(double)));
	}
}
