using System;

namespace IMVSInspectModuControl;

[Serializable]
public class SampleImageData
{
	public byte[] ImageBuffer = null;

	public string ImageName;

	public bool IsCalibrated;

	public bool IsOKCalibrated;
}
