using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public class FontComboBoxEditor : ComboBoxEditor
{
	protected override IEnumerable CreateItemsSource(PropertyItem propertyItem)
	{
		if (propertyItem.PropertyType == typeof(FontFamily))
		{
			return FontUtilities.Families.OrderBy((FontFamily x) => x.Source);
		}
		if (propertyItem.PropertyType == typeof(FontWeight))
		{
			return FontUtilities.Weights;
		}
		if (propertyItem.PropertyType == typeof(FontStyle))
		{
			return FontUtilities.Styles;
		}
		if (propertyItem.PropertyType == typeof(FontStretch))
		{
			return FontUtilities.Stretches;
		}
		return null;
	}
}
