using System.IO;
using System.Reflection;
using System.Resources;

namespace VM.Controls.Core.Utilities;

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
