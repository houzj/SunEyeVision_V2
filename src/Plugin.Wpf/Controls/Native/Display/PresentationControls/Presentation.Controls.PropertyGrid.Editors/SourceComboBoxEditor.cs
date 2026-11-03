using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Collections;
using System.ComponentModel;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

public class SourceComboBoxEditor : ComboBoxEditor
{
	private ICollection _collection;

	private TypeConverter _typeConverter;

	public SourceComboBoxEditor(ICollection collection, TypeConverter typeConverter)
	{
		_collection = collection;
		_typeConverter = typeConverter;
	}

	protected override IEnumerable CreateItemsSource(PropertyItem propertyItem)
	{
		return _collection;
	}

	protected override IValueConverter CreateValueConverter()
	{
		if (_typeConverter != null && _typeConverter is StringConverter)
		{
			return new SourceComboBoxEditorConverter(_typeConverter);
		}
		return null;
	}
}
