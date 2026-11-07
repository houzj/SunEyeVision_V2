using System;
using System.Text;
using System.Xml;
using Apps.Data;
using Hik.Xml;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class FloatBx : Float, IDynamicCameraProperty
{
	public override string DisplayType => "Float";

	public override bool NeedInit => true;

	public bool IsTriggerChanged { get; set; } = false;

	public bool IsNotifyRefreshParam { get; set; } = false;

	protected override void ValueChangedCmdExecute()
	{
		if (IsTriggerChanged || IsUnConnect() || !(OldValue != Value) || SetParamUnit() < 0)
		{
			return;
		}
		OldValue = Value;
		TriggerCurValueBusiness();
		if (!RemoteOperate.IsRemoteMode)
		{
			GetParamUnit();
		}
		PreExecuteRun();
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
				ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, Name, ref strValue);
			}
			stringBuilder.Append(strValue);
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			return -1;
		}
		string[] array = stringBuilder.ToString().Split('$');
		if (array.Length >= 4)
		{
			try
			{
				if (array[0] == "NULL")
				{
					array[0] = "0";
				}
				MaxValueA = Convert.ToDouble(array[2]);
				MinValueA = Convert.ToDouble(array[1]);
				double num = Convert.ToDouble(array[0]);
				if (CurValueA != num)
				{
					CurValueA = num;
					OldValue = CurValueA.ToString();
					TriggerCurValueBusiness();
				}
				try
				{
					IncValueA = Convert.ToDouble(array[3]);
				}
				catch (Exception)
				{
					IncValueA = 0.1;
				}
			}
			catch (Exception)
			{
			}
		}
		return 0;
	}

	public void RefreshRange()
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				StringBuilder stringBuilder = new StringBuilder();
				string strValue = string.Empty;
				try
				{
					ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, Name, ref strValue);
					stringBuilder.Append(strValue);
				}
				catch
				{
					return;
				}
				string[] array = stringBuilder.ToString().Split('$');
				MaxValueA = Convert.ToDouble(array[2]);
				MinValueA = Convert.ToDouble(array[1]);
			}
		}
		catch
		{
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		SetTriggerDisable("CurValueA");
		return this;
	}

	private void TriggerCurValueBusiness()
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
