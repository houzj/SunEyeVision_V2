using System;

namespace FewShotCommonControl;

[Serializable]
public class UICategoryData
{
	public bool IsEmptyCategory = false;

	public string CategoryName = "";

	public string LabelColor = "#00FF00";

	public uint CalibratedNum = 0u;

	public uint CalibratedROINum = 0u;

	public uint RegisteredNum = 0u;

	public uint RegisteredROINum = 0u;
}
