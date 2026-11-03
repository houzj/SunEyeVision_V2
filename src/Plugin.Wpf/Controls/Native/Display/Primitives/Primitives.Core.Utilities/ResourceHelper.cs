using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.IO;
using System.Reflection;
using System.Resources;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Utilities;

internal class ResourceHelper
{
	internal static Stream LoadResourceStream(Assembly assembly, string resId)
	{
		ResourceManager resourceManager = new ResourceManager(Path.GetFileNameWithoutExtension(assembly.ManifestModule.Name) + ".g", assembly);
		resId = resId.ToLower();
		resId = resId.Replace('\\', '/');
		return resourceManager.GetObject(resId) as Stream;
	}
}
