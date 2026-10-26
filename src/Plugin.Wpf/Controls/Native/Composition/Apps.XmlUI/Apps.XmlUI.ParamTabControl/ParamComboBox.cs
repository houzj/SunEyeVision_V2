using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

internal class ParamComboBox : ComboBox
{
	public static readonly DependencyProperty SelectionChangedCommandNameProperty = DependencyProperty.Register("SelectionChangedCommandName", typeof(string), typeof(ParamComboBox), new PropertyMetadata(OnSelectionChangedCommandNamePropertyChanged));

	public static readonly DependencyProperty SelectionChangedCommandProperty = DependencyProperty.Register("SelectionChangedCommand", typeof(ICommand), typeof(ParamComboBox), new PropertyMetadata(null));

	public string SelectionChangedCommandName
	{
		get
		{
			return (string)GetValue(SelectionChangedCommandNameProperty);
		}
		set
		{
			SetValue(SelectionChangedCommandNameProperty, value);
		}
	}

	public ICommand SelectionChangedCommand
	{
		get
		{
			return (ICommand)GetValue(SelectionChangedCommandProperty);
		}
		set
		{
			SetValue(SelectionChangedCommandProperty, value);
		}
	}

	private static void OnSelectionChangedCommandNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != null && e.NewValue.ToString() != "")
		{
			ParamComboBox paramComboBox = d as ParamComboBox;
			Binding binding = new Binding();
			binding.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(BaseWindow), 1);
			binding.Path = new PropertyPath("DataContext." + e.NewValue.ToString());
			paramComboBox.SetBinding(SelectionChangedCommandProperty, binding);
		}
	}

	protected override void OnSelectionChanged(SelectionChangedEventArgs e)
	{
		base.OnSelectionChanged(e);
		if (SelectionChangedCommand != null)
		{
			SelectionChangedCommand.Execute(null);
		}
	}
}
