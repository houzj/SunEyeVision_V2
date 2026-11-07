using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class Integer_CirclePairInsp : Integer
{
	private object _ROISourceData;

	private DelegateCommand<object> _ExternValueChangedCommand = null;

	public object ROISourceData
	{
		get
		{
			if (_ROISourceData == null)
			{
				AlgorithmTabRoot algorithmTabRoot = GetParentNode<AlgorithmTabRoot>();
				if (algorithmTabRoot != null)
				{
					_ROISourceData = algorithmTabRoot.FindBaseNodeByName("RoiType");
				}
			}
			return _ROISourceData;
		}
	}

	public bool SourceTag
	{
		set
		{
			if (value)
			{
				GetParamUnit();
			}
		}
	}

	public ICommand ExternValueChangedCommand
	{
		get
		{
			if (_ExternValueChangedCommand == null)
			{
				_ExternValueChangedCommand = new DelegateCommand<object>((Action<object>)ExternValueChangedCmdExecute);
			}
			return (ICommand)_ExternValueChangedCommand;
		}
	}

	private void ExternValueChangedCmdExecute(object value)
	{
		int num = Convert.ToInt32(value);
		if (num < MinValue)
		{
			Value = MinValue.ToString();
		}
		else if (num > MaxValue)
		{
			Value = MaxValue.ToString();
		}
		else
		{
			CurValue = num;
		}
		ValueChangedCmdExecute();
	}
}
