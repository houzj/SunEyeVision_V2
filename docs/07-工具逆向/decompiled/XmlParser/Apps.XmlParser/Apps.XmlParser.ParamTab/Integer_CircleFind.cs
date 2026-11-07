using System;
using System.Windows.Input;
using Mvvm.Commands;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class Integer_CircleFind : Integer, INeedGroupRunParamSubstitute
{
	public string ROIBindingPath;

	private DelegateCommand<object> _ExternValueChangedCommand = null;

	private object _ROISourceData;

	public string BindingPathA
	{
		get
		{
			if (!string.IsNullOrEmpty(ROIBindingPath))
			{
				return "ROISourceData.CurROIData." + ROIBindingPath;
			}
			return null;
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

	private void ExternValueChangedCmdExecute(object value)
	{
		Value = value.ToString();
		if (CurValue < MinValue)
		{
			CurValueA = MinValue;
		}
		if (CurValue > MaxValue)
		{
			CurValueA = MaxValue;
		}
		ValueChangedCmdExecute();
	}

	public override void Check(string value)
	{
		RangeChecker rangeChecker = new RangeChecker();
		int minValue = MinValue;
		int maxValue = MaxValue;
		rangeChecker.Check(new object[2] { minValue, maxValue }, value, DisplayNameA);
	}

	protected override bool CheckBeforSet()
	{
		return true;
	}

	object INeedGroupRunParamSubstitute.GetGroupRunParamSubstitute(object tag)
	{
		return CloneAsBase(typeof(Integer));
	}
}
