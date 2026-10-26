using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public partial class ImageButton : Button, IComponentConnector
{
	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(string), typeof(ImageButton), new PropertyMetadata(""));

	public string Source
	{
		get
		{
			return (string)GetValue(SourceProperty);
		}
		set
		{
			SetValue(SourceProperty, value);
		}
	}

	public ImageButton()
	{
		InitializeComponent();
	}
}
