using System;
using System.Runtime.InteropServices;

namespace VisionMaster;

internal static class NativeMethod
{
	private const string encrypt_lib = ".\\PublicFile\\x64\\AuthActivation.dll";

	[DllImport(".\\PublicFile\\x64\\AuthActivation.dll")]
	public static extern int VM_GetLicenseTime(EncryptDefine.AUTH_MACHINE_TYPE enMachineType, string strValue, IntPtr pstLicenseTime);

	[DllImport(".\\PublicFile\\x64\\AuthActivation.dll")]
	public static extern void VM_TrialVersionInit();
}
