using System;
using Apps.Data;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class BooleanBx : Boolean, IDynamicCameraProperty
{
	public override string DisplayType => "Boolean";

	public override bool NeedInit => true;

	public bool IsTriggerChanged { get; set; } = false;

	public bool IsNotifyRefreshParam { get; set; } = false;

	protected override void ValueChangedCmdExecute()
	{
		if (IsTriggerChanged)
		{
			return;
		}
		base.ValueChangedCmdExecute();
		if (IsNotifyRefreshParam)
		{
			if (RemoteOperate.IsRemoteMode)
			{
				(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit(isGetFromModuleInRemoteMode: true);
			}
			else
			{
				(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit();
			}
		}
	}

	public override int GetParamUnit()
	{
		MyItemState = ItemState.Normal;
		int num = -1;
		VmModule vmModule = GetVmModule();
		string strValue = string.Empty;
		if (vmModule != null)
		{
			try
			{
				ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, Name, ref strValue);
			}
			catch
			{
				MyItemState = ItemState.GetParamErro;
				return -1;
			}
		}
		string[] array = strValue.Split('$');
		if (array[0].Length >= 1)
		{
			if (array[0] == "NULL")
			{
				array[0] = "False";
			}
			bool flag = Convert.ToBoolean(array[0]);
			if (CurValueA != flag)
			{
				OldValue = flag.ToString();
				CurValueA = flag;
			}
		}
		return 0;
	}

	public override int SetParamUnit()
	{
		if (MyItemState == ItemState.Normal)
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				try
				{
					ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(vmModule.ID, Name, Value, bThrowException: true);
				}
				catch (VmException ex)
				{
					LogHelper.Error("设置失败：" + Name + " " + ex.errorCode + " " + ex.errorMessage);
					MyItemState = ItemState.SetParamErro;
					return -1;
				}
			}
			return 0;
		}
		return -1;
	}
}
