using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace IMVSPixelCountModuVAControl.UI;

public partial class RangeConfigListPanel : Control, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(RangeConfigListPanel), new PropertyMetadata(null));

	public DataTemplate ItemTemplate
	{
		get
		{
			return (DataTemplate)GetValue(ItemTemplateProperty);
		}
		set
		{
			SetValue(ItemTemplateProperty, value);
		}
	}

	public RangeConfigListPanel()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}
}
