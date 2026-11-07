using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VisionMaster.Project;

namespace VisionMaster.AppMainWindow;

public class SolutionItemsData : BaseNode, INotifyPropertyChanged
{
	private DispatcherTimer saveTime = new DispatcherTimer();

	public ObservableCollection<SolItem> _solItems = new ObservableCollection<SolItem>();

	public bool _saveSolTimerEnable;

	public bool _saveAllSolTimerEnable;

	public double _AutoSaveTime = 3.0;

	private DelegateCommand _addCommand = null;

	private DelegateCommand _setResultBackCommand = null;

	public ObservableCollection<SolItem> SolItems
	{
		get
		{
			return _solItems;
		}
		set
		{
			_solItems = value;
			OnPropertyChanged("SolItems");
		}
	}

	public bool SaveSolTimerEnable
	{
		get
		{
			return _saveSolTimerEnable;
		}
		set
		{
			if (_saveSolTimerEnable != value)
			{
				if (SetAutoSaveSolState(ref value))
				{
					_saveSolTimerEnable = value;
				}
				OnPropertyChanged("SaveSolTimerEnable");
			}
		}
	}

	public bool SaveAllSolTimerEnable
	{
		get
		{
			return _saveAllSolTimerEnable;
		}
		set
		{
			if (_saveAllSolTimerEnable != value)
			{
				_saveAllSolTimerEnable = value;
				if (value)
				{
					AddTimer(saveTime);
				}
				else
				{
					DisposeTimer(saveTime);
				}
				OnPropertyChanged("SaveAllSolTimerEnable");
			}
		}
	}

	public double AutoSaveTime
	{
		get
		{
			return _AutoSaveTime;
		}
		set
		{
			if (_AutoSaveTime != value)
			{
				_AutoSaveTime = value;
				int minutes = (int)_AutoSaveTime;
				saveTime.Interval = new TimeSpan(0, minutes, 0);
				OnPropertyChanged("AutoSaveTime");
			}
		}
	}

	public ICommand AddCommand
	{
		get
		{
			if (_addCommand == null)
			{
				_addCommand = new DelegateCommand(AddItemCmdExecute);
			}
			return _addCommand;
		}
	}

	public ICommand SetResultBackCommand
	{
		get
		{
			if (_setResultBackCommand == null)
			{
				_setResultBackCommand = new DelegateCommand(SetResultCallBackCmdExecute);
			}
			return _setResultBackCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	~SolutionItemsData()
	{
		DisposeTimer(saveTime);
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private bool GetAutoSaveSolState()
	{
		try
		{
			uint autoSaveSolState = ServerSDKManager.serverSDKManager.mSolutionManager.GetAutoSaveSolState();
			return autoSaveSolState == 1;
		}
		catch (VmException ex)
		{
			LogHelper.Error("GetAutoSaveSolState" + ex.errorMessage);
			return false;
		}
	}

	private bool SetAutoSaveSolState(ref bool flag)
	{
		try
		{
			if (!flag)
			{
				if (HikMessageBox.Show(AppLocalizationService.GetLang("SetAutoSaveSolStateTips"), MessageButtons.YesOrNo, MessageIcons.Question) == DialogResult.Yes)
				{
					flag = false;
				}
				else
				{
					flag = true;
				}
			}
			ServerSDKManager.serverSDKManager.mSolutionManager.SetAutoSaveSolState(flag ? 1u : 0u);
			return true;
		}
		catch (VmException ex)
		{
			HikMessageBox.Show("SetAutoSaveSolState" + ex.errorMessage);
			LogHelper.Error("SetAutoSaveSolState" + ex.errorMessage);
			return false;
		}
	}

	public void AddTimer(DispatcherTimer timer)
	{
		int minutes = (int)AutoSaveTime;
		timer.Interval = new TimeSpan(0, 0, minutes, 0);
		timer.Tick -= SaveTimer_Tick;
		timer.Tick += SaveTimer_Tick;
		timer.Start();
	}

	public void DisposeTimer(DispatcherTimer timer)
	{
		timer.Stop();
		timer.Tick -= SaveTimer_Tick;
		timer = null;
	}

	private void SaveTimer_Tick(object sender, EventArgs e)
	{
		if (VmSolution.Instance.IsSolModifyInfo() && !VmSolution.Instance.IsRunning && !VmSolution.Instance.ContinuousRunEnable && !CreatModel.Instance.IsCreatingModel)
		{
			ProjectManager.GetInstance().SaveSolution();
		}
	}

	private void AddItemCmdExecute()
	{
		if (SolItems.Count < 1000)
		{
			SolItem solItem = new SolItem();
			solItem.DeleteEventHander += Item_DeleteEventHander;
			SolItems.Add(solItem);
			RefreshIndex();
		}
		else
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolManagerMaxNum"), MessageIcons.Error);
		}
	}

	private void Item_DeleteEventHander(SolItem obj)
	{
		SolItems.Remove(obj);
		RefreshIndex();
	}

	private void RefreshIndex()
	{
		foreach (SolItem solItem in SolItems)
		{
			solItem.Index = (SolItems.IndexOf(solItem) + 1).ToString();
		}
	}

	private void SetResultCallBackCmdExecute()
	{
		int nModuleId = 30000;
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.EnableCallBackModuResult((uint)nModuleId);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_callbackok"), MessageIcons.Ok);
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_callbackerror") + ex.errorMessage);
		}
	}

	public void LoadSolInfo()
	{
		try
		{
			string config = ConfigurationHelper.GetConfig("SolutionCommInfo");
			if (!string.IsNullOrEmpty(config))
			{
				byte[] data = Convert.FromBase64String(config);
				loadXml(data);
				foreach (SolItem solItem in SolItems)
				{
					solItem.DeleteEventHander += Item_DeleteEventHander;
				}
			}
			if (SaveAllSolTimerEnable && !saveTime.IsEnabled)
			{
				AddTimer(saveTime);
			}
			_saveSolTimerEnable = GetAutoSaveSolState();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CfgErr"), ex.Message, MessageIcons.Warning);
		}
	}

	public void SaveSolInfo()
	{
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(ToXmlStringWithHead(IsShort: true));
			string defaultValue = Convert.ToBase64String(bytes);
			ConfigurationHelper.SaveConfig("SolutionCommInfo", defaultValue);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CfgSaveErr"), ex.Message, MessageIcons.Warning);
		}
	}

	public SolItem GetSolItem(string command)
	{
		return SolItems.FirstOrDefault((SolItem x) => x.CommString == command && x.CommEnable);
	}
}
