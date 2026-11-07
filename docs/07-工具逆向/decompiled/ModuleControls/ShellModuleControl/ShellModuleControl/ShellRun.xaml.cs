using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
using Apps.Data;
using Apps.Json;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using GlobalScriptUI;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using PasswordWindowUI;
using Script.Render;
using ShellCommonControl;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;

namespace ShellModuleControl;

public partial class ShellRun : UserControl, IRender, INotifyPropertyChanged, IComponentConnector
{
	private bool _isShowImage = false;

	private HikMessageBox _messageBox = null;

	private bool _isInit = false;

	private bool _isEditCode = true;

	public static readonly DependencyProperty CodeFileNameProperty = DependencyProperty.Register("CodeFileName", typeof(string), typeof(ShellRun), new PropertyMetadata(""));

	public static readonly DependencyProperty TargetValueProperty = DependencyProperty.Register("TargetValue", typeof(string), typeof(ShellRun), new PropertyMetadata("", TargetValueChanged));

	public static readonly DependencyProperty IsPasswordEnabledProperty = DependencyProperty.Register("IsPasswordEnabled", typeof(bool), typeof(ShellRun), new PropertyMetadata(false));

	public static readonly DependencyProperty IsShowPasswordProperty = DependencyProperty.Register("IsShowPassword", typeof(bool), typeof(ShellRun), new PropertyMetadata(false));

