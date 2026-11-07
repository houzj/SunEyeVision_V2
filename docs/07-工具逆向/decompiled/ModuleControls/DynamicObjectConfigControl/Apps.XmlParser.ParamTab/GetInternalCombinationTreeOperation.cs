using System;
using System.Collections;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetInternalCombinationTreeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetInternalCombinationTreeOperation(string TName)
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
		VmModule vmModule = null;
		object topBaseNode = parentNode.GetTopBaseNode();
		if (topBaseNode is AlgorithmTabRoot)
		{
			AlgorithmTabRoot algorithmTabRoot = topBaseNode as AlgorithmTabRoot;
			if (algorithmTabRoot.Owner != null)
			{
				vmModule = algorithmTabRoot.Owner as VmModule;
			}
		}
		else if (topBaseNode is BaseNode)
		{
			BaseNode baseNode = topBaseNode as BaseNode;
			vmModule = (baseNode as IOwner).Owner as VmModule;
		}
		if (vmModule != null && vmModule != null)
		{
			IList internalCombinationTree = vmModule.GetInternalCombinationTree((VmModule p) => true, "Output", text);
			try
			{
				parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", internalCombinationTree);
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
