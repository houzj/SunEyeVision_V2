using SunEyeVision.Plugin.Wpf.Interop.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Extern;
using SunEyeVision.Plugin.Wpf.Native.Common;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;

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
