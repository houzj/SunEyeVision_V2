using System;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class GetConfigOperation : IOperation
{
	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public GetConfigOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		string key = parentNode.ComName;
		if (ExtraParams != null && ExtraParams.Length != 0)
		{
			key = ExtraParams[0];
		}
		if (!(parentNode is IDataType))
		{
			return;
		}
		switch ((parentNode as IDataType).CurrentType)
		{
		case DataType.BOOLTYPE:
		{
			string config = ConfigurationHelper.GetConfig(key);
			bool result2;
			if (int.TryParse(config, out var result))
			{
				parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", result == 1);
			}
			else if (bool.TryParse(config, out result2))
			{
				parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", result2);
			}
			break;
		}
		case DataType.INTTYPE:
		{
			int num = Convert.ToInt32(ConfigurationHelper.GetConfig(key));
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", num);
			break;
		}
		}
	}

	public void ReOperate()
	{
	}
}
