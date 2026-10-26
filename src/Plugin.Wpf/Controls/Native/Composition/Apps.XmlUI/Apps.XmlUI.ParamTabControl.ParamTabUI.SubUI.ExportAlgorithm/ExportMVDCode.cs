using System;
using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.ParamTabUI.SubUI.ExportAlgorithm;

internal class ExportMVDCode
{
	public struct ModuleConfig
	{
		public byte byROIConfig;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 63)]
		public byte[] byReserved;
	}

	private const string DllPath = "ExportMVDCode.dll";

	[DllImport("ExportMVDCode.dll")]
	public static extern int EXP_Initialize();

	[DllImport("ExportMVDCode.dll")]
	public static extern int EXP_UnInitialize();

	[DllImport("ExportMVDCode.dll")]
	public static extern int EXP_SetModuleConfig(int nModuleId, IntPtr stConfigParam);

	[DllImport("ExportMVDCode.dll")]
	public static extern int EXP_ExportCodeFromData(int nIDEVersion, int nProgLan, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "VM.PlatformSDKCS.UTF8Marshaler, VM.PlatformSDKCS, Version=1.0.0.0, Culture=neutral, PublicKeyToken=c1484a72052ee3d4")] string pszPrjName, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "VM.PlatformSDKCS.UTF8Marshaler, VM.PlatformSDKCS, Version=1.0.0.0, Culture=neutral, PublicKeyToken=c1484a72052ee3d4")] string pszPrjDir, IntPtr pDescData, int nDataLen);

	[DllImport("ExportMVDCode.dll")]
	public static extern IntPtr EXP_GetErrorMsg(int nUserLan, IntPtr nMsgLen);
}
