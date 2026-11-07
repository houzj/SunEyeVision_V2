using Apps.XmlParser.ParamTab;
using VM.Core;

namespace DynamicObjectConfigControl.UI.Data;

public class IOPickData : PickDataBase
{
	public static readonly string[] allType = new string[7] { "int", "float", "string", "image", "pointset", "byte", "double" };

	private VmIOCollection targetVmIOs;

	public VmIOCollection TargetVmIOs
	{
		get
		{
			return targetVmIOs;
		}
		set
		{
			targetVmIOs = value;
		}
	}

	public override void Init()
	{
		if (targetVmIOs == (Node as VmModule).Inputs)
		{
			EnumEntrysA = (Node as VmModule).GetFrontVmIOTree((VmModule p) => true, "Output", allType, IsIncludeSelf: false, IsIncludeInside: false, IsNeedNull: false, IsGlobalVar: false);
		}
		else
		{
			EnumEntrysA = (Node as VmModule).GetInsideVmIOTree((VmModule p) => true, "Output", allType, IsIncludeSelf: false, IsIncludeInside: false, IsNeedNull: false, IsGlobalVar: false);
		}
	}
}
