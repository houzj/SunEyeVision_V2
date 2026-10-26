using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Commom;

public partial class SuperRadioButton : UserControl, IComponentConnector
{
	public static readonly RoutedEvent CheckedEvent = EventManager.RegisterRoutedEvent("Checked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SuperRadioButton));

	public static readonly RoutedEvent UnCheckedEvent = EventManager.RegisterRoutedEvent("UnChecked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SuperRadioButton));

	public static readonly DependencyProperty NormalConnentProperty = DependencyProperty.Register("NormalConnent", typeof(object), typeof(SuperRadioButton), new PropertyMetadata(null));

	public static readonly DependencyProperty PressedConnentProperty = DependencyProperty.Register("PressedConnent", typeof(object), typeof(SuperRadioButton), new PropertyMetadata(null));

	public static readonly DependencyProperty MOverConnentProperty = DependencyProperty.Register("MOverConnent", typeof(object), typeof(SuperRadioButton), new PropertyMetadata(null));

	public static readonly DependencyProperty DisableConnentProperty = DependencyProperty.Register("DisableConnent", typeof(object), typeof(SuperRadioButton), new PropertyMetadata(null));

	public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool?), typeof(SuperRadioButton), new PropertyMetadata(false));

	public object NormalConnent
	{
		get
		{
			return GetValue(NormalConnentProperty);
		}
		set
		{
			SetValue(NormalConnentProperty, value);
		}
	}

	public object PressedConnent
	{
		get
		{
			return GetValue(PressedConnentProperty);
		}
		set
		{
			SetValue(PressedConnentProperty, value);
		}
	}

	public object MOverConnent
	{
		get
		{
			return GetValue(MOverConnentProperty);
		}
		set
		{
			SetValue(MOverConnentProperty, value);
		}
	}

	public object DisableConnent
	{
		get
		{
			return GetValue(DisableConnentProperty);
		}
		set
		{
			SetValue(DisableConnentProperty, value);
		}
	}

	public bool? IsChecked
	{
		get
		{
			return (bool?)GetValue(IsCheckedProperty);
		}
		set
		{
			SetValue(IsCheckedProperty, value);
		}
	}

	public string GroupName
	{
		get
		{
			return rb.GroupName;
		}
		set
		{
			rb.GroupName = value;
		}
	}

	public event RoutedEventHandler Checked
	{
		add
		{
			AddHandler(CheckedEvent, value);
		}
		remove
		{
			RemoveHandler(CheckedEvent, value);
		}
	}

	public event RoutedEventHandler UnChecked
	{
		add
		{
			AddHandler(UnCheckedEvent, value);
		}
		remove
		{
			RemoveHandler(UnCheckedEvent, value);
		}
	}

	public SuperRadioButton()
	{
		InitializeComponent();
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		RoutedEventArgs e2 = new RoutedEventArgs(CheckedEvent);
		RaiseEvent(e2);
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		RoutedEventArgs e2 = new RoutedEventArgs(UnCheckedEvent);
		RaiseEvent(e2);
	}
}
