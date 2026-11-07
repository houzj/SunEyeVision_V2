using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Log;
using SaveImageCs;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GetModuleDisplayList_FrontDisplayOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetModuleDisplayList_FrontDisplayOperation(string TName)
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
		if (parentNode == null || !(parentNode.GetTopBaseNode() is AlgorithmTabRoot { Owner: not null, Owner: VmModule owner }) || !(parentNode.GetParentNode() is SubData { EnumEntrys: not null } subData))
		{
			return;
		}
		ButtonSelecter_DisplayObject buttonSelecter_DisplayObject = subData.EnumEntrys.OfType<ButtonSelecter_DisplayObject>().First();
		if (buttonSelecter_DisplayObject == null)
		{
			return;
		}
		VmModule moduleByPath = owner.Root.GetModuleByPath(buttonSelecter_DisplayObject.CurValueA);
		if (moduleByPath == null)
		{
			return;
		}
		string text = "";
		try
		{
			owner.Params["ModuleId"] = moduleByPath.ID.ToString();
			LogHelper.objLog.Info("ModuleId:" + moduleByPath.ID);
			text = owner.Params["ModuleDisplayStatus"];
			LogHelper.objLog.Info("ModuleDisplayStatus:" + text);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
		string[] array = text.Split(';');
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			string[] array3 = text2.Split(':');
			if (array3.Length == 2)
			{
				dictionary.Add(array3[0], array3[1]);
			}
		}
		ParamRoot paramRoot = VmSolution.Instance[((IVmFullPath)moduleByPath).FullPath + ".[Display]"] as ParamRoot;
		List<FrontDisplayItem> list = new List<FrontDisplayItem>();
		foreach (Object display in paramRoot.GetDisplayList())
		{
			if (SaveImageHelper.CheckObject(display) && SaveImageHelper.CheckObjectIsDisplay(moduleByPath, display.NameA))
			{
				bool isDisplay = true;
				if (dictionary.Keys.Contains(display.Name) && dictionary[display.Name] == "0")
				{
					isDisplay = false;
				}
				FrontDisplayItem item = new FrontDisplayItem
				{
					Source = display,
					Owner = moduleByPath,
					Requester = owner,
					IsDisplay = isDisplay
				};
				list.Add(item);
			}
		}
		parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", list);
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
