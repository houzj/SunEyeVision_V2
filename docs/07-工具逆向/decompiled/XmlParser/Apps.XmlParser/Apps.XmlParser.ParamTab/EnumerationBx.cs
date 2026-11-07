using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using Apps.Data;
using Hik.Xml;
using HikPcUI.Commom;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class EnumerationBx : Enumeration
{
	protected bool isTriggerSelectChange = true;

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
						for (int num = EnumEntrys.Count - 1; num >= 0; num--)
						{
							(EnumEntrys[num] as EnumEntry).IsVisibleA = false;
						}
					}
					else
					{
						for (int num2 = EnumEntrys.Count - 1; num2 >= 0; num2--)
						{
							if (-1 == Array.IndexOf(strInfoList, (EnumEntrys[num2] as EnumEntry).Value.ToString()))
							{
								(EnumEntrys[num2] as EnumEntry).IsVisibleA = false;
							}
							else
							{
								(EnumEntrys[num2] as EnumEntry).IsVisibleA = true;
							}
						}
					}
					int num3 = Convert.ToInt32(strInfoList[0]);
					if (CurValueA != num3)
					{
						CurValueA = num3;
						OldValue = strInfoList[0];
						TriggerCurValueBusiness();
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
		return 0;
	}

	public override void SelectedChangedCmdExecute(object data)
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
			TriggerCurValueBusiness();
			if (num2 >= 0 && (oldValue != null || IsSetValue))
			{
				PreExecuteRun();
			}
			OldValue = Value;
		}
	}

	public bool IsEnumEntrysEqual(ObservableCollection<object> EnumEntrys1, ObservableCollection<object> EnumEntrys2)
	{
		if (EnumEntrys1.Count != EnumEntrys2.Count)
		{
			return false;
		}
		for (int i = 0; i < EnumEntrys1.Count; i++)
		{
			EnumEntry enumEntry = (EnumEntry)EnumEntrys1[i];
			EnumEntry enumEntry2 = (EnumEntry)EnumEntrys2[i];
			if (enumEntry != null && enumEntry2 != null && enumEntry.DisplayName != enumEntry2.DisplayName)
			{
				return false;
			}
		}
		return true;
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		SetTriggerDisable("CurValueA");
		return this;
	}

	protected void TriggerCurValueBusiness()
	{
		try
		{
			TriggerBusiness("CurValueA");
			RaiseOnCurValueChangedEvent();
		}
		catch (Exception)
		{
		}
	}
}
