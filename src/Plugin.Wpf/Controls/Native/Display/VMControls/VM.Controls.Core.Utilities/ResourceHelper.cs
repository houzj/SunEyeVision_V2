using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.IO;
using System.Reflection;
using System.Resources;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

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
