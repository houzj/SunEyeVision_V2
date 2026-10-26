using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Commom;

public partial class SuperImageButton : Button, IComponentConnector
{
	public static readonly DependencyProperty NormalConnentProperty = DependencyProperty.Register("NormalConnent", typeof(object), typeof(SuperImageButton), new PropertyMetadata(null));

	public static readonly DependencyProperty PressedConnentProperty = DependencyProperty.Register("PressedConnent", typeof(object), typeof(SuperImageButton), new PropertyMetadata(null));

	public static readonly DependencyProperty MOverConnentProperty = DependencyProperty.Register("MOverConnent", typeof(object), typeof(SuperImageButton), new PropertyMetadata(null));

	public static readonly DependencyProperty DisableConnentProperty = DependencyProperty.Register("DisableConnent", typeof(object), typeof(SuperImageButton), new PropertyMetadata(null));

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

	public SuperImageButton()
	{
		InitializeComponent();
	}
}
