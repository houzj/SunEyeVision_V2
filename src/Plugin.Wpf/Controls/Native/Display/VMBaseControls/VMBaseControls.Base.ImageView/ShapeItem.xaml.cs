using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public partial class ShapeItem : DynamicItem, IComponentConnector
{
	public static readonly DependencyProperty ImageSourceWidthProperty = DependencyProperty.Register("ImageSourceWidth", typeof(int), typeof(ShapeItem), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageSourceHeightProperty = DependencyProperty.Register("ImageSourceHeight", typeof(int), typeof(ShapeItem), new PropertyMetadata(0));

	public int ImageSourceWidth
	{
		get
		{
			return (int)GetValue(ImageSourceWidthProperty);
		}
		set
		{
			SetValue(ImageSourceWidthProperty, value);
		}
	}

	public int ImageSourceHeight
	{
		get
		{
			return (int)GetValue(ImageSourceHeightProperty);
		}
		set
		{
			SetValue(ImageSourceHeightProperty, value);
		}
	}

	public ShapeItem()
	{
		InitializeComponent();
	}
}
