using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public class ItemsSourceAttributeEditor : TypeEditor<ComboBox>
{
	private readonly ItemsSourceAttribute _attribute;

	public ItemsSourceAttributeEditor(ItemsSourceAttribute attribute)
	{
		_attribute = attribute;
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = Selector.SelectedValueProperty;
	}

	protected override ComboBox CreateEditor()
	{
		return new PropertyGridEditorComboBox();
	}

	protected override void ResolveValueBinding(PropertyItem propertyItem)
	{
		SetItemsSource();
		base.ResolveValueBinding(propertyItem);
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.DisplayMemberPath = "DisplayName";
		Editor.SelectedValuePath = "Value";
	}

	private void SetItemsSource()
	{
		Editor.ItemsSource = CreateItemsSource();
	}

	private IEnumerable CreateItemsSource()
	{
		return (Activator.CreateInstance(_attribute.Type) as IItemsSource).GetValues();
	}
}
