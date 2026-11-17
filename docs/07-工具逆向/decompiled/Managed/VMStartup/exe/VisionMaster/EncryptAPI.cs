using System;

namespace VisionMaster;

public static class EncryptAPI
{
	public static int GetLicenseTime(EncryptDefine.AUTH_MACHINE_TYPE enMachineType, string strValue, IntPtr pstLicenseTime)
	{
		return NativeMethod.VM_GetLicenseTime(enMachineType, strValue, pstLicenseTime);
	}

	public static void TrialVersionInit()
	{
		NativeMethod.VM_TrialVersionInit();
	}
}
