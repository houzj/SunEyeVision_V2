using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class ListParam : NameValueItem, IBottomCommand
{
	private bool IsEnabled = true;

	private int SelectedType;

	public ObservableCollection<object> CurValue;

	private DelegateCommand _ValueChangedCommand;

	public bool IsEnabledA
	{
		get
		{
			return IsEnabled;
		}
		set
		{
			if (IsEnabled == value)
			{
				return;
			}
			IsEnabled = value;
			if (CurValue != null)
			{
				foreach (object item in CurValue)
				{
					if (item is ListParamItem listParamItem)
					{
						listParamItem.IsEnabledA = value;
					}
				}
			}
			OnPropertyChanged("IsEnabledA");
		}
	}

	public int SelectedTypeA
	{
		get
		{
			return SelectedType;
		}
		set
		{
			if (SelectedType != value)
			{
				SelectedType = value;
				ResetListItemValue();
			}
		}
	}

	public ObservableCollection<object> CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public override string Value
	{
		get
		{
			return List2Str(CurValueA);
		}
		set
		{
			CurValueA = Str2List(value);
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	private ObservableCollection<object> Str2List(string value)
	{
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		if (!string.IsNullOrWhiteSpace(value))
		{
			string[] array = value.Split('$');
			for (int i = 0; i < array.Length - 1; i++)
			{
				string[] array2 = array[i].Split('#');
				string iD = array2[0];
				string text = array2[1];
				ListParamItem listParamItem = new ListParamItem();
				listParamItem.ID = iD;
				listParamItem.SetParentNode(this);
				listParamItem.ValueValue = text;
				if (1 == SelectedType)
				{
					listParamItem.InputValue = listParamItem.BitValue;
					listParamItem.IsEnabledA = false;
				}
				else if (SelectedType == 0)
				{
					listParamItem.InputValue = text;
					listParamItem.IsEnabledA = true;
				}
				observableCollection.Add(listParamItem);
			}
		}
		return observableCollection;
	}

	private string List2Str(ObservableCollection<object> collection)
	{
		string text = "";
		if (collection != null)
		{
			ListParamItem listParamItem = null;
			foreach (object item in collection)
			{
				listParamItem = item as ListParamItem;
				string text2 = "";
				if (listParamItem != null)
				{
					string text3 = listParamItem.InputValue;
					if (string.IsNullOrWhiteSpace(text3))
					{
						text3 = "0";
					}
					text2 = ((listParamItem.BitValue == text3) ? listParamItem.BitValueN : text3);
					text = text + listParamItem.ID + "#" + text2 + "$";
				}
			}
		}
		Debugger.Log(0, "", "[Branch] FACE Set Param Value: " + text + " Length: " + text.Length);
		return text;
	}

	private void ResetListItemValue()
	{
		if (CurValue == null)
		{
			return;
		}
		ListParamItem listParamItem = null;
		foreach (object item in CurValue)
		{
			if (item is ListParamItem listParamItem2)
			{
				if (1 == SelectedType)
				{
					listParamItem2.InputValue = listParamItem2.BitValue;
				}
				else if (SelectedType == 0)
				{
					listParamItem2.InputValue = listParamItem2.ValueValue;
				}
			}
		}
	}

	public void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			SetParamUnit();
			OldValue = Value;
			Debugger.Log(0, "", "[Branch] FACE Set Param Value: " + Value + " Value Length: " + Value.Length);
		}
	}

	public override int GetParamUnit()
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("r"))
		{
			return -2;
		}
		MyItemState = ItemState.Normal;
		StringBuilder stringBuilder = new StringBuilder();
		IUserStringData moduleTagInfo = GetModuleTagInfo();
		if (moduleTagInfo == null)
		{
			return -1;
		}
		try
		{
			string text = moduleTagInfo[Name];
			Debugger.Log(0, "", "[Branch] FACE Get Param Value: " + text + " Value Length: " + text.Length);
			try
			{
				OldValue = text;
				Value = text;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show("(BranchModu_STD GetParam)" + ex.Message, MessageIcons.Error);
				LogHelper.Error("分支模块获取参数！！！！！名称：" + Name + " des:" + Description + "  获取值:" + stringBuilder);
			}
			return 0;
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			return -1;
		}
	}

	public void UpTemplate(ListParamItem item)
	{
		if (CurValue.Contains(item))
		{
			int num = CurValue.IndexOf(item);
			if (num > 0)
			{
				CurValue.Remove(item);
				CurValue.Insert(num - 1, item);
				(CurValue[num] as ListParamItem).UpdateProperty();
				(CurValue[num - 1] as ListParamItem).UpdateProperty();
				ValueChangedCmdExecute();
			}
		}
	}

	public void DownTemplate(ListParamItem item)
	{
		if (CurValue.Contains(item))
		{
			int num = CurValue.IndexOf(item);
			int count = CurValue.Count;
			if (num + 1 < count)
			{
				CurValue.Remove(item);
				CurValue.Insert(num + 1, item);
				(CurValue[num + 1] as ListParamItem).UpdateProperty();
				(CurValue[num] as ListParamItem).UpdateProperty();
				ValueChangedCmdExecute();
			}
		}
	}

	public bool IsFirstElement(ListParamItem item)
	{
		if (CurValue.Contains(item) && CurValue.IndexOf(item) == 0)
		{
			return true;
		}
		return false;
	}

	public bool IsLastElement(ListParamItem item)
	{
		if (CurValue.Contains(item))
		{
			int num = CurValue.IndexOf(item);
			int count = CurValue.Count;
			if (num == count - 1)
			{
				return true;
			}
		}
		return false;
	}

	public object ExecuteFunction(object obj = null)
	{
		if (obj == null)
		{
			return null;
		}
		if (obj is Array array)
		{
			bool flag = false;
			int result = 0;
			flag = (bool)array.GetValue(0);
			int.TryParse(array.GetValue(1).ToString(), out result);
			IsEnabledA = flag;
			SelectedTypeA = result;
		}
		return null;
	}
}
