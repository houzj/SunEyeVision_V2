using System;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using Apps.Data;
using Apps.Localization;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class IntegerBx : Integer, IDynamicCameraProperty
{
	public bool IsRemindBox = false;

	private DelegateCommand _ValueChangedCommand = null;

	private DispatcherTimer m_Timer = new DispatcherTimer();

	public override string DisplayType => "Integer";

	public bool IsRemindBoxA
	{
		get
		{
			return IsRemindBox;
		}
		set
		{
			if (IsRemindBox != value)
			{
				IsRemindBox = value;
			}
		}
	}

	public override bool NeedInit => true;

	public new ICommand ValueChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public bool IsTriggerChanged { get; set; } = false;

	public bool IsNotifyRefreshParam { get; set; } = false;

	private new void ValueChangedCmdExecute()
	{
		if (IsTriggerChanged || IsUnConnect())
		{
			return;
		}
		if (OldValue != Value && SetParamUnit() >= 0)
		{
			OldValue = Value;
			TriggerCurValueBusiness();
			PreExecuteRun();
			if (IsRemindBoxA)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Modify Parameter"), AppLocalizationService.GetLang("Lang_Remind Save UserSet"), MessageIcons.Warning);
			}
		}
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

	private void RefreshParam(object sender, EventArgs e)
	{
		m_Timer.Stop();
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
				MaxValueA = Convert.ToInt32(array[2]);
				MinValueA = Convert.ToInt32(array[1]);
				int num = Convert.ToInt32(array[0]);
				if (CurValueA != num)
				{
					OldValue = num.ToString();
					CurValueA = num;
					TriggerCurValueBusiness();
				}
				try
				{
					IncValueA = Convert.ToInt32(array[3]);
				}
				catch (Exception)
				{
					IncValueA = 1;
				}
			}
			catch (Exception)
			{
			}
		}
		return 0;
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
