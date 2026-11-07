using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.PcUI;
using Apps.UIHelper.Extern;
using Apps.XmlUI;
using DirectoryBrowser;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;
using VMControls.WPF.Base;
using VMControls.WPF.DataForUI;
using VMControls.WPF.GlobalUI;
using VisionMaster.AppMainWindow;
using starshipxac.Shell;
using starshipxac.Windows.Shell.Dialogs;
using starshipxac.Windows.Shell.Dialogs.Controls;

namespace VisionMaster.Project;

public class ProjectManager : NotifyPropertyChangedBase
{
	private bool _isCreating = false;

	private static readonly object syncObj = new object();

	private static ProjectManager instance = null;

	private DelegateCommand<object> openSolutionCommand = null;

	public string _strCurrentSolutionFileName = "";

	private string tempStrPassWord;

	private static List<SolutionItemInfo> _recentlySolutions;

	internal static SolutionItemInfo OpenRecentSolutionInfo;

	public List<SolutionItemInfo> RecentlySolutions => GetRecentlySolutions();

	public bool NeedShowSaveNotice
	{
		get
		{
			bool result = false;
			try
			{
				result = VmSolution.Instance.IsSolModifyInfo();
			}
			catch (VmException ex)
			{
				string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + "\r\n";
				text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_JudgeSolModifyError"), text, MessageIcons.Error);
			}
			return result;
		}
	}

	public static SolutionItemInfo RecentSolution { get; private set; }

	public bool IsSingleRunEnable
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public bool IsCreating
	{
		get
		{
			return _isCreating;
		}
		set
		{
			if (_isCreating != value)
			{
				_isCreating = value;
			}
		}
	}

	public ICommand OpenSolutionCommand => (ICommand)(openSolutionCommand ?? (openSolutionCommand = new DelegateCommand<object>((Action<object>)OpenSolutionExecute)));

	public string strCurrentSolutionFileName
	{
		get
		{
			return _strCurrentSolutionFileName;
		}
		set
		{
			_strCurrentSolutionFileName = value;
			OnPropertyChanged("strCurrentSolutionFileName");
		}
	}

	public event EventHandler OnPreSaveProject;

	public event EventHandler OnPreLoadProject;

	public event EventHandler OnLoadSucceed;

	public event EventHandler OnLoadFailed;

	private ProjectManager()
	{
		Init();
	}

	public static ProjectManager GetInstance()
	{
		if (instance == null)
		{
			lock (syncObj)
			{
				if (instance == null)
				{
					instance = new ProjectManager();
				}
			}
		}
		return instance;
	}

	private void OpenSolutionExecute(object solPath)
	{
		if (solPath is string path)
		{
			TitleMenueItemData.LoadSolution(path);
		}
		else if (solPath is bool isRemoteMode)
		{
			LoadSolution(null, isGuid: false, null, null, isRemoteMode);
		}
		else
		{
			LoadSolution();
		}
	}

	private void Init()
	{
		ItemsPage.OnLoadSolution += ProjectManager_OnLoadSolution;
	}

