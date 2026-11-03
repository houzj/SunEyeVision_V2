using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public class ButtonControl : Button
{
	public static readonly DependencyProperty DataNameProperty = DependencyProperty.Register("DataName", typeof(string), typeof(ButtonControl), new UIPropertyMetadata(null, OnDataNamePropertyChanged));

	public string DataName
	{
		get
		{
			return (string)GetValue(DataNameProperty);
		}
		set
		{
			SetValue(DataNameProperty, value);
		}
	}

	private static void OnDataNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ButtonControl buttonControl = d as ButtonControl;
		Binding binding = new Binding();
		binding.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(BaseWindow), 1);
		binding.Path = new PropertyPath("DataContext." + e.NewValue.ToString());
		buttonControl.SetBinding(ButtonBase.CommandProperty, binding);
	}
}
