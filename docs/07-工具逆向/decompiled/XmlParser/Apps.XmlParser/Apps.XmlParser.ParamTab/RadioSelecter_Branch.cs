using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Data;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class RadioSelecter_Branch : NameValueItem
{
	public int CurValue;

	public int DefaultValue;

	public ObservableCollection<object> EnumEntrys;

	private DelegateCommand<object> _ValueChangedCommand;

	public int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			if (CurValue == 0)
			{
				SetListParamEnable(isEnable: true, CurValue);
			}
			else if (1 == CurValue)
			{
				SetListParamEnable(isEnable: false, CurValue);
			}
			OnPropertyChanged("CurValueA");
		}
	}

	public int DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				value = CurValueA.ToString();
			}
			CurValueA = int.Parse(value);
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand<object>((Action<object>)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	private void ValueChangedCmdExecute(object e)
	{
		if (!IsUnConnect())
		{
			CurValueA = (int)e;
			if (OldValue != Value)
			{
				SetParamUnit();
				OldValue = Value;
			}
		}
	}

	private void SetListParamEnable(bool isEnable, int type)
	{
		Tab tab = GetParentNode(2u) as Tab;
		object obj = tab.FindBaseNodeByName("ModuleInfoList");
		if (obj != null)
		{
			Array obj2 = new object[2] { isEnable, type };
			(obj as IBottomCommand).ExecuteFunction(obj2);
		}
	}

	public override int GetParamUnit()
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("r"))
		{
			return -2;
		}
		MyItemState = ItemState.Normal;
		MyItemState = ItemState.Normal;
		IUserStringData moduleTagInfo = GetModuleTagInfo();
		if (moduleTagInfo == null)
		{
			return -1;
		}
		try
		{
			string text = moduleTagInfo[Name];
			try
			{
				OldValue = text;
				Value = text;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show("(BranchModu_STD GetParam)" + ex.Message, MessageIcons.Error);
				LogHelper.Error("分支模块获取参数！！！！！名称：" + Name + " des:" + Description + "  获取值:" + text);
			}
			return 0;
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			return -1;
		}
	}
}
