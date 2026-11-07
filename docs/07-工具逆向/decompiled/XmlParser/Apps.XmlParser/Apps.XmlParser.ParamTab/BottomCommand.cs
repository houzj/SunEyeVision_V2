using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using DirectoryBrowser;
using HikPcUI.Commom;
using Microsoft.Win32;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class BottomCommand : Command
{
	public bool NeedPopTip = false;

	public string ToolTipMessage = string.Empty;

	private bool cancel = false;

	private Task continuousSelfExecuteTask = null;

	private bool isModuleCallBackArrived = true;

	private object _lock = new object();

	protected virtual bool IsBusy => ((IVMRun)VmSolution.Instance).IsRunning;

	public void CloseParamsTabControl()
	{
		SystemCommands.CloseWindowCommand.Execute(null, null);
	}

	private void StartContinuousSelfExecute()
	{
		cancel = false;
		if (continuousSelfExecuteTask != null)
		{
			return;
		}
		CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
		{
		});
		continuousSelfExecuteTask = new Task(() =>
		{
			while (!cancel)
			{
				ContinuousSelfExcute();
				Thread.Sleep(300);
			}
		}, TaskCreationOptions.LongRunning);
		LogHelper.objLog.Info("ContinuousSelfExecute task is start");
		continuousSelfExecuteTask.Start();
	}

	public void StopContinuousSelfExecute()
	{
		LogHelper.Debug("StopContinuousSelfExecute");
		cancel = true;
		if (continuousSelfExecuteTask != null)
		{
			continuousSelfExecuteTask.Wait();
			continuousSelfExecuteTask = null;
		}
		LogHelper.Debug("ContinuousSelfExecute task is cancel");
		if ((GetTopBaseNode() as AlgorithmTabRoot)?.Owner is IVmModule vmModule)
		{
			vmModule.ModuleResultCallBackArrived -= OnModuleCallBackArrived;
		}
		else
		{
			LogHelper.Debug("module is null");
		}
		lock (_lock)
		{
			isModuleCallBackArrived = true;
		}
	}

	public void SelfExcuteModule()
	{
		if (IsBusy)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		try
		{
			if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule { IsReady: var isReady } owner }))
			{
				return;
			}
			if ((!isReady) ?? false)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Param not Complete"), MessageIcons.Error);
			}
			else
			{
				if (((IVmSolution)VmSolution.Instance).IsLoading)
				{
					return;
				}
				IList<object> list = ((owner["[Display]"] is ParamRoot paramRoot) ? paramRoot.GetDisplayList() : null);
				if (list != null)
				{
					foreach (object item in list)
					{
						(item as Object)?.ClearValueStore();
					}
				}
				owner.Run();
			}
		}
		catch (VmException ex)
		{
			string errorInfo = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(errorInfo, MessageIcons.Error);
		}
	}

	public void ContinuousSelfExcute()
	{
		VmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot)?.Owner as VmModule;
		if (!isModuleCallBackArrived || (vmModule != null && vmModule.Root?.IsRunning == true))
		{
			Thread.Sleep(300);
		}
		else if (((IVmSolution)VmSolution.Instance).IsContinuousSelfExecute)
		{
			if (vmModule != null)
			{
				try
				{
					lock (_lock)
					{
						isModuleCallBackArrived = false;
					}
					if (((IVmSolution)VmSolution.Instance).IsLoading)
					{
						((IVmSolution)VmSolution.Instance).IsContinuousSelfExecute = false;
					}
					else
					{
						IList<object> list = ((vmModule["[Display]"] is ParamRoot paramRoot) ? paramRoot.GetDisplayList() : null);
						if (list != null)
						{
							foreach (object item in list)
							{
								(item as Object)?.ClearValueStore();
							}
						}
						vmModule.Run();
					}
					return;
				}
				catch (VmException ex)
				{
					((IVmSolution)VmSolution.Instance).IsContinuousSelfExecute = false;
					string msg = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
					CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
					{
						HikMessageBox.Show(msg, MessageIcons.Error);
					});
					Task.Run(() =>
					{
						StopContinuousSelfExecute();
					});
					return;
				}
			}
			LogHelper.objLog.Error("ContinuousSelfExecute CurrentModuleInfo is null");
		}
		else
		{
			Task.Run(() =>
			{
				StopContinuousSelfExecute();
			});
		}
	}

	private bool NeedExecuteProcess(VmProcedure procedure)
	{
		IVmProcedure vmProcedure;
		if ((vmProcedure = procedure) != null && (procedure.ExecuteCount == 0 || !vmProcedure.CanModuleSelfExecute))
		{
			return true;
		}
		return false;
	}

	public void ContinuousSelfExcuteModule()
	{
		if (IsBusy)
		{
			((IVmSolution)VmSolution.Instance).IsContinuousSelfExecute = false;
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
		}
		else if ((GetTopBaseNode() as AlgorithmTabRoot)?.Owner is IVmModule { IsReady: var isReady } vmModule)
		{
			if ((!isReady) ?? false)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Param not Complete"), MessageIcons.Error);
				return;
			}
			((IVmSolution)VmSolution.Instance).IsContinuousSelfExecute = true;
			vmModule.ModuleResultCallBackArrived -= OnModuleCallBackArrived;
			vmModule.ModuleResultCallBackArrived += OnModuleCallBackArrived;
			StartContinuousSelfExecute();
		}
		else
		{
			LogHelper.objLog.Error("ContinuousSelfExecute CurrentModuleInfo is null");
		}
	}

	public void OnModuleCallBackArrived(object sender, EventArgs e)
	{
		lock (_lock)
		{
			isModuleCallBackArrived = true;
		}
	}

	public void StopSelfExcuteModule()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
		{
			VmModule vmModule = algorithmTabRoot.Owner as VmModule;
			((IVmSolution)VmSolution.Instance).IsContinuousSelfExecute = false;
		}
	}

	private IBottomCommand GetFormat()
	{
		IBottomCommand result = null;
		try
		{
			if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				result = algorithmTabRoot.FindBaseNodeByName("StringInput") as IBottomCommand;
			}
		}
		catch
		{
		}
		return result;
	}

	public void TestExpression_FormatModule()
	{
		GetFormat()?.ExecuteFunction("OKCommand");
	}

	public void Save_FormatModule()
	{
		IBottomCommand format = GetFormat();
		if (format == null || format.ExecuteFunction("OKCommand").ToString() == "False")
		{
			return;
		}
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		VmModule vmModule = null;
		if (algorithmTabRoot != null)
		{
			vmModule = algorithmTabRoot.Owner as VmModule;
			if (vmModule != null)
			{
				vmModule.NeedConfirm = false;
			}
		}
		CloseParamsTabControl();
		if (vmModule != null)
		{
			vmModule.NeedConfirm = true;
		}
	}

	public void Cancel_FormatModule()
	{
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		VmModule vmModule = null;
		if (algorithmTabRoot != null)
		{
			vmModule = algorithmTabRoot.Owner as VmModule;
			if (vmModule != null)
			{
				vmModule.NeedConfirm = false;
			}
		}
		CloseParamsTabControl();
		if (vmModule != null)
		{
			vmModule.NeedConfirm = true;
		}
	}

	private IBottomCommand GetCalculator()
	{
		IBottomCommand result = null;
		try
		{
			if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				result = algorithmTabRoot.FindBaseNodeByName("CalculatorPro") as IBottomCommand;
			}
		}
		catch
		{
		}
		return result;
	}

	public void Rest_CalculateModule()
	{
		GetCalculator()?.ExecuteFunction();
	}

	public void Execute_CalculateModule()
	{
		GetCalculator()?.ExecuteFunction("RunOnceCommand");
	}

	public void Close_CalculateModule()
	{
		IBottomCommand calculator = GetCalculator();
		if (calculator != null && !(calculator.ExecuteFunction("OKCommand").ToString() == "False"))
		{
			CloseParamsTabControl();
		}
	}

	private void SetDicByFilter(Dictionary<Filter, IndexFilter> dic, Filter filter, Filter outputFilter)
	{
		if (dic.Keys.Contains(filter))
		{
			dic[filter] = new IndexFilter(filter.RelativeIndexStringName, outputFilter);
		}
	}

	public void SaveCalibPath()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		foreach (Tab tab in algorithmTabRoot.Tabs)
		{
			object obj = tab.FindBaseNodeByName("SaveCalibPath");
			if (obj != null && obj is SaveFileDialog)
			{
				(obj as SaveFileDialog).DoSaveFile();
				break;
			}
		}
	}

	public void RealTimeImgAcq()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		foreach (Tab tab in algorithmTabRoot.Tabs)
		{
			object obj = tab.FindBaseNodeByName("RealTimeImgAcq");
		}
	}

	public new void Clear()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner }))
		{
			return;
		}
		try
		{
			owner.Params["Clear"] = "1";
			if (NeedPopTip)
			{
				HikMessageBox.Show("Lang_InfoTips", "Lang_ClearImageSuc", MessageButtons.Normal, MessageIcons.Ok, isTranslate: true);
			}
		}
		catch
		{
			if (NeedPopTip)
			{
				HikMessageBox.Show("Lang_Err", "Lang_ClearImageErr", MessageButtons.Normal, MessageIcons.Error, isTranslate: true);
			}
		}
	}

	public void ClearCalibPoint()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner }))
		{
			return;
		}
		try
		{
			owner.Params["Clear"] = "0";
			if (NeedPopTip)
			{
				HikMessageBox.Show("Lang_InfoTips", "Lang_ClearCalibPointSuc", MessageButtons.Normal, MessageIcons.Ok, isTranslate: true);
			}
		}
		catch
		{
			if (NeedPopTip)
			{
				HikMessageBox.Show("Lang_Err", "Lang_ClearCalibPointErr", MessageButtons.Normal, MessageIcons.Error, isTranslate: true);
			}
		}
	}

	public void SaveFilePath()
	{
		DoSaveTextFile();
	}

	public void DoSaveTextFile()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner }))
		{
			return;
		}
		string empty = string.Empty;
		try
		{
			empty = owner.Params["OutputType"];
		}
		catch
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), MessageIcons.Error);
			return;
		}
		if (empty != "0" && empty != "1")
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), MessageIcons.Error);
			return;
		}
		try
		{
			if (RemoteOperate.IsRemoteMode)
			{
				string text = ConfigurationHelper.INIGetDefaultOpenRemotePath(((IVmModule)owner)?.ModuleDir);
				DirectoryBrowser.SaveFileDialog saveFileDialog = new DirectoryBrowser.SaveFileDialog
				{
					Filter = ((empty == "0") ? "txt|*.txt" : "csv|*.csv")
				};
				if (text != null)
				{
					saveFileDialog.InitialDirectory = text;
				}
				if (saveFileDialog.ShowDialog() == true)
				{
					string filePath = saveFileDialog.FilePath;
					try
					{
						owner.Params["SaveTempFile"] = filePath;
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveSuc"), MessageIcons.Ok);
					}
					catch
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), MessageIcons.Error);
						return;
					}
					string directoryName = saveFileDialog.GetDirectoryName();
					ConfigurationHelper.INISetDefaultOpenRemotePath(directoryName, ((IVmModule)owner)?.ModuleDir);
				}
				return;
			}
			string text2 = ConfigurationHelper.INIGetDefaultOpenPath(((IVmModule)owner)?.ModuleDir);
			Microsoft.Win32.SaveFileDialog saveFileDialog2 = new Microsoft.Win32.SaveFileDialog
			{
				Filter = ((empty == "0") ? "txt|*.txt" : "csv|*.csv"),
				FilterIndex = 1,
				RestoreDirectory = true
			};
			if (text2 != null)
			{
				saveFileDialog2.InitialDirectory = text2;
			}
			if (saveFileDialog2.ShowDialog() == true)
			{
				string fileName = saveFileDialog2.FileName;
				try
				{
					owner.Params["SaveTempFile"] = fileName;
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveSuc"), MessageIcons.Ok);
				}
				catch
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), MessageIcons.Error);
					return;
				}
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(fileName), ((IVmModule)owner)?.ModuleDir);
			}
		}
		catch (VmException ex)
		{
			string[] strArrMsg = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ErrorCodeInfo.GetErrorInfo(ex.errorCode)
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), strArrMsg, MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			string[] strArrMsg2 = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ex2.Message
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), strArrMsg2, MessageIcons.Error);
		}
	}

	public void CompileShellCode()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		foreach (Tab tab in algorithmTabRoot.Tabs)
		{
			object obj = tab.FindBaseNodeByName("ShellContent");
			if (obj != null)
			{
				bool flag = (bool)(obj as IBottomCommand).ExecuteFunction();
				break;
			}
		}
	}

	public void SaveShellCode()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		bool flag = false;
		foreach (Tab tab in algorithmTabRoot.Tabs)
		{
			if (tab.FindBaseNodeByName("ShellContent") is IBottomCommand bottomCommand)
			{
				flag = (bool)bottomCommand.ExecuteFunction(true);
				break;
			}
		}
		if ((algorithmTabRoot.Owner != null) & flag)
		{
			CloseParamsTabControl();
		}
	}

	public void ClearStitchImageBuffer()
	{
		if (((IVMRun)VmSolution.Instance).IsRunning)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
		}
		else if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
		{
			IVmModule vmModule = algorithmTabRoot.Owner as IVmModule;
			try
			{
				((IUserStringData)vmModule.Params)["ClearImageBuffer"] = "";
				HikMessageBox.Show("Lang_ImageIsEmpty", MessageIcons.Ok, isTranslate: true);
			}
			catch (VmException ex)
			{
				string errorInfo = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				HikMessageBox.Show(errorInfo, MessageIcons.Error);
			}
		}
	}

	public void SetCommand()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner }))
		{
			return;
		}
		try
		{
			owner.Params[Name] = CurValueA;
			if (NeedPopTip)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang(ToolTipMessage), MessageIcons.Ok);
			}
		}
		catch (VmException ex)
		{
			MyItemState = ItemState.SetParamErro;
			string errorInfo = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			string lang = AppLocalizationService.GetLang(errorInfo);
			HikMessageBox.Show(lang, MessageIcons.Error);
			LogHelper.Error("设置参数：" + ComName + "失败：" + lang);
		}
		catch (Exception ex2)
		{
			MyItemState = ItemState.SetParamErro;
			int nErrorCode = 0;
			try
			{
				nErrorCode = Convert.ToInt32(ex2.Message, 16);
			}
			catch
			{
			}
			string errorInfo2 = ErrorCodeInfo.GetErrorInfo(nErrorCode);
			string lang2 = AppLocalizationService.GetLang(errorInfo2);
			HikMessageBox.Show(lang2, MessageIcons.Error);
			LogHelper.Error("设置参数：" + ComName + "失败：" + ex2.Message + lang2);
		}
	}

	public void InitFrameTrack()
	{
		if (((IVMRun)VmSolution.Instance).IsRunning)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
		}
		else if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
		{
			IVmModule vmModule = algorithmTabRoot.Owner as IVmModule;
			try
			{
				((IUserStringData)vmModule.Params)["InitFrameTrack"] = "";
				HikMessageBox.Show("InitSuccess", MessageIcons.Ok, isTranslate: true);
			}
			catch (VmException ex)
			{
				string errorInfo = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				HikMessageBox.Show(errorInfo, MessageIcons.Error);
			}
		}
	}
}
