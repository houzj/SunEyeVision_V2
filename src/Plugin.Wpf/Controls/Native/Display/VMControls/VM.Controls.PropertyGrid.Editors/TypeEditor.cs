using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public abstract class TypeEditor<T> : ITypeEditor where T : FrameworkElement, new()
{
	protected T Editor { get; set; }

	protected DependencyProperty ValueProperty { get; set; }

	public virtual FrameworkElement ResolveEditor(PropertyItem propertyItem)
	{
		Editor = CreateEditor();
		SetValueDependencyProperty();
		SetControlProperties(propertyItem);
		ResolveValueBinding(propertyItem);
		return Editor;
	}

	protected virtual T CreateEditor()
	{
		return new T();
	}

	protected virtual IValueConverter CreateValueConverter()
	{
		return null;
	}

	protected virtual void ResolveValueBinding(PropertyItem propertyItem)
	{
		Binding binding = new Binding("Value");
		binding.Source = propertyItem;
		binding.UpdateSourceTrigger = ((Editor is InputBase) ? UpdateSourceTrigger.PropertyChanged : UpdateSourceTrigger.Default);
		binding.Mode = (propertyItem.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay);
		binding.Converter = CreateValueConverter();
		BindingOperations.SetBinding(Editor, ValueProperty, binding);
	}

	protected virtual void SetControlProperties(PropertyItem propertyItem)
	{
	}

	protected abstract void SetValueDependencyProperty();
}
