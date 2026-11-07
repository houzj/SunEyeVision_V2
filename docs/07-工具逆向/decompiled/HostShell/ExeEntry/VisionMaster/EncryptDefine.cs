using System;
using System.Runtime.InteropServices;

namespace VisionMaster;

public class EncryptDefine
{
	public enum AUTH_LOCALHOST_LICENSE_TYPE
	{
		AUTH_LOCALHOST_LICENSE_UNDEFINE,
		AUTH_LOCALHOST_LICENSE_PERPETUAL,
		AUTH_LOCALHOST_LICENSE_LIMIT_VALUE
	}

	public enum AUTH_MACHINE_TYPE
	{
		AUTH_MACHINE_UNDEFINE,
		AUTH_LOCAL_MACHINE,
		AUTH_REMOTE_MACHINE
	}

	[Serializable]
	public struct AUTH_LOCALHOST_LICENSE_TIME
	{
		public AUTH_LOCALHOST_LICENSE_TYPE enLicenseType;

		public uint nAvailableDay;

		public uint nAvailableCount;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public uint[] nReserved;
	}
}
