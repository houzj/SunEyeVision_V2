using SunEyeVision.Plugin.Wpf.Themes.Palettes;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Apps.ColorFun;

public class ColorResourceBinding : Binding
{
	public class ColorViewModel
	{
		public dynamic Color { get; set; }
	}

	public ColorResourceBinding(string path)
	{
		if (ColorThemesResourceDictionary.Instance.Contains(path))
		{
			object color = ColorThemesResourceDictionary.Instance[path];
			Source = new ColorViewModel
			{
				Color = color
			};
			Path = new PropertyPath("Color");
		}
		else
		{
			Trace.TraceWarning($"The color key value was not found：{path}");
		}
	}
}
