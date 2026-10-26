using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public partial class ParamCategoryUI : ListView, IComponentConnector
{
	public static readonly DependencyProperty ChildLeftWidthProperty = DependencyProperty.Register("ChildLeftWidth", typeof(double), typeof(ParamCategoryUI), new PropertyMetadata(89.0));

	public static readonly DependencyProperty ChildMarginProperty = DependencyProperty.Register("ChildMargin", typeof(Thickness), typeof(ParamCategoryUI), new PropertyMetadata(new Thickness(34.0, 1.0, 40.0, 0.0)));

	public double ChildLeftWidth
	{
		get
		{
			return (double)GetValue(ChildLeftWidthProperty);
		}
		set
		{
			SetValue(ChildLeftWidthProperty, value);
		}
	}

	public Thickness ChildMargin
	{
		get
		{
			return (Thickness)GetValue(ChildMarginProperty);
		}
		set
		{
			SetValue(ChildMarginProperty, value);
		}
	}

	public ParamCategoryUI()
	{
		InitializeComponent();
	}
}
