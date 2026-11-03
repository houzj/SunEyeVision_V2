using SunEyeVision.Plugin.Wpf.Themes.Palettes;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ThemeColors;

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