	public bool SaveSolution(Task afterSaveTask = null, bool bDisplayProgress = true, bool isRemoteSave = false)
	{
		if (!string.IsNullOrEmpty(VmSolution.Instance.SolutionPath) && ((isRemoteSave && VmSolution.Instance.SolutionPath.StartsWith("/root")) || (!isRemoteSave && !VmSolution.Instance.SolutionPath.StartsWith("/root"))))
		{
			LogHelper.Info("Save solution path: " + VmSolution.Instance.SolutionPath);
			if (!bDisplayProgress)
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					SaveCalibUserData();
				});
				VmSolution.Export(VmSolution.Instance.SolutionPath, null, bDisplayProgress);
			}
			else
			{
				SaveSolution(VmSolution.Instance.SolutionPath, null, afterSaveTask);
			}
			UILogManager.logCreate("信息", AppLocalizationService.GetLang("保存方案，路径") + VmSolution.Instance.SolutionPath, false, true);
			return true;
		}
		return AnotherSaveSolution(afterSaveTask, bDisplayProgress, isRemoteSave);
	}

	private void SaveCalibUserData()
	{
		foreach (VmProcedure item in VmSolution.Instance.Modules.OfType<VmProcedure>())
		{
			((IVmProcedure)item).SaveCalibData();
		}
	}

	public bool SaveSolution(string strPath, string strPassWord = null, Task afterSaveTask = null)
	{
		if (!VmSolution.Instance.IsHasPassword && strPassWord == null)
		{
			strPassWord = "";
		}
		Application.Current.Dispatcher.Invoke(() =>
		{
			SaveCalibUserData();
		});
		if (VmSolution.Export(strPath, strPassWord))
		{
			Program.MainData.OnPropertyChanged("SolutionPath");
			Program.MainData.OnPropertyChanged("SolutionName");
			afterSaveTask?.Start();
			strCurrentSolutionFileName = strPath;
			return true;
		}
		return false;
	}

	public bool AnotherSaveSolution(Task afterSaveTask = null, bool bDisplayProgress = true, bool isRemoteSave = false)
	{
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Expected Obj, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected Obj, but got Unknown
		//IL_009e: Expected Obj, but got Unknown
		//IL_00a4: Expected Obj, but got Unknown
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected Obj, but got Unknown
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Expected Obj, but got Unknown
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Invalid comparison between Unknown and I4
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected Obj, but got Unknown
		if (VmSolution.Instance.IsHasPassword)
		{
			Decryption decryption = new Decryption();
			decryption.SolutionPath = strCurrentSolutionFileName;
			if (decryption.ShowDialog() != true)
			{
				return false;
			}
		}
		if (!isRemoteSave)
		{
			string text = ConfigurationHelper.INIGetStringValue("SaveSol");
			FileSaveDialog val = new FileSaveDialog();
			FileTypeFilterCollection val2 = new FileTypeFilterCollection();
			((Collection<FileTypeFilter>)val2).Add(new FileTypeFilter(AppLocalizationService.GetLang("解决方案"), "." + AppFileExtension.Solution));
			((FileSaveDialogBase)val).FileTypeFilters = val2;
			((FileSaveDialogBase)val).DefaultFileExtension = "." + AppFileExtension.Solution;
			((FileSaveDialogBase)val).RestoreDirectory = true;
			FileSaveDialog val3 = val;
			try
			{
				if (text != null && Directory.Exists(text))
				{
					((FileDialogBase)val3).InitialFolder = new ShellFolder(ShellItem.FromParsingName(text));
				}
				FileDialogButton val4 = new FileDialogButton("encryptButton", AppLocalizationService.GetLang("加密选项"));
				val4.Click += button_Click;
				((Collection<FileDialogControl>)(object)((FileDialogBase)val3).Controls).Add((FileDialogControl)(object)val4);
				if ((int)val3.Show(BaseWindow.LastWindow) == 1)
				{
					string text2 = ((tempStrPassWord == null) ? "" : tempStrPassWord);
					bool flag = true;
					if (!bDisplayProgress)
					{
						Application.Current.Dispatcher.Invoke(() =>
						{
							SaveCalibUserData();
						});
						VmSolution.SaveAs(val3.ShellFile.Path, text2);
					}
					else
					{
						flag = BeginSave(val3.ShellFile.Path, text2, afterSaveTask);
					}
					UpdateAutoLoadLaseSol(val3.ShellFile.Path, text2);
					string directoryName = Path.GetDirectoryName(val3.ShellFile.Path);
					if (Directory.Exists(directoryName))
					{
						ConfigurationHelper.INISetStringValue("SaveSol", directoryName);
					}
					LogHelper.Info("Another save solution path: " + val3.ShellFile.Path);
					UILogManager.logCreate("信息", AppLocalizationService.GetLang("另存为方案，路径：") + val3.ShellFile.Path, false, true);
					tempStrPassWord = "";
					if (flag)
					{
						UpdateRecentlySolutions(val3.ShellFile.Path);
					}
					return flag;
				}
				return false;
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		string initialDirectory = ConfigurationHelper.INIGetStringValue("SaveSolRemote");
		SaveFileDialog val5 = new SaveFileDialog
		{
			Filter = AppLocalizationService.GetLang("解决方案") + "|.solx",
			InitialDirectory = initialDirectory
		};
		if (((BaseWindow)(object)val5).ShowDialog() == true)
		{
			string text3 = ((tempStrPassWord == null) ? "" : tempStrPassWord);
			bool flag2 = true;
			if (!bDisplayProgress)
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					SaveCalibUserData();
				});
				VmSolution.Export(val5.FilePath, text3, bDisplayProgress);
			}
			else
			{
				flag2 = BeginSave(val5.FilePath, text3, afterSaveTask, isRemoteSave: true);
			}
			string directoryName2 = val5.GetDirectoryName();
			if (!string.IsNullOrWhiteSpace(directoryName2))
			{
				ConfigurationHelper.INISetStringValue("SaveSolRemote", directoryName2);
			}
			LogHelper.Info("Another save solution path: " + val5.FilePath);
			UILogManager.logCreate("信息", AppLocalizationService.GetLang("另存为方案，路径：") + val5.FilePath, false, true);
			tempStrPassWord = "";
			if (flag2)
			{
				UpdateRecentlySolutions(val5.FilePath);
			}
			return flag2;
		}
		return false;
	}

	private bool BeginSave(string path, string strPassWord, Task afterSaveTask = null, bool isRemoteSave = false)
	{
		if (!isRemoteSave)
		{
			try
			{
				Path.GetFullPath(path);
			}
			catch (PathTooLongException message)
			{
				LogHelper.objLog.Warn(message);
				uint num = 3758096905u;
				string errorInfo = ErrorCodeInfo.GetErrorInfo((int)num);
				HikMessageBox.Show(Application.Current.MainWindow, AppLocalizationService.GetLang("Lang_SolSaveFail"), errorInfo, MessageIcons.Warning);
				return false;
			}
			catch (Exception message2)
			{
				LogHelper.objLog.Warn(message2);
				HikMessageBox.Show(Application.Current.MainWindow, AppLocalizationService.GetLang("Lang_SolSaveFail"), AppLocalizationService.GetLang("Lang_FileSaveError"), MessageIcons.Warning);
				return false;
			}
		}
		return SaveSolution(path, strPassWord, afterSaveTask);
	}

	private void button_Click(object sender, EventArgs e)
	{
		Encryption encryption = new Encryption();
		if (encryption.ShowDialog() == true && !string.IsNullOrEmpty(encryption.AccessAdministatorCode))
		{
			HikMessageBox.Show(Application.Current.MainWindow, AppLocalizationService.GetLang("Lang_Sure"), AppLocalizationService.GetLang("SubWindowPassword") + "：" + encryption.textCodeSet.Password + "\r\n" + AppLocalizationService.GetLang("Lang_Password"), MessageIcons.Warning);
			tempStrPassWord = encryption.AccessAdministatorCode;
		}
	}

	private string GetNewFilePath()
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		if (!Directory.Exists(baseDirectory + "\\TempFiles"))
		{
			Directory.CreateDirectory(baseDirectory + "\\TempFiles");
		}
		string text = baseDirectory + "TempFiles\\0." + AppFileExtension.Solution;
		string text2 = "." + AppFileExtension.Solution;
		string directoryName = Path.GetDirectoryName(text);
		string text3 = text;
		string result = null;
		string config = ConfigurationHelper.GetConfig("UnRedoStackCount");
		uint result2 = 15u;
		if (!uint.TryParse(config, out result2))
		{
			result2 = 15u;
		}
		if (File.Exists(text3))
		{
			for (uint num = 1u; num < result2; num++)
			{
				text3 = directoryName + "\\" + num + text2;
				if (!File.Exists(text3))
				{
					result = text3;
					break;
				}
			}
		}
		else
		{
			result = text3;
		}
		return result;
	}

	internal bool CreateNewSolution()
	{
		Ioc.Get<IUnRedoService>()?.Register(new VM.Core.RootOperation
		{
			RootOperType = "SolutionCreate"
		}, AppLocalizationService.GetLang("ChangeSolution") ?? "");
		string solutionPath = VmSolution.Instance.SolutionPath;
		string displayName = AppLocalizationService.GetLang("Lang_Edit") ?? "";
		if (string.IsNullOrEmpty(solutionPath))
		{
			solutionPath = GetNewFilePath();
			try
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					SaveCalibUserData();
				});
				VmSolution.Export(solutionPath, "", bDisplayProgress: false);
				Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
				{
					SolutionType = "oldSolution",
					Path = solutionPath
				}), displayName);
			}
			catch
			{
				Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
				{
					SolutionType = "oldSolution",
					Path = null
				}), displayName);
			}
		}
		else
		{
			Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
			{
				SolutionType = "oldSolution",
				Path = solutionPath
			}), displayName);
		}
		VMControls.WPF.DataForUI.Clipboard.instance.ClearCopyInfo();
		try
		{
			((IVmSolution)VmSolution.Instance).CreatNewInstance();
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
		}
		finally
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				Program.MainData.OnPropertyChanged("SolutionPath");
				Program.MainData.OnPropertyChanged("SolutionName");
			});
		}
		Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
		{
			SolutionType = "newSolution",
			Path = null
		}), displayName);
		return true;
	}

	private bool CreateNewSolWithRelease()
	{
		if (GlobalCommunicationMgr.Instance.IsGet)
		{
			GlobalCommunicationMgr.Instance.CloseComm();
			GlobalCommunicationMgr.Instance.IsGet = false;
		}
		TitleMenueItemData.ResetResource();
		CreateNewSolution();
		ServerSDKManager.serverSDKManager.mSolutionManager.StartGlobalScript();
		return true;
	}

	private static void ProjectManager_OnLoadSolution(string path)
	{
		if (!string.IsNullOrEmpty(path) && Path.GetExtension(path).Equals("." + AppFileExtension.Solution))
		{
			GetInstance().LoadSolution(path);
		}
	}

	internal bool LoadSolution(string filename = null, bool isGuid = false, string solPassWord = null, string initialDirectory = null, bool isRemoteMode = false, bool isFromAppStart = false, bool isFromAppArgs = false, bool isShowSaveNotice = true)
	{
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected Obj, but got Unknown
		LogHelper.Info("界面LoadSolution----开始加载方案");
		bool? flag;
		if (!string.IsNullOrEmpty(filename))
		{
			flag = true;
		}
		else if (!isRemoteMode)
		{
			string path = ((initialDirectory == null) ? ConfigurationHelper.INIGetStringValue("OpenSol") : initialDirectory);
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = AppLocalizationService.GetLang("解决方案") + "|*." + AppFileExtension.Solution
			};
			Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(openFileDialog, path);
			flag = openFileDialog.ShowDialog();
			if (flag != true)
			{
				return true;
			}
			filename = openFileDialog.FileName;
			ConfigurationHelper.INISetStringValue("OpenSol", Path.GetDirectoryName(filename));
		}
		else
		{
			string initialDirectory2 = ((initialDirectory == null) ? ConfigurationHelper.INIGetStringValue("OpenSolRemote") : initialDirectory);
			OpenFileDialog val = new OpenFileDialog
			{
				Filter = "Solution Files (*.solx)|*.solx",
				CheckFileExists = true,
				InitialDirectory = initialDirectory2
			};
			flag = ((BaseWindow)(object)val).ShowDialog();
			if (flag == true)
			{
				filename = val.FileNames.First();
				string directoryName = val.GetDirectoryName();
				ConfigurationHelper.INISetStringValue("OpenSolRemote", directoryName);
			}
		}
		if (flag == true)
		{
			bool flag2 = false;
			uint nHasPwd = 0u;
			uint nValidFormat = 0u;
			try
			{
				ServerSDKManager.serverSDKManager.mSolutionManager.CheckSolution(filename, ref nHasPwd, ref nValidFormat);
			}
			catch (VmException vmException)
			{
				ShowMessage.ShowVmExceptionMessage(vmException);
				return false;
			}
			string passWord = "";
			if (nValidFormat != 1)
			{
				if (!isGuid)
				{
					LoadSolutionFailed(filename, AppLocalizationService.GetLang("Lang_SolLoad"), AppLocalizationService.GetLang("Lang_SolFmt"));
				}
				return false;
			}
			if (nHasPwd == 1)
			{
				if (solPassWord == null)
				{
					Decryption decryption = new Decryption();
					decryption.SolutionPath = filename;
					if (decryption.ShowDialog() != true)
					{
						flag2 = true;
					}
					passWord = decryption.AccessAdministatorCode;
				}
				else
				{
					int nIsCorrect = 0;
					ServerSDKManager.serverSDKManager.mSolutionManager.CheckPassword(filename, solPassWord, ref nIsCorrect);
					if (nIsCorrect == 0)
					{
						throw new VmException(-536868864);
					}
					passWord = solPassWord;
				}
			}
			if (!flag2)
			{
				bool needShowSaveNotice = NeedShowSaveNotice;
				Task task = new Task(() =>
				{
					ImvsSdkDefine.IMVS_SOLUTION_VERSION_INFO pstSolutionVersionInfo = default;
					int num = ServerSDKAPI.IMVS_GetSolutionVersion(CVmObjectBase.mhandle, filename, passWord, ref pstSolutionVersionInfo);
					if (num != 0)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + num.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang(ErrorCodeInfo.GetErrorInfo(num)), MessageIcons.Error);
					}
					else if (num == 0 && string.Compare(pstSolutionVersionInfo.strTargetSolutionInfo, "V4.0.0") < 0)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolVersion_OutOfDate") + "\r\n" + AppLocalizationService.GetLang("版本信息") + pstSolutionVersionInfo.strTargetSolutionInfo, MessageIcons.Error);
					}
					else
					{
						Ioc.Get<IUnRedoService>()?.Register(new VM.Core.RootOperation(), AppLocalizationService.GetLang("ChangeSolution") ?? "");
						string solutionPath = VmSolution.Instance.SolutionPath;
						string displayStr = AppLocalizationService.GetLang("Lang_Edit") ?? "";
						if (string.IsNullOrEmpty(solutionPath))
						{
							solutionPath = GetNewFilePath();
							try
							{
								LogHelper.Info("Restore solution path: " + solutionPath);
								Application.Current.Dispatcher.Invoke(() =>
								{
									SaveCalibUserData();
								});
								VmSolution.Export(solutionPath, "", bDisplayProgress: false);
								Application.Current.Dispatcher.Invoke(() =>
								{
									Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
									{
										SolutionType = "oldSolution",
										Path = solutionPath
									}), displayStr);
								});
							}
							catch
							{
								Application.Current.Dispatcher.Invoke(() =>
								{
									Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
									{
										SolutionType = "oldSolution",
										Path = null
									}), displayStr);
								});
							}
						}
						else
						{
							Application.Current.Dispatcher.Invoke(() =>
							{
								Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
								{
									SolutionType = "oldSolution",
									Path = solutionPath
								}), displayStr);
							});
						}
						if (GlobalCommunicationMgr.Instance.IsGet)
						{
							GlobalCommunicationMgr.Instance.CloseComm();
							GlobalCommunicationMgr.Instance.IsGet = false;
						}
						LogHelper.Info("Load solution path: " + filename);
						bool flag3 = false;
						bool flag4 = false;
						string lang = AppLocalizationService.GetLang("Lang_version");
						if (num == 0 && string.Compare(pstSolutionVersionInfo.strTargetSolutionInfo, lang) < 0)
						{
							flag4 = true;
							if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "CompatibleTool.exe") || !File.Exists(AppDomain.CurrentDomain.BaseDirectory + "CompatibleTool.exe.config"))
							{
								HikMessageBox.Show("CompatibleTool.exe" + AppLocalizationService.GetLang("Lang_FileNotEixt"), MessageIcons.Ok, MessageButtons.NoButton, 3000u);
								return;
							}
							Program.MainData.MaskMessage = AppLocalizationService.GetLang("sol_updating");
							Program.MainData.IsShowProgressMask = true;
							string text = Path.GetDirectoryName(filename) + "\\" + Path.GetFileNameWithoutExtension(filename) + "_";
							int? num2 = null;
							string text2;
							do
							{
								text2 = text + lang + ((!num2.HasValue) ? "" : $"_{num2}") + Path.GetExtension(filename);
								num2 = (num2 ?? 0) + 1;
							}
							while (File.Exists(text2));
							Process[] processesByName = Process.GetProcessesByName("CompatibleTool");
							if (processesByName.Length != 0 && processesByName[0].MainModule.FileName != AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "CompatibleTool.exe")
							{
								processesByName[0].Kill();
							}
							string text3 = text2;
							Process process = new Process();
							process.StartInfo.FileName = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "CompatibleTool.exe";
							process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
							process.StartInfo.Arguments = "VMSolution;\"" + filename + "\";" + passWord + ";\"" + text2 + "\";" + lang;
							process.StartInfo.UseShellExecute = false;
							process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
							process.StartInfo.CreateNoWindow = true;
							process.StartInfo.RedirectStandardOutput = true;
							process.StartInfo.RedirectStandardError = true;
							process.Start();
							process.WaitForExit();
							string infoKey = process.StandardOutput.ReadToEnd();
							Program.MainData.IsShowProgressMask = false;
							if (!File.Exists(text3))
							{
								HikMessageBox.Show(AppLocalizationService.GetLang("升级失败"), AppLocalizationService.GetLang(infoKey), MessageIcons.Warning);
								return;
							}
							filename = text3;
							flag3 = true;
						}
						Application.Current.Dispatcher.Invoke(() =>
						{
							Ioc.Get<IUnRedoService>()?.Register(new SolutionUnRedoOperation(Redo_Solution, Undo_Solution, new UnRedo_SolutionData
							{
								SolutionType = "newSolution",
								Path = filename
							}), displayStr);
						});
						try
						{
							if (!((IVmSolution)VmSolution.Instance).IsLoading)
							{
								VmSolution.Load(filename, passWord);
							}
						}
						catch (VmException vmException2)
						{
							ShowMessage.ShowVmExceptionMessage(vmException2);
							return;
						}
						catch (Exception ex)
						{
							HikMessageBox.Show(ex.Message);
							return;
						}
						strCurrentSolutionFileName = VmSolution.Instance.SolutionPath;
						Program.MainData.OnPropertyChanged("SolutionPath");
						Program.MainData.OnPropertyChanged("SolutionName");
						UpdateRecentlySolutions(strCurrentSolutionFileName);
						if (flag4)
						{
							if (flag3)
							{
								HikMessageBox.Show(AppLocalizationService.GetLang("sol_Updated") + "\r\n" + Path.GetFileName(filename), MessageIcons.Ok);
							}
							else
							{
								strCurrentSolutionFileName = string.Empty;
								Program.MainData.OnPropertyChanged("SolutionPath");
								Program.MainData.OnPropertyChanged("SolutionName");
							}
						}
					}
				});
				VMControls.WPF.DataForUI.Clipboard.instance.ClearCopyInfo();
				if (VmSolution.Instance.Modules.Count > 0)
				{
					if (isFromAppArgs && !string.IsNullOrEmpty(VmSolution.Instance.SolutionPath))
					{
						DialogResult dialogResult = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OpenSol"), AppLocalizationService.GetLang("Lang_AllowSwitchSolution"), MessageButtons.YesOrNo, MessageIcons.Question);
						DialogResult dialogResult2 = dialogResult;
						if (dialogResult2 != DialogResult.Yes && dialogResult2 == DialogResult.No)
						{
							return true;
						}
					}
					if ((needShowSaveNotice & isShowSaveNotice) && !isFromAppStart)
					{
						switch (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolSave"), AppLocalizationService.GetLang("Lang_SolCurSave"), MessageButtons.YesOrNoOrCancel, MessageIcons.Question))
						{
						case DialogResult.Yes:
						{
							bool isRemoteSave = false;
							SaveSolution(task, bDisplayProgress: true, isRemoteSave);
							return true;
						}
						default:
							return true;
						case DialogResult.No:
							break;
						}
					}
				}
				task.Start();
			}
			UpdateAutoLoadLaseSol(filename, passWord);
		}
		return true;
	}

	private void UpdateAutoLoadLaseSol(string solPath, string solPassword)
	{
		SoftWareData softWareData = new SoftWareData();
		if (softWareData.GetAutoLoadSolState() == 0 && softWareData.IsAutoLoadLast)
		{
			softWareData.SolPath = solPath;
			softWareData.SolPassword = solPassword;
			softWareData.SetAutoLoadSol();
		}
	}

	public bool SolutionSaveCheck(bool bDisplayProgress = false)
	{
		bool result = false;
		if (VmSolution.Instance.Modules.Count > 0 && NeedShowSaveNotice)
		{
			switch (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolSave"), AppLocalizationService.GetLang("Lang_SolCurSave"), MessageButtons.YesOrNoOrCancel, MessageIcons.Question))
			{
			case DialogResult.Yes:
				try
				{
					SaveSolution(null, bDisplayProgress);
				}
				catch (VmException vmException)
				{
					ShowMessage.ShowVmExceptionMessage(vmException);
					result = true;
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message);
					result = true;
				}
				break;
			default:
				result = true;
				break;
			case DialogResult.No:
				break;
			}
		}
		return result;
	}

	private void LoadSolutionFailed(string solname, string erro, string details)
	{
		if (!RemoveSolution(solname, details))
		{
			Thread thread = new Thread(() =>
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(erro, details, MessageButtons.Normal, MessageIcons.Error);
				});
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground = true;
			thread.Start();
		}
		OnPropertyChanged("RecentlySolutions");
	}

	public static bool ExistRecentlySolution(string path)
	{
		List<SolutionItemInfo> recentlySolutions = GetRecentlySolutions();
		return recentlySolutions.Any((SolutionItemInfo p) => p.SolutionPath == path);
	}

	public static List<SolutionItemInfo> GetRecentlySolutions()
	{
		if (_recentlySolutions == null)
		{
			string uri = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, ConfigurationHelper.GetConfig("ApplicationName") + ".cfg");
			XDocument xDocument = XDocument.Load(uri);
			if (xDocument.Root == null)
			{
				return null;
			}
			XElement xElement = xDocument.Root.Elements().SingleOrDefault((XElement p) => p.Name == "ArrayOfSolutionItemInfo");
			_recentlySolutions = ((xElement == null) ? new List<SolutionItemInfo>() : xElement.DeSerialization<List<SolutionItemInfo>>());
		}
		_recentlySolutions.RemoveAll((SolutionItemInfo p) => p.GeTimeSpan().Days > 99);
		return _recentlySolutions.OrderByDescending((SolutionItemInfo p) => p.LastTime).ToList();
	}

	public static void SaveRecentlySolutions()
	{
		try
		{
			string uri = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, ConfigurationHelper.GetConfig("ApplicationName") + ".cfg");
			XDocument xDocument = XDocument.Load(uri);
			if (xDocument.Root != null)
			{
				xDocument.Root.Elements().SingleOrDefault((XElement p) => p.Name == "ArrayOfSolutionItemInfo")?.Remove();
				xDocument.Root.Add(GetRecentlySolutions().ToXElement(omitXmlDeclaration: true));
				ConfigurationHelper.SaveConfig(xDocument);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("Save recent solutions error." + ex.Message);
		}
	}

	public static void UpdateRecentlySolutions(string solutionpath)
	{
		if (string.IsNullOrEmpty(solutionpath))
		{
			return;
		}
		List<SolutionItemInfo> recentlySolutions = GetRecentlySolutions();
		SolutionItemInfo solutionItemInfo = recentlySolutions.FirstOrDefault((SolutionItemInfo p) => p.SolutionPath == solutionpath);
		if (solutionItemInfo != null)
		{
			solutionItemInfo.LastTime = DateTime.Now;
		}
		else
		{
			solutionItemInfo = new SolutionItemInfo
			{
				SolutionPath = solutionpath,
				LastTime = DateTime.Now
			};
			if (_recentlySolutions.Count > 5)
			{
				_recentlySolutions.Remove(recentlySolutions[recentlySolutions.Count - 1]);
			}
			_recentlySolutions.Insert(0, solutionItemInfo);
		}
		SaveRecentlySolutions();
		Program.MainData.RefreshRecentSolution();
	}

	internal static bool OpenRecentSolution(SolutionItemInfo sol, bool isGuid = false)
	{
		if (sol == null)
		{
			return false;
		}
		RecentSolution = sol;
		return GetInstance().LoadSolution(sol.SolutionPath, isGuid);
	}

	internal static void RemoveRecentSolution(string path)
	{
		if (_recentlySolutions == null)
		{
			string uri = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, ConfigurationHelper.GetConfig("ApplicationName") + ".cfg");
			XDocument xDocument = XDocument.Load(uri);
			if (xDocument.Root == null)
			{
				return;
			}
			XElement xElement = xDocument.Root.Elements().SingleOrDefault((XElement p) => p.Name == "ArrayOfSolutionItemInfo");
			_recentlySolutions = ((xElement == null) ? new List<SolutionItemInfo>() : xElement.DeSerialization<List<SolutionItemInfo>>());
		}
		_recentlySolutions.RemoveAll((SolutionItemInfo p) => p.SolutionPath == path);
		SaveRecentlySolutions();
		Program.MainData.RefreshRecentSolution();
	}

	public static bool RemoveSolution(string path, string erroinfo = null)
	{
		if (string.IsNullOrEmpty(path) && RecentSolution != null)
		{
			path = RecentSolution.SolutionPath;
		}
		RecentSolution = null;
		if (!ExistRecentlySolution(path))
		{
			return false;
		}
		string[] msg = ((!string.IsNullOrEmpty(erroinfo)) ? new string[2]
		{
			erroinfo,
			AppLocalizationService.GetLang("Lang_SolRM")
		} : new string[1] { AppLocalizationService.GetLang("Lang_SolOpenErrtoRM") });
		Thread thread = new Thread(() =>
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolOpenErr"), msg, MessageButtons.YesOrNo, MessageIcons.Error) == DialogResult.Yes)
				{
					RemoveRecentSolution(path);
				}
			});
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.IsBackground = true;
		thread.Start();
		return true;
	}

	public bool GetRunnable()
	{
		throw new NotImplementedException();
	}

	private bool ValitPassWord(object dispatcher, string path, out string passWord)
	{
		if (string.IsNullOrEmpty(path))
		{
			passWord = "";
			return false;
		}
		bool isCancel = false;
		uint nHasPwd = 0u;
		uint nValidFormat = 0u;
		try
		{
			ServerSDKManager.serverSDKManager.mSolutionManager.CheckSolution(path, ref nHasPwd, ref nValidFormat);
		}
		catch (VmException ex)
		{
			throw ex;
		}
		string inPassWord = "";
		if (nValidFormat == 1 && nHasPwd == 1)
		{
			(dispatcher as Dispatcher).Invoke(() =>
			{
				Decryption decryption = new Decryption
				{
					SolutionPath = path
				};
				if (decryption.ShowDialog() != true)
				{
					isCancel = true;
				}
				inPassWord = decryption.AccessAdministatorCode;
			});
		}
		passWord = inPassWord;
		return isCancel;
	}

	internal void Redo_Solution(UnRedo_SolutionData solutionData, IUnRedoParams unRedoParams)
	{
		if (!(solutionData.SolutionType == "newSolution"))
		{
			return;
		}
		string path = solutionData.Path;
		string passWord = "";
		if (ValitPassWord(unRedoParams.Dispatcher, path, out passWord))
		{
			unRedoParams.RecoverCallBack?.Compile()();
			return;
		}
		if (path != null)
		{
			VmSolution.Import(path, passWord);
			Thread.Sleep(1000);
		}
		else
		{
			CreateNewSolWithRelease();
		}
		Program.MainData.OnPropertyChanged("SolutionPath");
		Program.MainData.OnPropertyChanged("SolutionName");
	}

	internal void Undo_Solution(UnRedo_SolutionData solutionData, IUnRedoParams unRedoParams)
	{
		if (!(solutionData.SolutionType == "oldSolution"))
		{
			return;
		}
		string path = solutionData.Path;
		string passWord = "";
		if (ValitPassWord(unRedoParams.Dispatcher, path, out passWord))
		{
			unRedoParams.RecoverCallBack?.Compile()();
			return;
		}
		if (path != null)
		{
			VmSolution.Import(path, passWord);
			Thread.Sleep(1000);
		}
		else
		{
			CreateNewSolWithRelease();
		}
		Program.MainData.OnPropertyChanged("SolutionPath");
		Program.MainData.OnPropertyChanged("SolutionName");
	}
}
