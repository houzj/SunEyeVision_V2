using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;
using PasswordWindowUI;
using ShellCommonControl;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;

namespace PyShellModuleControl;

public partial class PyShellRun : UserControl, INotifyPropertyChanged, IComponentConnector
{
	public static readonly DependencyProperty CodeFileNameProperty = DependencyProperty.Register("CodeFileName", typeof(string), typeof(PyShellRun), new PropertyMetadata(""));

	public static readonly DependencyProperty TargetValueProperty = DependencyProperty.Register("TargetValue", typeof(string), typeof(PyShellRun), new PropertyMetadata("", TargetValueChanged));

	public static readonly DependencyProperty IsPasswordEnabledProperty = DependencyProperty.Register("IsPasswordEnabled", typeof(bool), typeof(PyShellRun), new PropertyMetadata(false));

	public static readonly DependencyProperty IsShowPasswordProperty = DependencyProperty.Register("IsShowPassword", typeof(bool), typeof(PyShellRun), new PropertyMetadata(false));

	public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(PyShellRun), new PropertyMetadata(""));

	public static readonly DependencyProperty RefrencesProperty = DependencyProperty.Register("Refrences", typeof(string), typeof(PyShellRun), new PropertyMetadata(null));

	public static readonly DependencyProperty IsShowProcessbarProperty = DependencyProperty.Register("IsShowProcessbar", typeof(string), typeof(PyShellRun), new PropertyMetadata("Collapsed"));

	private bool _isEditCode = true;

	private const int DATA_MAX_VALUE = 20971520;

	private int nModuleID = -1;

	private const int REPORT_UPDATE_CODE = 255;

	private bool isBusy = false;

	private string _novalue = AppLocalizationService.GetLang("Lang_RstEpt");

	public ObservableCollection<string> CurVarNullValues = new ObservableCollection<string> { AppLocalizationService.GetLang("Lang_RstEpt") };

	public const string _globalSciptKey = "GlobalScipt-";

	private List<IVmModule> EnableModuleResultIDList = new List<IVmModule>();

	internal TextEditor textEditorControl;

	public string CodeFileName
	{
		get
		{
			return (string)GetValue(CodeFileNameProperty);
		}
		set
		{
			SetValue(CodeFileNameProperty, value);
		}
	}

	public string TargetValue
	{
		get
		{
			return (string)GetValue(TargetValueProperty);
		}
		set
		{
			SetValue(TargetValueProperty, value);
		}
	}

	public bool IsPasswordUnEnabled
	{
		get
		{
			return !(bool)GetValue(IsPasswordEnabledProperty);
		}
		set
		{
			SetValue(IsPasswordEnabledProperty, !value);
		}
	}

	public bool IsPasswordEnabled
	{
		get
		{
			return (bool)GetValue(IsPasswordEnabledProperty);
		}
		set
		{
			SetValue(IsPasswordEnabledProperty, value);
			OnPropertyChanged("IsPasswordUnEnabled");
			IsEditCode = !value;
		}
	}

	public bool IsShowPassword
	{
		get
		{
			return (bool)GetValue(IsShowPasswordProperty);
		}
		set
		{
			SetValue(IsShowPasswordProperty, value);
		}
	}

	public string Password
	{
		get
		{
			return (string)GetValue(PasswordProperty);
		}
		set
		{
			SetValue(PasswordProperty, value);
		}
	}

	public string Refrences
	{
		get
		{
			return (string)GetValue(RefrencesProperty);
		}
		set
		{
			SetValue(RefrencesProperty, value);
		}
	}

	public string IsShowProcessbar
	{
		get
		{
			return (string)GetValue(IsShowProcessbarProperty);
		}
		set
		{
			SetValue(IsShowProcessbarProperty, value);
		}
	}

	public bool IsEditCode
	{
		get
		{
			return _isEditCode;
		}
		set
		{
			if (value != _isEditCode)
			{
				_isEditCode = value;
				OnPropertyChanged("IsEditCode");
			}
		}
	}

	public DynamicIOContainer<ShellDynamicIO> InputCategory
	{
		get
		{
			if (!(DataContext is ButtonSelecter_PyShell buttonSelecter_PyShell))
			{
				return null;
			}
			if (buttonSelecter_PyShell.ShellInVarContainerA != null)
			{
				return buttonSelecter_PyShell.ShellInVarContainerA;
			}
			return null;
		}
	}

	public DynamicIOContainer<ShellDynamicIO> OutputCategory
	{
		get
		{
			if (!(DataContext is ButtonSelecter_PyShell buttonSelecter_PyShell))
			{
				return null;
			}
			if (buttonSelecter_PyShell.ShellOutVarContainerA != null)
			{
				return buttonSelecter_PyShell.ShellOutVarContainerA;
			}
			return null;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public static void TargetValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		PyShellRun pyShellRun = (PyShellRun)d;
		string text = e.NewValue.ToString();
		if (pyShellRun.textEditorControl.Text != text)
		{
			pyShellRun.textEditorControl.Text = text;
		}
	}

	public PyShellRun()
	{
		InitializeComponent();
		Loaded += OnLoad;
		inputParamCtrl.AddVariableCompleted += inputParamCtrl_AddVariableCompleted;
		outParamCtrl.AddVariableCompleted += inputParamCtrl_AddVariableCompleted;
	}

	private void inputParamCtrl_AddVariableCompleted(object sender, ObjectEventArgs e)
	{
		AddVariableCompleted(Convert.ToInt32(e.Param));
	}

	private void AddVariableCompleted(int paramType)
	{
		switch (paramType)
		{
		case 1:
			SetInputModuleResult(isEnable: true);
			break;
		}
	}

	private void OnLoad(object sender, RoutedEventArgs e)
	{
		initTextEdit();
		InitPassward();
		textEditorControl.Text = TargetValue;
		textEditorControl.Document.FileName = CodeFileName;
		AddVariableCompleted(1);
		AddVariableCompleted(2);
		LogHelper.Info("参数配置弹窗打开，注册获取回调结果事件！");
		ServerSDKManager.serverSDKManager.mModuleManager.OnModuleResultCallBack += OnModuleResultCallBack;
		ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM += ServerManager_OnReportDataToVM;
		nModuleID = (int)((DataContext as ButtonSelecter_PyShell).GetVmModule()?.ID).Value;
		GetLatestResult();
		SetInputModuleResult(isEnable: true);
	}

	private void initTextEdit()
	{
		string name = Assembly.GetExecutingAssembly().GetName().Name + ".Python-Mode.xshd";
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		using (Stream input = executingAssembly.GetManifestResourceStream(name))
		{
			using XmlTextReader xmlTextReader = new XmlTextReader(input);
			XshdSyntaxDefinition val = HighlightingLoader.LoadXshd((XmlReader)xmlTextReader);
			textEditorControl.SyntaxHighlighting = HighlightingLoader.Load(val, (IHighlightingDefinitionReferenceResolver)(object)HighlightingManager.Instance);
		}
		textEditorControl.Encoding = Encoding.GetEncoding("GB2312");
	}

	private void ServerManager_OnReportDataToVM(ImvsSdkDefine.IMVS_ALGORITHM_INIT_RESULT_INFO algResultInfor)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			textBoxCompile.AppendText(algResultInfor.strTransData + "\n");
		}));
	}

	private void InitPassward()
	{
		byte[] array = (DataContext as ButtonSelecter_PyShell).GetVmModule().UserData["passward"];
		if (array != null)
		{
			string text = StringUtil.UTF8GetString(array);
			IsPasswordEnabled = bool.Parse(text.Split(':')[0]);
			Password = text.Split(':')[1];
		}
	}

	private void ClearRichText_OnClick(object sender, RoutedEventArgs e)
	{
		textBoxCompile.Clear();
	}

	private void textEditorControl_TextChanged(object sender, EventArgs e)
	{
		TargetValue = ((TextEditor)((sender is TextEditor) ? sender : null)).Text;
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				e.Handled = true;
			}
		}
	}

	private void ShellControl_Unloaded(object sender, RoutedEventArgs e)
	{
		try
		{
			LogHelper.objLog.Info("参数配置弹窗关闭，注销获取回调结果事件！");
			VmModule vmModule = (DataContext as ButtonSelecter_PyShell)?.GetVmModule();
			IVmModule vmModule2;
			if (vmModule != null && (vmModule2 = vmModule) != null)
			{
				vmModule2.ModuleResultCallBackArrived -= OnModuleCallBackArrived;
			}
			ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM -= ServerManager_OnReportDataToVM;
			ServerSDKManager.serverSDKManager.mModuleManager.OnModuleResultCallBack -= OnModuleResultCallBack;
			SetInputModuleResult(isEnable: false);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Info("unload exception:" + ex.ToString());
		}
	}

	private void OnModuleResultCallBack(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data result)
	{
		Dispatcher?.BeginInvoke((Action)(() =>
		{
			GetRealResult(result);
		}));
		Thread.Sleep(50);
	}

	private void OnModuleCallBackArrived(object sender, EventArgs e)
	{
		GetModuleResult();
	}

	private void GetModuleResult()
	{
		Application.Current?.Dispatcher.Invoke(() =>
		{
			GetLatestResult();
		});
	}

	private void GetRealResult(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data dataResult)
	{
		try
		{
			if (InputCategory == null)
			{
				return;
			}
			foreach (ShellDynamicIO item in InputCategory.ItemsA)
			{
				if (item.IoDataSource == null)
				{
					item.ShellInputVarA = CurVarNullValues;
					continue;
				}
				VmIO relateIO = item.IoDataSource.RelateIO;
				if (relateIO == null)
				{
					continue;
				}
				if (relateIO.FullName.Contains("TRIGGER_STRING"))
				{
					item.ShellInputVarA = null;
					continue;
				}
				uint num = ((relateIO.Root != null) ? relateIO.Root.ID : 0u);
				if (num == 13000 || ServerSDKManager.serverSDKManager.mModuleManager.CheckLocalVariableModule((int)num))
				{
					item.ShellInputVarA = ShellHelper.GetVarResult(relateIO);
					continue;
				}
				uint? num2 = relateIO.Root?.ID;
				uint num3 = dataResult.nModuleID;
				if (num2.GetValueOrDefault() == num3 && num2.HasValue && !(relateIO is VmConstantIO))
				{
					item.ShellInputVarA = ShellHelper.GetIOResultByCallBack(dataResult, relateIO);
				}
				else if (relateIO is VmConstantIO)
				{
					item.ShellInputVarA = ShellHelper.GetIOResult(relateIO);
				}
			}
			if (OutputCategory == null)
			{
				return;
			}
			foreach (ShellDynamicIO item2 in OutputCategory.ItemsA)
			{
				if (item2.IoDataSource == null)
				{
					item2.ShellOutputVarA = CurVarNullValues;
				}
				else if (item2.IoDataSource.Root?.ID == dataResult.nModuleID)
				{
					item2.ShellOutputVarA = ShellHelper.GetIOResultByCallBack(dataResult, item2.IoDataSource);
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetModuleResult is error:" + ex.ToString());
		}
	}

	private void SetInputModuleResult(bool isEnable)
	{
		try
		{
			if ((InputCategory != null) & isEnable)
			{
				foreach (ShellDynamicIO item in InputCategory.ItemsA)
				{
					if (item.IoDataSource == null)
					{
						item.ShellInputVarA = CurVarNullValues;
						continue;
					}
					VmIO relateIO = item.IoDataSource.RelateIO;
					IVmModule root;
					if (relateIO != null && relateIO.Root != null && (root = relateIO.Root) != null && root.ID != 13000 && !EnableModuleResultIDList.Contains(root))
					{
						root.SubscribeCount++;
						EnableModuleResultIDList.Add(root);
					}
				}
			}
			if (isEnable)
			{
				return;
			}
			foreach (IVmModule enableModuleResultID in EnableModuleResultIDList)
			{
				if (enableModuleResultID != null && enableModuleResultID.SubscribeCount > 0)
				{
					enableModuleResultID.SubscribeCount--;
				}
			}
			EnableModuleResultIDList.Clear();
		}
		catch (Exception ex)
		{
			LogHelper.Error("SetInputModuleResult is error:" + ex.ToString());
		}
	}

	private void GetLatestResult()
	{
		try
		{
			if (InputCategory != null)
			{
				foreach (ShellDynamicIO item in InputCategory.ItemsA)
				{
					if (item.IoDataSource == null)
					{
						item.ShellInputVarA = CurVarNullValues;
						continue;
					}
					VmIO relateIO = item.IoDataSource.RelateIO;
					if (relateIO != null)
					{
						if (relateIO.FullName.Contains("TRIGGER_STRING"))
						{
							item.ShellInputVarA = null;
							continue;
						}
						uint num = ((relateIO.Root != null) ? relateIO.Root.ID : 0u);
						if (num == 13000 || ServerSDKManager.serverSDKManager.mModuleManager.CheckLocalVariableModule((int)num))
						{
							item.ShellInputVarA = ShellHelper.GetVarResult(relateIO);
						}
						else if (relateIO.Root is IMVSGroup)
						{
							ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(relateIO.Root.ID);
							if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
							{
								ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data iMVS_MODULE_RESULT_INFO_LIST_EX_Data = ServerSDKManager.serverSDKManager.mModuleManager.PreProData(iMVS_MODULE_RESULT_INFO_LIST_EX.Value);
								item.ShellInputVarA = ShellHelper.GetIOResultByCallBack(iMVS_MODULE_RESULT_INFO_LIST_EX_Data, relateIO);
							}
						}
						else
						{
							item.ShellInputVarA = ShellHelper.GetIOResult(relateIO);
						}
					}
					else
					{
						item.ShellInputVarA = CurVarNullValues;
					}
				}
			}
			if (OutputCategory == null)
			{
				return;
			}
			foreach (ShellDynamicIO item2 in OutputCategory.ItemsA)
			{
				if (item2.IoDataSource == null)
				{
					item2.ShellOutputVarA = CurVarNullValues;
				}
				else
				{
					item2.ShellOutputVarA = ShellHelper.GetIOResult(item2.IoDataSource);
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetModuleResult is error:" + ex.ToString());
		}
	}

	private void btnInportCode_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Filter = "(*.py) | *.py";
		IVmModule vmModule = (DataContext as ButtonSelecter_PyShell).GetVmModule();
		string text = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		string fileName = openFileDialog.FileName;
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), vmModule?.ModuleDir);
		string text2 = fileName.Substring(fileName.LastIndexOf('.') + 1);
		if ("py" != text2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ShellFmtErr"), MessageIcons.Error);
			return;
		}
		try
		{
			FileInfo fileInfo = new FileInfo(fileName);
			if (fileInfo.Length > 20971520)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileTooLarge"), MessageIcons.Error);
				return;
			}
			textEditorControl.Text = File.ReadAllText(fileName, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("脚本代码导入失败") + "!：" + ex.Message, MessageIcons.Error);
			return;
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ShellCodeLoadSuc"), MessageIcons.Ok);
		if (DataContext is ButtonSelecter_PyShell buttonSelecter_PyShell)
		{
			bool isCompileRetOK = false;
			buttonSelecter_PyShell.CompileShellCode(out isCompileRetOK);
			if (isCompileRetOK)
			{
			}
		}
	}

	private void btnExportCode_Click(object sender, RoutedEventArgs e)
	{
		string text = textEditorControl.Text;
		if (string.IsNullOrWhiteSpace(text))
		{
			HikMessageBox.Show("Lang_ShellEditfst", MessageIcons.Error, isTranslate: true);
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
		saveFileDialog.Filter = " python source files(*.py)|*.py";
		saveFileDialog.RestoreDirectory = true;
		IVmModule vmModule = (DataContext as ButtonSelecter_PyShell).GetVmModule();
		string text2 = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
		if (text2 != null)
		{
			saveFileDialog.InitialDirectory = text2;
		}
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			string text3 = "temp.py";
			using (TextWriter textWriter = File.CreateText(text3))
			{
				textWriter.Write(text);
				textWriter.Flush();
			}
			string fileName = saveFileDialog.FileName;
			if (File.Exists(fileName))
			{
				FileInfo fileInfo = new FileInfo(fileName);
				if (fileInfo.Attributes.ToString().IndexOf("ReadOnly") != -1)
				{
					fileInfo.Attributes = FileAttributes.Normal;
				}
				File.Delete(fileName);
			}
			string directoryName = Path.GetDirectoryName(fileName);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.Copy(text3, fileName, overwrite: true);
			File.Delete(text3);
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), vmModule?.ModuleDir);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("脚本代码导出失败") + "！：" + ex.Message, MessageIcons.Error);
			return;
		}
		HikMessageBox.Show("Lang_ShellCodeOutPSuc", MessageIcons.Ok, isTranslate: true);
	}

	private void btnEnablePermissions_Click(object sender, RoutedEventArgs e)
	{
		Dispatcher.Invoke(() =>
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected Obj, but got Unknown
			try
			{
				PasswordSetWindow val = new PasswordSetWindow();
				((BaseWindow)(object)val).ShowDialog();
				if (val.IsPasswordOK)
				{
					Password = val.Password;
					IsPasswordEnabled = true;
					inputParamCtrl.SwitchToDisplay();
					outParamCtrl.SwitchToDisplay();
					string str = "true:" + Password;
					(DataContext as ButtonSelecter_PyShell).GetVmModule().UserData["passward"] = StringUtil.UTF8GetBytesPadZero(str);
				}
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error(ex.ToString());
			}
		});
	}

	private void btnCancelPermissions_Click(object sender, RoutedEventArgs e)
	{
		Dispatcher.Invoke(() =>
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected Obj, but got Unknown
			try
			{
				PasswordCheckWindow val = new PasswordCheckWindow();
				val.OriginalPassword = Password;
				((BaseWindow)(object)val).ShowDialog();
				if (val.IsPasswordOK)
				{
					IsPasswordEnabled = false;
					Password = null;
					string str = "false:";
					(DataContext as ButtonSelecter_PyShell).GetVmModule().UserData["passward"] = StringUtil.UTF8GetBytesPadZero(str);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.Message);
			}
		});
	}

	private void btnEditCode_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		PasswordCheckWindow val = new PasswordCheckWindow();
		val.OriginalPassword = Password;
		((BaseWindow)(object)val).ShowDialog();
		if (val.IsPasswordOK)
		{
			IsEditCode = true;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
