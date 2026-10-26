using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal class ValueChangeHelper : DependencyObject
{
	private class BlankMultiValueConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			return new object();
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new InvalidOperationException();
		}
	}

	private static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object), typeof(ValueChangeHelper), new UIPropertyMetadata(null, OnValueChanged));

	private object Value
	{
		get
		{
			return GetValue(ValueProperty);
		}
		set
		{
			SetValue(ValueProperty, value);
		}
	}

	public event EventHandler ValueChanged;

	private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		((ValueChangeHelper)sender).RaiseValueChanged();
	}

	public ValueChangeHelper(Action changeCallback)
	{
		if (changeCallback == null)
		{
			throw new ArgumentNullException("changeCallback");
		}
		ValueChanged += (object s, EventArgs args) =>
		{
			changeCallback();
		};
	}

	public void UpdateValueSource(object sourceItem, string path)
	{
		BindingBase binding = null;
		if (sourceItem != null && path != null)
		{
			binding = new Binding(path)
			{
				Source = sourceItem
			};
		}
		UpdateBinding(binding);
	}

	public void UpdateValueSource(IEnumerable sourceItems, string path)
	{
		BindingBase binding = null;
		if (sourceItems != null && path != null)
		{
			MultiBinding multiBinding = new MultiBinding();
			multiBinding.Converter = new BlankMultiValueConverter();
			foreach (object sourceItem in sourceItems)
			{
				multiBinding.Bindings.Add(new Binding(path)
				{
					Source = sourceItem
				});
			}
			binding = multiBinding;
		}
		UpdateBinding(binding);
	}

	private void UpdateBinding(BindingBase binding)
	{
		if (binding != null)
		{
			BindingOperations.SetBinding(this, ValueProperty, binding);
		}
		else
		{
			ClearBinding();
		}
	}

	private void ClearBinding()
	{
		BindingOperations.ClearBinding(this, ValueProperty);
	}

	private void RaiseValueChanged()
	{
		if (ValueChanged != null)
		{
			ValueChanged(this, EventArgs.Empty);
		}
	}
}
