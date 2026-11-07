using System;
using System.Collections;
using Apps.Interface;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontFilterTreeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontFilterTreeOperation(string TName)
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
		object topBaseNode = parentNode.GetTopBaseNode();
		IOwner owner = null;
		if (topBaseNode is AlgorithmTabRoot)
		{
			owner = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
		}
		else if (topBaseNode is DynamicIOContainer<DynamicIO> { DataSource: var dataSource })
		{
			bool isNeedNull = true;
			IList frontFilterTree = dataSource.GetFrontFilterTree((VmModule p) => true, "Output", ExtraParams, IsIncludeSelf: false, IsIncludeInside: false, isNeedNull);
			try
			{
				parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", frontFilterTree);
				return;
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message);
			}
		}
		if (owner != null && owner.Owner != null)
		{
			VmModule vmModule = owner.Owner as VmModule;
			bool isNeedNull2 = true;
			IList frontFilterTree2 = vmModule.GetFrontFilterTree((VmModule p) => true, "Output", ExtraParams, IsIncludeSelf: false, IsIncludeInside: false, isNeedNull2);
			try
			{
				parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", frontFilterTree2);
			}
			catch (Exception ex2)
			{
				throw new Exception(ex2.Message);
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
