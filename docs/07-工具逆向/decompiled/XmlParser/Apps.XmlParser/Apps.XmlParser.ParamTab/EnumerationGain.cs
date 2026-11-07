using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using HikPcUI.Commom;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class EnumerationGain : Enumeration
{
	private bool isTriggerSelectChange = true;

	private ICommand _SelectedChangedCommand = null;

	public override string DisplayType => "Enumeration";

	public new ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectedChangedCmdExecute);
			}
			return _SelectedChangedCommand;
		}
		set
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected Obj, but got Unknown
			_SelectedChangedCommand = (ICommand)(DelegateCommand)value;
		}
	}

	public override int GetParamUnit()
	{
		MyItemState = ItemState.Normal;
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			return -1;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string strValue = string.Empty;
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, Name, ref strValue);
			stringBuilder.Append(strValue);
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			return -1;
		}
		string[] strInfoList = stringBuilder.ToString().Split('$');
		if (strInfoList.Length >= 1)
		{
			try
			{
				CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
				{
					isTriggerSelectChange = false;
					if (strInfoList[0] == "NULL")
					{
						strInfoList[0] = "-1";
					}
					else
					{
						if (EnumEntrysA == null)
						{
							EnumEntrysA = new ObservableCollection<object>();
						}
						EnumEntrysA.Clear();
						for (int i = 1; i < strInfoList.Length; i++)
						{
							double num = Math.Round(float.Parse(strInfoList[i]) / 1000f, 1);
							EnumEntry item = new EnumEntry
							{
								DisplayNameA = $"{num:F1}" + "x",
								ValueA = int.Parse(strInfoList[i])
							};
							EnumEntrysA.Add(item);
						}
					}
					int num2 = Convert.ToInt32(strInfoList[0]);
					if (CurValueA != num2)
					{
						CurValueA = num2;
						OldValue = strInfoList[0];
					}
				});
			}
			catch (Exception)
			{
			}
			finally
			{
				isTriggerSelectChange = true;
			}
		}
		OnPropertyChanged("CurValueA");
		return 0;
	}

	public new void SelectedChangedCmdExecute(object data)
	{
		if (data == null)
		{
			return;
		}
		int num = (int)data;
		if (num >= 0 && !IsUnConnect() && isTriggerSelectChange && OldValue != Value && Value != "-1")
		{
			string oldValue = OldValue;
			int num2 = SetParamUnit();
			if (num2 >= 0 && (oldValue != null || IsSetValue))
			{
				PreExecuteRun();
			}
			OldValue = Value;
		}
	}
}
