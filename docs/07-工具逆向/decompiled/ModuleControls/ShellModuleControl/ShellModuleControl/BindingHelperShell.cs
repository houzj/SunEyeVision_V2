using System.Windows;
using System.Windows.Data;
using VM.Utility;

namespace ShellModuleControl;

public static class BindingHelperShell
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
