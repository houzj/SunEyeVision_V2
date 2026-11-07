using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using HikPcUI.Commom;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class EnumerationHikLx : NameValueItem
{
	private ObservableCollection<object> EnumEntrys;

	public string CurValue = "0";

	private int DefaultValue;

	private int EnumCurValue;

	private bool isTriggerCameraSelectChange = true;

	public new bool IsGetShowError = true;

	private DelegateCommand<object> _SelectedChangedCommand = null;

	private DelegateCommand _ClickCommand = null;

	protected bool CanClick = true;

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
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public override string DisplayType
	{
		get
		{
			return "EnumerationLx";
		}
		set
		{
		}
	}

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public int EnumCurValueA
	{
		get
		{
			return EnumCurValue;
		}
		set
		{
			EnumCurValue = value;
			OnPropertyChanged("EnumCurValueA");
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

	public new string AlgorithmIndexA
	{
		get
		{
			return AlgorithmIndex;
		}
		set
		{
			AlgorithmIndex = value;
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
			CurValueA = value;
		}
	}

	public override bool NeedInit => true;

	public int LangEnumerWidth
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

	public ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand<object>((Action<object>)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
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

	public override BaseNode Init(XmlNode node)
	{
		IsGetShowError = false;
		base.Init(node);
		return this;
	}

	private void SelectedChangedCmdExecute(object value)
	{
		if (IsUnConnect() || !isTriggerCameraSelectChange || (int)value < 0 || (int)value >= EnumEntrysA.Count || !((EnumEntrysA[(int)value] as EnumEntryLx).StringValueA != CurValueA))
		{
			return;
		}
		string curValueA = CurValueA;
		int enumCurValueA = EnumCurValueA;
		CurValueA = (EnumEntrysA[(int)value] as EnumEntryLx).StringValueA;
		Task.Run(() =>
		{
			VmModule vmModule = GetVmModule();
			int num = ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData(vmModule.ID, "AcquisitionName", StringUtil.UTF8GetBytesPadZero(CurValueA), 60000u);
			if (num != 0)
			{
				CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CamLinkFail"), AppLocalizationService.GetLang("Lang_CamUsedorNetErr"), MessageIcons.Error);
				});
			}
			(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit();
		});
	}

	private void ClickCommandCmdExecute()
	{
		if (CanClick)
		{
			if (MyItemState != ItemState.Normal)
			{
				return;
			}
			Task.Run(() =>
			{
				CanClick = false;
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					int num = ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData(vmModule.ID, "EnumAcquisition", StringUtil.UTF8GetBytesPadZero("1"), 60000u);
					if (num != 0)
					{
						CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
						{
							HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RefreshAcqFail"), MessageIcons.Error);
						});
						LogHelper.Error("刷新采集卡失败 ," + num);
					}
				}
				(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit();
				CanClick = true;
			});
		}
		else
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_AcquisitionRefreshing"), MessageIcons.Warning);
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
		StringBuilder strEnum = new StringBuilder();
		string empty = string.Empty;
		byte[] data = null;
		try
		{
			int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(vmModule.ID, Name, ref data, 60000u);
			if (binaryData != 0)
			{
				VmException ex = new VmException(binaryData);
				LogHelper.Error("获取AcquisitionName失败：" + ex.errorCode + " " + ex.errorMessage);
				throw ex;
			}
			empty = StringUtil.UTF8GetString(data);
			strEnum.Append(empty);
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			return -1;
		}
		string[] strInfoList = strEnum.ToString().Split('$');
		if (strInfoList.Length >= 6)
		{
			try
			{
				int nCurrentIndex = Convert.ToInt32(strInfoList[0]);
				if (nCurrentIndex < 0)
				{
					return 0;
				}
				CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
				{
					isTriggerCameraSelectChange = false;
					if (EnumEntrysA == null)
					{
						EnumEntrysA = new ObservableCollection<object>();
					}
					CurValueA = "0";
					EnumCurValueA = -1;
					EnumEntrysA.Clear();
					string text = strEnum.ToString().Substring(strInfoList[0].Length + 1 + strInfoList[1].Length + 1 + strInfoList[2].Length + 1 + strInfoList[3].Length + 1 + strInfoList[4].Length + 1);
					string[] array = text.Split('\r');
					for (int i = 0; i < array.Length; i++)
					{
						int num = array[i].LastIndexOf("(");
						int num2 = array[i].LastIndexOf(")");
						if (i == 0)
						{
							EnumEntryLx item = new EnumEntryLx
							{
								DisplayNameA = array[i],
								ValueA = i,
								StringValueA = "0"
							};
							EnumEntrysA.Add(item);
						}
						else if (num != -1 && num2 != -1)
						{
							EnumEntryLx item2 = new EnumEntryLx
							{
								DisplayNameA = array[i],
								ValueA = i,
								StringValueA = array[i].Substring(num + 1, num2 - num - 1)
							};
							EnumEntrysA.Add(item2);
						}
					}
					if (CurValueA != strInfoList[1])
					{
						CurValueA = strInfoList[1];
					}
					if (EnumCurValueA != nCurrentIndex && nCurrentIndex < EnumEntrysA.Count)
					{
						EnumCurValueA = nCurrentIndex;
					}
				});
			}
			catch (Exception)
			{
			}
			finally
			{
				isTriggerCameraSelectChange = true;
			}
		}
		return 0;
	}
}
