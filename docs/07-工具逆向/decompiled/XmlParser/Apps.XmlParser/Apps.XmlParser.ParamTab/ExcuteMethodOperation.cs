using System;
using System.Reflection;

namespace Apps.XmlParser.ParamTab;

public class ExcuteMethodOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public ExcuteMethodOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem nameValueItem = Executor.TargetObj ?? Executor.GetParentNode<NameValueItem>();
		string text = "";
		if (ExtraParams.Length != 0)
		{
			text = ExtraParams[0];
		}
		if (nameValueItem != null && !string.IsNullOrEmpty(text))
		{
			Type type = nameValueItem.GetType();
			try
			{
				MethodInfo method = type.GetMethod(text);
				method.Invoke(nameValueItem, null);
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message);
			}
		}
	}

	private void GetTargetObj()
	{
		if (!string.IsNullOrEmpty(TargetName) && Executor.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
