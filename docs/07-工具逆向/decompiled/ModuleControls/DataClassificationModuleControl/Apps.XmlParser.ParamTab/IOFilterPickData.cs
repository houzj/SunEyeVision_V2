using System.Collections;
using System.Collections.Generic;
using DynamicObjectConfigControl.UI.Data;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class IOFilterPickData : PickDataBase
{
	public static readonly string[] allType = new string[2] { "int", "float" };

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

	public VmModule SelectedModule { get; set; }

	public override void Init()
	{
		IList frontVmIOTree = (base.Node as VmModule).GetFrontVmIOTree((VmModule p) => true, "Output", allType, IsIncludeSelf: false, IsIncludeInside: false, IsNeedNull: false, IsGlobalVar: false);
		List<Holder> list = new List<Holder>();
		foreach (object item in frontVmIOTree)
		{
			if (item is Holder holder && holder.HolderTarget == SelectedModule)
			{
				list.Add(holder);
			}
		}
		((PickDataBase)this).EnumEntrysA = list;
	}
}
