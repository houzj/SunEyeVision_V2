using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Models;

public static class BindingHelper
{
	public static bool BindingPath(string path, string dpName, object model, BindingMode bindingMode, FrameworkElement targetElement)
	{
		DependencyProperty dependencyProperty = targetElement.GetDependencyProperty(dpName + "Property");
		if (dependencyProperty == null)
		{
			return false;
		}
		Binding binding = new Binding
		{
			Source = model,
			Path = new PropertyPath(path),
			Mode = bindingMode
		};
		targetElement.SetBinding(dependencyProperty, binding);
		return true;
	}
}
