using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;

public abstract class ComboBoxEditor : TypeEditor<ComboBox>
{
	protected override void SetValueDependencyProperty()
	{
		ValueProperty = Selector.SelectedItemProperty;
	}

	protected override ComboBox CreateEditor()
	{
		return new PropertyGridEditorComboBox();
	}

	protected override void ResolveValueBinding(PropertyItem propertyItem)
	{
		SetItemsSource(propertyItem);
		base.ResolveValueBinding(propertyItem);
	}

	protected abstract IEnumerable CreateItemsSource(PropertyItem propertyItem);

	private void SetItemsSource(PropertyItem propertyItem)
	{
		Editor.ItemsSource = CreateItemsSource(propertyItem);
	}
}
