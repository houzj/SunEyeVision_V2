using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetInsideAndSelfInputFilterTreeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetInsideAndSelfInputFilterTreeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null || ExtraParams.Length < 1)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || !(parentNode.GetTopBaseNode() is BaseNode baseNode) || (baseNode as IOwner).Owner == null)
		{
			return;
		}
		VmModule node = (baseNode as IOwner).Owner as VmModule;
		List<Holder> list = VmModuleExtern.GetFrontFilterTree(node, (Func<VmModule, bool>)((VmModule p) => true), "Output", ExtraParams, true, true, false, false, "", true) as List<Holder>;
		Holder holder = list?.Find((Holder p) => p.HolderTarget == node);
		int num = -1;
		if (holder != null)
		{
			num = list.IndexOf(holder);
			list.Remove(holder);
		}
		holder = GetInputTreeNode(node, ExtraParams);
		if (num >= 0)
		{
			list.Insert(num, holder);
		}
		else
		{
			list.Add(holder);
		}
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", list);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	private Holder GetInputTreeNode(VmModule vmModule, string[] ValueType, string SearchFilter = "")
	{
		if (vmModule.Inputs.Count > 0)
		{
			VmIOCollection inputs = vmModule.Inputs;
			List<VmIO> list = (from p in inputs.GetAllLeafIO()
				where ValueType.Contains(p.TypeName) && p.CanSubscribeA && p.Visible && !VmModule.fTimeStamp.Contains(p.Name)
				select p).ToList();
			List<Holder> forShownList = new List<Holder>();
			if (list != null && list.Count > 0)
			{
				Holder holder = new Holder
				{
					DisplayNameA = AppLocalizationService.UnDoTranslate(vmModule.ID + " " + vmModule.Name),
					HolderTarget = vmModule
				};
				Stack stack = new Stack();
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
				return holder;
			}
		}
		return null;
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