	public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(ShellRun), new PropertyMetadata(""));

	public static readonly DependencyProperty RefrencesProperty = DependencyProperty.Register("Refrences", typeof(string), typeof(ShellRun), new PropertyMetadata(null));

	public static readonly DependencyProperty IsShowProcessbarProperty = DependencyProperty.Register("IsShowProcessbar", typeof(string), typeof(ShellRun), new PropertyMetadata("Collapsed"));

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(ShellRun), new PropertyMetadata(null));

	private const int DATA_MAX_VALUE = 20971520;

	private const int MAX_LINE_LEN = 1024;

	private int nModuleID = -1;

	private const int REPORT_UPDATE_CODE = 255;

	private bool isBusy = false;

	private string _novalue = AppLocalizationService.GetLang("Lang_RstEpt");

	public ObservableCollection<string> CurVarNullValues = new ObservableCollection<string> { AppLocalizationService.GetLang("Lang_RstEpt") };

	public const string _globalSciptKey = "GlobalScipt-";

	private bool IsSingleMode = true;

	private string vmProxyPath = "";

	private List<IVmModule> EnableModuleResultIDList = new List<IVmModule>();

	private const string USERCLASSHEAD = "public partial ";

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

	public ImageData CurImage
	{
		get
		{
			return (ImageData)GetValue(CurImageProperty);
		}
		set
		{
			SetValue(CurImageProperty, value);
		}
	}

	public bool IsShowImage
	{
		get
		{
			return _isShowImage;
		}
		set
		{
			if (value == _isShowImage)
			{
				return;
			}
			_isShowImage = value;
			Window window = Window.GetWindow(this);
			if (window != null)
			{
				if (value)
				{
					window.Width = 1450.0;
				}
				else
				{
					window.Width = 900.0;
				}
			}
			OnPropertyChanged("CodeImageControlWidth");
			OnPropertyChanged("ImageControlWidth");
			OnPropertyChanged("ImageControlMinWidth");
			OnPropertyChanged("IsShowImage");
		}
	}

	public string CodeImageControlWidth
	{
		get
		{
			if (IsShowImage)
			{
				return "1136*";
			}
			return "586*";
		}
	}

	public string ImageControlWidth
	{
		get
		{
			if (IsShowImage)
			{
				return "550*";
			}
			return "Auto";
		}
	}

	public double ImageControlMinWidth
	{
		get
		{
			if (IsShowImage)
			{
				return 200.0;
			}
			return 0.0;
		}
	}

	public double TabItemWidth => (AppLocalizationService.GetLang("Language") == "English") ? 105 : 76;

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

	object IRender.Dispatcher => Dispatcher;

	ImageData IRender.Image
	{
		get
		{
			return CurImage;
		}
		set
		{
			if (RenderManager.Instance.IsForbiddenRender)
			{
				ShowVersionWarn();
				return;
			}
			IsShowImage = true;
			CurImage = value;
		}
	}

	public DynamicIOContainer<ShellDynamicIO> InputCategory
	{
		get
		{
			if (!(DataContext is ButtonSelecter_Shell buttonSelecter_Shell))
			{
				return null;
			}
			if (buttonSelecter_Shell.ShellInVarContainerA != null)
			{
				return buttonSelecter_Shell.ShellInVarContainerA;
			}
			return null;
		}
	}

	public DynamicIOContainer<ShellDynamicIO> OutputCategory
	{
		get
		{
			if (!(DataContext is ButtonSelecter_Shell buttonSelecter_Shell))
			{
				return null;
			}
			if (buttonSelecter_Shell.ShellOutVarContainerA != null)
			{
				return buttonSelecter_Shell.ShellOutVarContainerA;
			}
			return null;
		}
	}

	public string VmProxyPath
	{
		get
		{
			if (string.IsNullOrEmpty(vmProxyPath))
			{
				vmProxyPath = GetShellPath();
			}
			return vmProxyPath;
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
		ShellRun shellRun = (ShellRun)d;
		string text = e.NewValue.ToString();
		if (((TextEditor)shellRun.textEditorControl).Text != text)
		{
			((TextEditor)shellRun.textEditorControl).Text = text;
		}
	}

	public void DrawShape(object shape)
	{
		renderControl?.DrawShape(shape);
	}

	public void ShowVersionWarn()
	{
		if (_messageBox == null)
		{
			string strMsg = string.Format("{0}{1}{2}", AppLocalizationService.GetLang("Lang_InsufficientDonglePermissions"), Environment.NewLine, AppLocalizationService.GetLang("Lang_RenderMethodName"));
			_messageBox = HikMessageBox.CreateMessageBox(strMsg, MessageIcons.Warning);
			_messageBox.ShowDialog();
			_messageBox = null;
		}
	}

	public ShellRun()
	{
		InitializeComponent();
		Loaded += OnLoad;
		Unloaded += OnUnload;
		inputParamCtrl.AddVariableCompleted += inputParamCtrl_AddVariableCompleted;
		outParamCtrl.AddVariableCompleted += inputParamCtrl_AddVariableCompleted;
		renderControl?.ChangeImageComboBoxVisibility(flag: false);
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
			textEditorControl.AddShellVariable(InputCategory, paramType);
			SetInputModuleResult(isEnable: true);
			break;
		case 2:
			textEditorControl.AddShellVariable(OutputCategory, paramType);
			break;
		}
	}

	private void OnLoad(object sender, RoutedEventArgs e)
	{
		RenderManager.Instance.SetRenderControl(this);
		if (_isInit)
		{
			return;
		}
		_isInit = true;
		InitPassward();
		InitTextEdit();
		((TextEditor)textEditorControl).Text = TargetValue;
		textEditorControl.nTextLen = ((TextEditor)textEditorControl).Text.Length;
		textEditorControl.FileName = CodeFileName;
		((TextEditor)textEditorControl).Document.FileName = CodeFileName;
		AddVariableCompleted(1);
		AddVariableCompleted(2);
		if (!string.IsNullOrEmpty(Refrences))
		{
			try
			{
				List<ShellRefrences> list = JsonConvert.DeserializeObject<List<ShellRefrences>>(Refrences);
				textEditorControl.UpdateAssembile(list.ToArray());
			}
			catch (Exception)
			{
			}
		}
		LogHelper.Info("参数配置弹窗打开，注册获取回调结果事件！");
		ServerSDKManager.serverSDKManager.mModuleManager.OnModuleResultCallBack += OnModuleResultCallBack;
		ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM += ServerManager_OnReportDataToVM;
		nModuleID = (int)((DataContext as ButtonSelecter_Shell).GetVmModule()?.ID).Value;
		GetLatestResult();
		SetInputModuleResult(isEnable: true);
		ParamRoot paramRoot = (DataContext as ButtonSelecter_Shell).GetVmModule()["[Display]"] as ParamRoot;
		List<DynamicObject> list2 = (from p in paramRoot.GetDisplayList().OfType<DynamicObject>()
			where p.RelateIO == null
			select p).ToList();
		if (list2 == null || list2.Count <= 0)
		{
			return;
		}
		list2.ForEach((DynamicObject q) =>
		{
			DynamicIOContainer<ShellDynamicIO> outputCategory = OutputCategory;
			ShellDynamicIO shellDynamicIO = ((outputCategory != null) ? outputCategory.ItemsA.OfType<ShellDynamicIO>().FirstOrDefault((ShellDynamicIO p) => p.IoDataSource.Name == q.NameA) : null);
			if (shellDynamicIO != null)
			{
				q.RelateIO = shellDynamicIO.IoDataSource;
			}
			else
			{
				q.RemoveSelf();
			}
		});
	}

	private void OnUnload(object sender, RoutedEventArgs e)
	{
		RenderManager.Instance.SetRenderControl(null);
	}

	private void ServerManager_OnReportDataToVM(ImvsSdkDefine.IMVS_ALGORITHM_INIT_RESULT_INFO algResultInfor)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			uint num = 0u;
			int num2 = -1;
			num2 = (int)algResultInfor.nModuleID;
			num = algResultInfor.nType;
			if (num2 == nModuleID && num == 255)
			{
				UpdateSourceCode();
			}
		}));
	}

	private void UpdateSourceCode()
	{
		string path = VmProxyPath + $"\\UserScript\\UserScript_{nModuleID}\\UserScript.cs";
		if (File.Exists(path))
		{
			string text = File.ReadAllText(path, Encoding.UTF8);
			string text2 = ((TextEditor)textEditorControl).Text;
			if (text != text2)
			{
				((TextEditor)textEditorControl).Text = text;
			}
		}
	}

	private void InitTextEdit()
	{
		((TextEditor)textEditorControl).Encoding = Encoding.GetEncoding("GB2312");
	}

	private void InitPassward()
	{
		byte[] array = (DataContext as ButtonSelecter_Shell).GetVmModule()?.UserData["passward"];
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

	private void EditRefrences_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected Obj, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected Obj, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Expected Obj, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected Obj, but got Unknown
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected Obj, but got Unknown
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected Obj, but got Unknown
		List<ShellRefrences> list = null;
		if (string.IsNullOrEmpty(Refrences))
		{
			list = new List<ShellRefrences>();
			list.Add(new ShellRefrences
			{
				Name = "mscorlib.dll",
				refrencesType = (RefrencesType)0
			});
			list.Add(new ShellRefrences
			{
				Name = "System.dll",
				refrencesType = (RefrencesType)0
			});
			list.Add(new ShellRefrences
			{
				Name = "System.Core.dll",
				refrencesType = (RefrencesType)0
			});
			list.Add(new ShellRefrences
			{
				Name = "System.Windows.Forms.dll",
				refrencesType = (RefrencesType)0
			});
			list.Add(new ShellRefrences
			{
				Name = "Script.Methods.dll",
				refrencesType = (RefrencesType)3
			});
		}
		else
		{
			list = JsonConvert.DeserializeObject<List<ShellRefrences>>(Refrences);
		}
		if (list == null)
		{
			return;
		}
		RefrenceEditUI val = new RefrenceEditUI(list, (ScriptType)1);
		if (((BaseWindow)(object)val).ShowDialog() == true)
		{
			list = val.RefrencesList.ToList();
			Refrences = JsonConvert.SerializeObject(list);
			string text = textEditorControl.SetListAssembly(list);
			if (!string.IsNullOrEmpty(text))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_AssemblyEmpty") + "\r\n" + text, MessageButtons.Normal, MessageIcons.Warning);
			}
		}
	}

	private void BtnExportSoluion_Click(object sender, RoutedEventArgs e)
	{
		IsShowProcessbar = "Visible";
		isBusy = true;
		ExportSolution();
	}

	private string GetShellPath()
	{
		try
		{
			if (!IsSingleMode)
			{
				Process[] processesByName = Process.GetProcessesByName("VmModuleProxy");
				if (processesByName != null && processesByName.Length != 0)
				{
					string fileName = processesByName[0].MainModule.FileName;
					if (!string.IsNullOrEmpty(fileName))
					{
						fileName = fileName.Substring(0, fileName.LastIndexOf("\\"));
						DirectoryInfo directoryInfo = new DirectoryInfo(fileName);
						return directoryInfo.Parent.Parent.FullName + "\\Module(sp)\\x64\\Logic\\ShellModule";
					}
				}
			}
			else
			{
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				Assembly assembly = assemblies.FirstOrDefault((Assembly x) => x.GetName().Name == "Script.Support");
				if (assembly != null)
				{
					return assembly.Location.Substring(0, assembly.Location.LastIndexOf("\\"));
				}
			}
		}
		catch (Exception)
		{
			LogHelper.Error("GetShellPath is error");
		}
		return "";
	}

	private async void ExportSolution()
	{
		string shelltxt = ((TextEditor)textEditorControl).Text;
		Task createCodeTask = Task.Factory.StartNew(() =>
		{
			try
			{
				string text = VmProxyPath + "\\UserScript\\DefaultSln";
				if (Directory.Exists(text))
				{
					string text2 = "UserScript_" + nModuleID;
					string text3 = VmProxyPath + "\\UserScript\\" + text2;
					if (Directory.Exists(text3))
					{
						Directory.Delete(text3, recursive: true);
					}
					if (!Directory.Exists(text3))
					{
						Directory.CreateDirectory(text3);
					}
					CopyDir(text, text3);
					string text4 = Path.Combine(text3, "UserScript.csproj");
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.Load(text4);
					XmlElement xmlElement = (XmlElement)xmlDocument.GetElementsByTagName("PropertyGroup").Item(0);
					xmlElement.GetElementsByTagName("AssemblyName").Item(0).InnerText = text2;
					xmlDocument.Save(text4);
					File.Move(text4, Path.Combine(text3, $"{text2}.csproj"));
					string text5 = Path.Combine(text3, $"{text2}.sln");
					File.Move(Path.Combine(text3, "UserScript.sln"), text5);
					string text6 = File.ReadAllText(text5, Encoding.UTF8);
					text6 = text6.Replace("UserScript", text2);
					using (StreamWriter streamWriter = new StreamWriter(text5, append: false, Encoding.UTF8))
					{
						streamWriter.Write(text6);
					}
					string path = text3 + "\\UserScript.cs";
					byte[] bytes = Encoding.UTF8.GetBytes(shelltxt);
					using (FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
					{
						fileStream.SetLength(0L);
						fileStream.Write(bytes, 0, bytes.Length);
						fileStream.Flush(flushToDisk: true);
					}
					string arguments = text3 + "\\";
					Process.Start("explorer.exe", arguments);
					Dispatcher.Invoke(() =>
					{
						try
						{
							VmModule vmModule = (DataContext as ButtonSelecter_Shell).GetVmModule();
							if (vmModule != null)
							{
								vmModule.Params["Exportsln"] = "";
							}
						}
						catch
						{
						}
					});
				}
			}
			catch (Exception ex)
			{
				HikMessageBox.Show("导出工程失败", MessageButtons.Normal);
				LogHelper.objLog.Error("导出工程失败:" + ex.ToString());
			}
		});
		await createCodeTask;
		IsShowProcessbar = "Collapsed";
		isBusy = false;
	}

	private void CopyDir(string srcpath, string dstpath)
	{
		if (!Directory.Exists(srcpath))
		{
			throw new Exception("path not exists:" + srcpath);
		}
		if (!Directory.Exists(dstpath))
		{
			Directory.CreateDirectory(dstpath);
		}
		string[] directories = Directory.GetDirectories(srcpath);
		foreach (string text in directories)
		{
			CopyDir(text + "\\", dstpath + "\\" + Path.GetFileName(text) + "\\");
		}
		string[] files = Directory.GetFiles(srcpath);
		foreach (string text2 in files)
		{
			if (!text2.Contains(".svn"))
			{
				File.Copy(text2, dstpath + "\\" + Path.GetFileName(text2), overwrite: true);
			}
		}
	}

	private void textEditorControl_TextChanged(object sender, EventArgs e)
	{
		TargetValue = ((TextEditor)(sender as ShellCodeEditor)).Text;
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
			VmModule vmModule = (DataContext as ButtonSelecter_Shell)?.GetVmModule();
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
	}

	private void OnModuleCallBackArrived(object sender, EventArgs e)
	{
		GetModuleResult();
	}

	private void GetModuleResult()
	{
		Dispatcher?.BeginInvoke((Action)(() =>
		{
			GetLatestResult();
		}));
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
						else if (relateIO.Root is IMVSGroup iMVSGroup)
						{
							VmIO vmIO = (relateIO.IsOutput ? iMVSGroup.Outputs.FirstOrDefault((VmIO x) => x.TypeName == "pointset") : iMVSGroup.Inputs.FirstOrDefault((VmIO x) => x.TypeName == "pointset"));
							if (vmIO == null || !VmSolution.Instance.IsRunning)
							{
								ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(relateIO.Root.ID);
								if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
								{
									ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data iMVS_MODULE_RESULT_INFO_LIST_EX_Data = ServerSDKManager.serverSDKManager.mModuleManager.PreProData(iMVS_MODULE_RESULT_INFO_LIST_EX.Value);
									item.ShellInputVarA = ShellHelper.GetIOResultByCallBack(iMVS_MODULE_RESULT_INFO_LIST_EX_Data, relateIO);
								}
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
		openFileDialog.Filter = "(*.cs) | *.cs";
		IVmModule vmModule = (DataContext as ButtonSelecter_Shell).GetVmModule();
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
		if ("cs" != text2)
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
			string text3 = "";
			string[] array = File.ReadAllLines(fileName, Encoding.UTF8);
			if (array != null && array.Length != 0)
			{
				string[] array2 = array;
				foreach (string text4 in array2)
				{
					if (!string.IsNullOrEmpty(text3))
					{
						text3 += Environment.NewLine;
					}
					text3 += ((text4.Length > 1024) ? text4.Substring(0, 1024) : text4);
				}
			}
			text3 = CheckShell(text3);
			((TextEditor)textEditorControl).Text = text3;
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("脚本代码导入失败") + "!：" + ex.Message, MessageIcons.Error);
			return;
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ShellCodeLoadSuc"), MessageIcons.Ok);
		if (DataContext is ButtonSelecter_Shell buttonSelecter_Shell)
		{
			bool isCompileRetOK = false;
			buttonSelecter_Shell.CompileShellCode(out isCompileRetOK);
			if (isCompileRetOK)
			{
			}
		}
	}

	private string CheckShell(string strShell)
	{
		if (!string.IsNullOrEmpty(strShell) && !strShell.Contains("public partial "))
		{
			int num = strShell.IndexOf("class UserScript");
			if (num > 0)
			{
				strShell = strShell.Insert(num, "public partial ");
			}
		}
		return strShell;
	}

	private void btnExportCode_Click(object sender, RoutedEventArgs e)
	{
		string text = ((TextEditor)textEditorControl).Text;
		if (string.IsNullOrWhiteSpace(text))
		{
			HikMessageBox.Show("Lang_ShellEditfst", MessageIcons.Error, isTranslate: true);
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
		saveFileDialog.Filter = " Visual C# source files(*.cs)|*.cs";
		saveFileDialog.RestoreDirectory = true;
		IVmModule vmModule = (DataContext as ButtonSelecter_Shell).GetVmModule();
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
			string text3 = "temp.cs";
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
					(DataContext as ButtonSelecter_Shell).GetVmModule().UserData["passward"] = StringUtil.UTF8GetBytesPadZero(str);
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
					(DataContext as ButtonSelecter_Shell).GetVmModule().UserData["passward"] = StringUtil.UTF8GetBytesPadZero(str);
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
