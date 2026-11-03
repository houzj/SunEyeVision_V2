using SunEyeVision.Plugin.Wpf.Interop.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VisualElements;
using SunEyeVision.Plugin.Wpf.Native.Common;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView;

public class AdditionTemplateSelector : DataTemplateSelector
{
	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		var view = container.GetParent<ImageViewBox>();
		if (view?.ExtensionProvider is {} provider)
		{
			provider.PrepareResources(view, profile: false);
			return (DataTemplate)(container as FrameworkElement).FindResource("PointCloundCanvas");
		}
		return null;
	}
}
