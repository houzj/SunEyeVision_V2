using System;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetSelectedObjectMapOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSelectedObjectMapOperation(string TName)
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
		VmIO vmIO = obj.GetDirectMappingIO() as VmIO;
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", vmIO?.ModulePathName);
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
