using System;
using Apps.Interface;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class HiddenOperationForMarkFind : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public HiddenOperationForMarkFind(string TName)
	{
		TargetName = TName;
	}

	public void ReOperate()
	{
		if (Executor == null || Executor.TargetObj == null)
		{
			return;
		}
		try
		{
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			if (parentNode == null)
			{
				throw new Exception("parentItem not find");
			}
			if (!(parentNode.GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot))
			{
				throw new Exception("AlgorithmTabRoot not find");
			}
			if (!(((IOwner)algorithmTabRoot).Owner is IVmModule vmModule))
			{
				throw new Exception("Module not find");
			}
			int result = 1;
			try
			{
				int.TryParse(((IUserStringData)vmModule.Params)["FeatureMatchType"], out result);
				if (result == 0)
				{
					Executor.TargetObj.CustomVisibleA = false;
					_Working = true;
				}
				else
				{
					Executor.TargetObj.CustomVisibleA = OriginalValue;
					_Working = false;
				}
			}
			catch
			{
				Executor.TargetObj.CustomVisibleA = OriginalValue;
				_Working = false;
			}
		}
		catch
		{
			Executor.TargetObj.CustomVisibleA = OriginalValue;
			_Working = false;
		}
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		Executor.TargetObj.CustomVisibleA = false;
		_Working = true;
	}

	public void GetOriginalValue()
	{
		OriginalValue = true;
	}
}
