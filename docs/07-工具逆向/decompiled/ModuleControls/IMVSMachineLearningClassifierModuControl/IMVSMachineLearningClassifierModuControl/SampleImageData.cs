using System;

namespace IMVSMachineLearningClassifierModuControl;

[Serializable]
public class SampleImageData
{
	public string ImagePath = "";

	public string ImageLabel = "";

	public bool IsCalibrated = false;

	public bool IsOKCalibrated = false;

	public byte[] RoiData = null;
}
