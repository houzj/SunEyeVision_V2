using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class MyDataTemplateSelector : DataTemplateSelector
{
	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		FrameworkElement frameworkElement = container as FrameworkElement;
		DataTemplate result = null;
		if (item != null && frameworkElement != null)
		{
			result = ((!(item.GetType().GetProperty("Type").GetValue(item)
				.ToString() == "PointF")) ? (frameworkElement.FindResource("ParamObject") as DataTemplate) : (frameworkElement.FindResource("PointFParam") as DataTemplate));
		}
		return result;
	}
}
