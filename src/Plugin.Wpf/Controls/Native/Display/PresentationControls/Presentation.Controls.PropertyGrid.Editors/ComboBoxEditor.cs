using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

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
