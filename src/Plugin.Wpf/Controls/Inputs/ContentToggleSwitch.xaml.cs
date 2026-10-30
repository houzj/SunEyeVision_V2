using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Controls.Inputs;

public partial class ContentToggleSwitch : UserControl, IComponentConnector
{
	public delegate void IsCheckedChange(object sender, RoutedEventArgs e);

	public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register("OffContent", typeof(object), typeof(ContentToggleSwitch), new PropertyMetadata(null));

	public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register("OnContent", typeof(object), typeof(ContentToggleSwitch), new PropertyMetadata(null));

	public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(ContentToggleSwitch), new PropertyMetadata(false));

	public object OffContent
	{
		get
		{
			return GetValue(OffContentProperty);
		}
		set
		{
			SetValue(OffContentProperty, value);
		}
	}

	public object OnContent
	{
		get
		{
			return GetValue(OnContentProperty);
		}
		set
		{
			SetValue(OnContentProperty, value);
		}
	}

	public bool IsChecked
	{
		get
		{
			return (bool)GetValue(IsCheckedProperty);
		}
		set
		{
			SetValue(IsCheckedProperty, value);
		}
	}

	public event IsCheckedChange OnCheckedChange;

	public ContentToggleSwitch()
	{
		InitializeComponent();
	}

	private void TO_Checked(object sender, RoutedEventArgs e)
	{
		if (OnCheckedChange != null)
		{
			OnCheckedChange(sender, e);
		}
	}

	private void TO_Unchecked(object sender, RoutedEventArgs e)
	{
		if (OnCheckedChange != null)
		{
			OnCheckedChange(sender, e);
		}
	}
}
