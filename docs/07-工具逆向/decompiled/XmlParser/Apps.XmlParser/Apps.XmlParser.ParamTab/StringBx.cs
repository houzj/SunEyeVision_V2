using System;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class StringBx : String, IDynamicCameraProperty
{
	public bool _IsReadOnly = true;

	private DelegateCommand _ClickCommand = null;

	public override bool NeedInit => true;

	public int LangStringBxWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return 170;
			}
			return 89;
		}
	}

	public bool IsReadOnlyA
	{
		get
		{
			return _IsReadOnly;
		}
		set
		{
			if (_IsReadOnly != value)
			{
				_IsReadOnly = value;
				OnPropertyChanged("IsReadOnlyA");
			}
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

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

	private void ClickCommandCmdExecute()
	{
		if (!IsUnConnect())
		{
			GetParamUnit();
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
		string[] array = stringBuilder.ToString().Split('$');
		if (array.Length >= 1)
		{
			try
			{
				if (array[0] == "NULL")
				{
					array[0] = "0";
				}
				string text = array[0];
				if (CurValueA != text)
				{
					OldValue = text;
					CurValueA = text;
				}
			}
			catch (Exception)
			{
			}
		}
		return 0;
	}
}
