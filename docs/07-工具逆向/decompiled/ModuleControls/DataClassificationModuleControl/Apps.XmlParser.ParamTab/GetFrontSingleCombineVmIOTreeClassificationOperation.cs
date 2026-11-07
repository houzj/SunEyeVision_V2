using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.Localization;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontSingleCombineVmIOTreeClassificationOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontSingleCombineVmIOTreeClassificationOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || string.IsNullOrEmpty(TargetName))
		{
			return;
		}
		ConditionItem conditionItem = parentNode.GetParentNode() as ConditionItem;
		List<VmIO> list = (from p in conditionItem.vmModule.Inputs.GetAllLeafIO()
			where ExtraParams.Contains(p.TypeName) && p.CanSubscribeA && p.Visible && !VmModule.fTimeStamp.Contains(p.Name)
			select p).ToList();
		VmModule vmModule = conditionItem.vmModule;
		Holder holder = new Holder
		{
			DisplayNameA = AppLocalizationService.UnDoTranslate(vmModule.ID + " " + vmModule.Name),
			HolderTarget = vmModule
		};
		if (list != null && list.Count > 0)
		{
			List<Holder> forShownList = new List<Holder>();
			Stack stack = new Stack();
			VmIO vmIO = list.FirstOrDefault((VmIO c) => c.Name == "ModuStatus");
			if (vmIO != null)
			{
				list.Remove(vmIO);
			}
			VmIO vmIO2 = list.FirstOrDefault((VmIO c) => c.Name == "ModuRunTime");
			if (vmIO2 != null)
			{
				list.Remove(vmIO2);
			}
			VmIO vmIO3 = list.FirstOrDefault((VmIO c) => c.Name == "DataNum");
			if (vmIO3 != null)
			{
				list.Remove(vmIO3);
			}
			foreach (VmIO item in list)
			{
				Holder holder2 = holder;
				item.BuildFilterTreePath(forShownList, stack);
				while (stack.Count > 0)
				{
					Holder holder3 = stack.Pop() as Holder;
					if (!holder2.EnumEntrysA.Contains(holder3))
					{
						holder3.ParentTree = holder2;
						holder2.EnumEntrysA.Add(holder3);
					}
					holder2 = holder3;
				}
				holder2.EnumEntrysA.Add(item);
			}
		}
		List<Holder> list2 = new List<Holder>();
		list2.Add(holder);
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", list2);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
