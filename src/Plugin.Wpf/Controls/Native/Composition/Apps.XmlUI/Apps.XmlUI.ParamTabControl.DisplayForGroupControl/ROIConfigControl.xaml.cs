using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.DisplayForGroupControl;

public partial class ROIConfigControl : Control, IComponentConnector, IStyleConnector
{
	private ImageViewBox imageView;

	private Grid viewbox;

	public ROIConfigControl()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		imageView = Template.FindName("imageView", this) as ImageViewBox;
		viewbox = Template.FindName("viewbox", this) as Grid;
	}

	private void BtnToolRegionScale_Click(object sender, RoutedEventArgs e)
	{
		imageView.Init();
	}

	private void m_btnFullScreen_Click(object sender, RoutedEventArgs e)
	{
		FullScreen instance = FullScreen.Instance;
		instance.DataContext = viewbox.DataContext;
		viewbox.Children.Remove(imageView);
		instance.SetChild(imageView);
		instance.ShowDialog();
		instance.RemoveChild();
		instance.DataContext = null;
		viewbox.Children.Add(imageView);
	}
}
