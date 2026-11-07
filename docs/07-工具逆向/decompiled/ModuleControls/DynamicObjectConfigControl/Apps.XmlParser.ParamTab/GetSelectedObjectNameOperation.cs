using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetSelectedObjectNameOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSelectedObjectNameOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		int num = 0;
		if (parentNode is ButtonSelecter)
		{
			num = (parentNode as ButtonSelecter).Type;
		}
		string name = "";
		if (ExtraParams.Length != 0)
		{
			name = ExtraParams[0];
		}
		if (parentNode == null)
		{
			return;
		}
		BaseNode baseNode = parentNode.GetTopBaseNode() as BaseNode;
		if (!((baseNode as IOwner)?.Owner is VmModule vmModule))
		{
			return;
		}
		Object obj = null;
		obj = vmModule.GetObject(name);
		string text = (obj as DynamicObject).LoadExternInfo("OriSelectedObj");
		try
		{
			if (!string.IsNullOrEmpty(text))
			{
				List<VmIO> list = null;
				list = ((!obj.TypeA.Equals("text")) ? obj.FeaturesB[0].vmIOs : obj.FeaturesB.FirstOrDefault((FeatureBase p) => p.NameA == "Content")?.vmIOs);
				if (list != null && list.Count > 0)
				{
					string[] array = list[0].SubscriptionInfo.Split(' ', '.');
					string[] array2 = text.Split('.');
					if (array.Length == 3 && array2.Length == 2 && !array[1].Equals(array2[0]))
					{
						array2[0] = array[1];
						text = string.Join(".", array2);
						(obj as DynamicObject).SaveExternInfo("OriSelectedObj", text);
					}
				}
			}
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", text);
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
