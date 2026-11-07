using System;
using System.Collections.ObjectModel;
using System.Text;
using Apps.Data;
using HikPcUI.Commom;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class EnumerationBxDynamic : EnumerationBx, IDynamicCameraProperty
{
	private string PreKey = "_EnumWithName";

	private bool isTriggerGetParamUnit = true;

	public override string DisplayType
	{
		get
		{
			return "EnumerationBxDynamic";
		}
		set
		{
		}
	}

	public bool IsTriggerChanged { get; set; } = false;

	public bool IsNotifyRefreshParam { get; set; } = false;

	public override int GetParamUnit()
	{
		if (!isTriggerGetParamUnit)
		{
			return 0;
		}
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
			if (RemoteOperate.IsRemoteMode)
			{
				AlgorithmTabRoot algorithRoot = GetTopBaseNode() as AlgorithmTabRoot;
				IsGetFromDictionary(ref strValue, algorithRoot);
			}
			else
			{
				ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, Name + PreKey, ref strValue);
			}
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
						EnumEntrysA = new ObservableCollection<object>();
						for (int i = 1; i < strInfoList.Length; i++)
						{
							EnumEntry item = new EnumEntry
							{
								DisplayNameA = strInfoList[i].Split(':')[1],
								ValueA = int.Parse(strInfoList[i].Split(':')[0])
							};
							EnumEntrysA.Add(item);
						}
					}
					int num = Convert.ToInt32(strInfoList[0]);
					if (CurValueA != num)
					{
						CurValueA = num;
						OldValue = strInfoList[0];
						TriggerCurValueBusiness();
					}
					else
					{
						OnPropertyChanged("CurValueA");
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
		if (IsTriggerChanged || data == null)
		{
			return;
		}
		int num = (int)data;
		if (num < 0 || IsUnConnect() || !isTriggerSelectChange)
		{
			return;
		}
		base.SelectedChangedCmdExecute(data);
		if (IsNotifyRefreshParam)
		{
			isTriggerGetParamUnit = false;
			if (RemoteOperate.IsRemoteMode)
			{
				(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit(isGetFromModuleInRemoteMode: true);
			}
			else
			{
				(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit();
			}
			isTriggerGetParamUnit = true;
		}
	}
}
