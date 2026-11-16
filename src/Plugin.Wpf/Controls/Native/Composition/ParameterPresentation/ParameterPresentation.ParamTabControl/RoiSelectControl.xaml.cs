using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;

public partial class RoiSelectControl : Control, IImageSizeControl, IComponentConnector
{
	public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register("ImageWidth", typeof(int), typeof(RoiSelectControl), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register("ImageHeight", typeof(int), typeof(RoiSelectControl), new PropertyMetadata(0));

	public static readonly DependencyProperty CanUpDateROIProperty = DependencyProperty.Register("CanUpDateROI", typeof(bool), typeof(RoiSelectControl), new PropertyMetadata(true));

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

	public RoiSelectControl()
	{
		// XAML 内的 StaticResource（TextLeft、Roi 按钮样式）在 InitializeComponent 期间即解析，
		// 此时控件尚未挂树、继承不到外层字典；外层不会替子控件兜底。
		// 因此本控件必须自己声明主题字典，且必须在 InitializeComponent 之前建立。
		Resources.MergedDictionaries.Add(EditorResources.Shared);
		InitializeComponent();
	}
}
