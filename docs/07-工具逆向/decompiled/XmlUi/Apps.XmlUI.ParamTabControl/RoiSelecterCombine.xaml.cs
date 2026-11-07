using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Apps.XmlUI.ParamTabControl;

public partial class RoiSelecterCombine : UserControl, IImageSizeControl, IComponentConnector
{
	public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register("ImageWidth", typeof(int), typeof(RoiSelecterCombine), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register("ImageHeight", typeof(int), typeof(RoiSelecterCombine), new PropertyMetadata(0));

	public static readonly DependencyProperty CanUpDateROIProperty = DependencyProperty.Register("CanUpDateROI", typeof(bool), typeof(RoiSelecterCombine), new PropertyMetadata(true));

	public int ImageWidth
	{
		get
		{
			return (int)GetValue(ImageWidthProperty);
		}
		set
		{
			SetValue(ImageWidthProperty, value);
		}
	}

	public int ImageHeight
	{
		get
		{
			return (int)GetValue(ImageHeightProperty);
		}
		set
		{
			SetValue(ImageHeightProperty, value);
		}
	}

	public bool CanUpDateROI
	{
		get
		{
			return (bool)GetValue(CanUpDateROIProperty);
		}
		set
		{
			SetValue(CanUpDateROIProperty, value);
		}
	}

	public RoiSelecterCombine()
	{
		InitializeComponent();
	}
}
