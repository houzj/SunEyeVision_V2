using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

public static class UIExtern
{
	public static Transform ToTransform(this GeneralTransformGroup generalTransformGroup)
	{
		if (generalTransformGroup.Children == null || generalTransformGroup.Children.Count == 0)
		{
			return null;
		}
		Matrix identity = Matrix.Identity;
		foreach (Transform child in generalTransformGroup.Children)
		{
			identity *= child.Value;
		}
		return new MatrixTransform(identity);
	}

	public static bool IsInDesignMode(this FrameworkElement control)
	{
		return control != null && DesignerProperties.GetIsInDesignMode(control);
	}
}
