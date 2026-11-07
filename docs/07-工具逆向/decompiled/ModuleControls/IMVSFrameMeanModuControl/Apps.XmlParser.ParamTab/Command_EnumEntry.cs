using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using VM.Controls;
using VM.Core;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class Command_EnumEntry : Command
{
	public bool IsSelected;

	public bool IsEnable = true;

	public string DisplayTitle;

	public new int Value;

	public ObservableCollection<object> EnumEntrys;

	private DelegateCommand _ClickCommand;

	private DelegateCommand _CheckedCommand;

	public bool IsEnableA
	{
		get
		{
			return IsEnable;
		}
		set
		{
			IsEnable = value;
			OnPropertyChanged("IsEnableA");
		}
	}

	public bool IsSelectedA
	{
		get
		{
			return IsSelected;
		}
		set
		{
			IsSelected = value;
			OnPropertyChanged("IsSelectedA");
		}
	}

	public string DisplayTitleA
	{
		get
		{
			return DisplayTitle;
		}
		set
		{
			DisplayTitle = value;
		}
	}

	public int ValueA
	{
		get
		{
			return Value;
		}
		set
		{
			Value = value;
		}
	}

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
			}
		}
	}

	public override ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand(ClickCommandCmdExecute);
			}
			return _ClickCommand;
		}
	}

	public ICommand CheckedCommand
	{
		get
		{
			if (_CheckedCommand == null)
			{
				_CheckedCommand = new DelegateCommand(CheckedCommandExecute);
			}
			return _CheckedCommand;
		}
	}

	public override string ToString()
	{
		return DisplayName;
	}

	private void ClickCommandCmdExecute()
	{
		if (CommandSetter != null)
		{
			if (CommandSetter.Operation == null)
			{
				CommandSetter.CreatOperateInstance();
			}
			if (CommandSetter.Operation != null)
			{
				CommandSetter.Operate(null);
			}
		}
	}

	private void CheckedCommandExecute()
	{
		if (GetProcedure() is VmProcedure { ContinuousRunEnable: false } && !((IVMRun)VmSolution.Instance).ContinuousRunEnable && GetParentNode() is CommandSelecter commandSelecter)
		{
			commandSelecter.Value = Value.ToString();
			commandSelecter.SelectedChangedCmdExecute();
		}
	}

	public void StartRunOnce()
	{
		if (!(GetParentNode() is CommandSelecter_Frame commandSelecter_Frame) || !(GetProcedure() is VmProcedure vmProcedure))
		{
			return;
		}
		if (vmProcedure.ContinuousRunEnable || ((IVMRun)VmSolution.Instance).ContinuousRunEnable)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("当前流程处于忙碌状态!"), MessageIcons.Error);
			return;
		}
		commandSelecter_Frame.GetParamUnit();
		if (commandSelecter_Frame.ImgCountA >= 100 && commandSelecter_Frame.CurValueA == 1)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("统计图像上限") + " :100", MessageIcons.Error);
			return;
		}
		vmProcedure.Run();
		if (ValueA == 1)
		{
			commandSelecter_Frame.GetMeanResultFlag();
		}
	}

	public void ClearFrameResult()
	{
		if (GetProcedure() is VmProcedure vmProcedure)
		{
			if (vmProcedure.ContinuousRunEnable || ((IVMRun)VmSolution.Instance).ContinuousRunEnable)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("当前流程处于忙碌状态!"), MessageIcons.Error);
			}
			else if (GetParentNode() is CommandSelecter_Frame commandSelecter_Frame)
			{
				commandSelecter_Frame.ImgCountA = 0;
				GetVmModule().Run();
				commandSelecter_Frame.GetParamUnit();
			}
		}
	}

	private object GetProcedure()
	{
		VmModule vmModule = GetVmModule().Parent as VmModule;
		if (vmModule == null)
		{
			return null;
		}
		while (!(vmModule is VmProcedure))
		{
			vmModule = vmModule.Parent as VmModule;
			if (vmModule == null)
			{
				return null;
			}
		}
		return vmModule;
	}
}
