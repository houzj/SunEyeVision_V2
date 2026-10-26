using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Common;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public class AdditionWTemplateSelector : DataTemplateSelector
{
	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		var view = container.GetParent<ImageViewBox>();
		if (view?.ExtensionProvider is {} provider)
		{
			provider.PrepareResources(view, profile: true);
			return (DataTemplate)(container as FrameworkElement).FindResource("WCustomPlotCanvas");
		}
		return null;
	}
}
