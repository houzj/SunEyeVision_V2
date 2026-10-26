using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class ShapesConfigTreeView : Control, IComponentConnector
{
	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IList), typeof(ShapesConfigTreeView), new PropertyMetadata(null));

	public IList ItemsSource
	{
		get
		{
			return (IList)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

	public ShapesConfigTreeView()
	{
		InitializeComponent();
	}
}
