using System;
using System.Collections;
using Apps.Interface;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontParamItemsOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontParamItemsOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		string text = "";
		if (ExtraParams.Length != 0)
		{
			text = ExtraParams[0];
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || string.IsNullOrEmpty(text))
		{
			return;
		}
		object topBaseNode = parentNode.GetTopBaseNode();
		IOwner owner = null;
		if (topBaseNode is AlgorithmTabRoot)
		{
			owner = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
		}
		else if (topBaseNode is ParamRoot)
		{
			owner = topBaseNode as ParamRoot;
		}
		if (owner != null && owner.Owner != null)
		{
			VmModule vmModule = owner.Owner as VmModule;
			IList frontIOWithLeafItemsByName = vmModule.GetFrontIOWithLeafItemsByName(text);
			try
			{
				parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", frontIOWithLeafItemsByName);
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message);
			}
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
