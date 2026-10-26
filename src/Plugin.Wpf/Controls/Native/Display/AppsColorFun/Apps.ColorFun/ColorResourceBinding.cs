using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Data;
using Apps.Log;

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
			LogHelper.objLog.Error($"The color key value was not found：{path}");
		}
	}
}
