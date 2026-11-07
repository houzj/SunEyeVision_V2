using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IMVS_IMAGE_CONFIG
{
	public int nSerial;

	public double nFloatExposure;

	public double nFloatGain;

	public int nIntTriggerTime;

	public int nIntChannel;

	public int nIntBright;

	public double nFloatDistribution;

	public double nFloatIrradiation;

	public int nIndex;

	public int nId;

	public int nDeviceType;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string nchDeviceGuid;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
