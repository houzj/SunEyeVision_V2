using System.Runtime.InteropServices;

namespace VisionMaster.AppMainWindow;

internal class AlgorithmVersion
{
	public struct VERSION_INFO
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1000)]
		public byte[] chszVersion;
	}

	private const int MAX_VERSION_INFO_LENGTH = 1000;

	[DllImport("myLibs/AlgorithmVersion.dll")]
	public static extern int GetAlgorithmVersion(ref VERSION_INFO versionInfo);
}
