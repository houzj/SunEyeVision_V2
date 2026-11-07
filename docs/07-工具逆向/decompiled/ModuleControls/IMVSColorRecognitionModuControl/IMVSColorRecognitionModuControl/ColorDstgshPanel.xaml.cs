using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Threading;
using Apps.ColorFun;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using ModelCommonHelper;
using VM.Controls;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSColorRecognitionModuControl;

public partial class ColorDstgshPanel : System.Windows.Controls.UserControl, IParentNode, IComponentConnector
{
	private uint IMVS_OK = 0u;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(ColorDstgshPanel), new PropertyMetadata(null));

	private bool _callProcess;

	private IVmModule _module;

	private IParamsConfig _param;

	private IVMRun vMRun;

	private bool _creatNew;

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public IVmModule Module
	{
		get
		{
			if (_module != null)
			{
				return _module;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return _module;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return _module;
			}
			if (nameValueItem.GetTopBaseNode() is IOwner owner)
			{
				_module = owner.Owner as IVmModule;
			}
			return _module;
		}
	}

	public IParamsConfig Param
	{
		get
		{
			if (_param != null)
			{
				return _param;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return null;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return null;
			}
			if (nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				_param = algorithmTabRoot.UserStringData;
			}
			return _param;
		}
	}

	public IVMRun VMRun
	{
		get
		{
			if (vMRun != null)
			{
				return vMRun;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return vMRun;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return vMRun;
			}
			if (nameValueItem.GetTopBaseNode() is IOwner owner)
			{
				vMRun = owner.Owner as IVMRun;
			}
			return vMRun;
		}
	}

	public ColorDstgshPanel()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			GetTimeOut();
			ItemsControl.ItemsSource = GetModelImages();
			Button.IsEnabledChanged += btn_IsEnabledChanged;
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			BindingOperations.ClearBinding(Button, UIElement.IsEnabledProperty);
			BindingOperations.ClearBinding(Menu, UIElement.IsEnabledProperty);
			Button.IsEnabledChanged -= btn_IsEnabledChanged;
		};
	}

	public void GetTimeOut()
	{
		string empty = string.Empty;
		empty = ((IUserStringData)Param)["TimeOut"];
		if (uint.TryParse(empty, out var result))
		{
			Param.TimeOut = result;
		}
	}

	public ObservableCollection<ModelImage> GetModelImages()
	{
		if (Param == null)
		{
			return null;
		}
		try
		{
			byte[] array = ((IUserBytesData)Param)["GetModelImage"];
			if (array.Length != 0)
			{
				return ModelImage.GetModuleImages(array);
			}
		}
		catch (Exception ex)
		{
			if (ex.Message != "E0001101")
			{
				int num = Convert.ToInt32(ex.Message, 16);
				throw new Exception(ErrorCodeInfo.GetErrorInfo(num));
			}
		}
		return null;
	}

	public object GetParentNode()
	{
		throw new NotImplementedException();
	}

	public void SetParentNode(object parentNode)
	{
		throw new NotImplementedException();
	}

	private void LoadModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null)
		{
			return;
		}
		if (VMRun != null && VMRun.IsRunning)
		{
			HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
			return;
		}
		ObservableCollection<ModelImage> observableCollection = ItemsControl.ItemsSource as ObservableCollection<ModelImage>;
		bool flag = false;
		if (observableCollection != null && observableCollection.Count > 0)
		{
			flag = true;
			if (HikMessageBox.Show("Lang_Sure", "是否清空已有的颜色模板？", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true) != VM.Controls.DialogResult.Yes)
			{
				return;
			}
		}
		if (flag)
		{
			ClearModel();
		}
		Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, (Action)(() =>
		{
			string text = ConfigurationHelper.INIGetDefaultOpenPath(Module?.ModuleDir);
			using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog())
			{
				openFileDialog.Title = AppLocalizationService.GetLang("获取") + AppLocalizationService.GetLang("Lang_TemplateFile");
				openFileDialog.Filter = AppLocalizationService.GetLang("Lang_TemplateFile") + "|*.bin";
				if (text != null)
				{
					openFileDialog.InitialDirectory = text;
				}
				if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
				{
					return;
				}
				text = openFileDialog.FileName;
				ConfigurationHelper.INISetDefaultOpenPath(text, Module?.ModuleDir);
			}
			if (!string.IsNullOrEmpty(text))
			{
				byte[] libFile = EncryptionAndDecryptionHelper.GetLibFile(text);
				if (libFile != null)
				{
					SendLibDataToModule(libFile);
				}
			}
		}));
	}

	public void SendLibDataToModule(byte[] buf)
	{
		try
		{
			((IUserBytesData)Param)["SetModelData"] = buf;
			ItemsControl.ItemsSource = GetModelImages();
			HikMessageBox.Show(AppLocalizationService.GetLang("加载模板文件成功"), MessageIcons.Ok);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + ex.Message, MessageIcons.Error);
		}
	}

	private void ExportModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null)
		{
			return;
		}
		if (VMRun != null && VMRun.IsRunning)
		{
			HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
		}
		else if (ItemsControl.ItemsSource is ObservableCollection<ModelImage> { Count: not 0 })
		{
			string text = ConfigurationHelper.INIGetDefaultOpenPath(Module?.ModuleDir);
			bool flag = false;
			string strMsg = string.Empty;
			using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
			{
				saveFileDialog.Title = AppLocalizationService.GetLang("Save") + AppLocalizationService.GetLang("Lang_TemplateFile");
				saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_TemplateFile") + "|*.bin";
				if (text != null)
				{
					saveFileDialog.InitialDirectory = text;
				}
				if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
				{
					return;
				}
				try
				{
					text = saveFileDialog.FileName;
				}
				catch (Exception ex)
				{
					strMsg = ex.Message;
					flag = true;
				}
			}
			if (flag)
			{
				HikMessageBox.Show("导出模板文件错误", strMsg, MessageIcons.Error, isTranslate: true);
			}
			else if (!string.IsNullOrEmpty(text))
			{
				byte[] array = null;
				array = ((IUserBytesData)Param)["GetModelData"];
				if (array == null)
				{
					HikMessageBox.Show("导出模板文件错误", "导出模板文件失败", MessageIcons.Error, isTranslate: true);
				}
				else if (EncryptionAndDecryptionHelper.WriteLibFile(text, array))
				{
					HikMessageBox.Show("导出模板文件成功", MessageIcons.Ok, isTranslate: true);
					ConfigurationHelper.INISetDefaultOpenPath(text, Module?.ModuleDir);
				}
			}
		}
		else
		{
			HikMessageBox.Show("模板文件不存在", MessageIcons.Error, isTranslate: true);
		}
	}

	public bool ClearModel()
	{
		if (Param == null)
		{
			return false;
		}
		byte[] value = new byte[1];
		((IUserBytesData)Param)["DeleteModel"] = value;
		ItemsControl.ItemsSource = null;
		return true;
	}

	private void CreatModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null)
		{
			return;
		}
		if (VMRun != null && VMRun.IsRunning)
		{
			HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
			return;
		}
		_creatNew = false;
		ObservableCollection<ModelImage> observableCollection = ItemsControl.ItemsSource as ObservableCollection<ModelImage>;
		bool flag = false;
		if (observableCollection != null && observableCollection.Count > 0)
		{
			flag = true;
			if (HikMessageBox.Show("Lang_Sure", "是否清空已有的颜色模板？", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true) != VM.Controls.DialogResult.Yes)
			{
				return;
			}
		}
		if (flag)
		{
			if (!ClearModel())
			{
				return;
			}
			if (_callProcess)
			{
				_creatNew = true;
			}
		}
		OpenDlg();
	}

	private void OpenDlg()
	{
		BaseWindow baseWindow = new BaseWindow
		{
			ShowInTaskbar = false
		};
		baseWindow.Height = 618.0;
		baseWindow.Width = 836.0;
		baseWindow.Title = AppLocalizationService.GetLang("Lang_ModelSet");
		baseWindow.FontSize = 14.0;
		baseWindow.FontWeight = FontWeights.Normal;
		baseWindow.MinButtonVisibility = Visibility.Collapsed;
		ColorThemesResourceDictionary.GetInstance(baseWindow);
		ColorDstgshDialog content = new ColorDstgshDialog
		{
			Owner = baseWindow,
			Module = Module,
			Param = Param
		};
		baseWindow.Content = content;
		baseWindow.ShowDialog();
		(ParentNode as IRunService)?.PreExecuteRunModel();
		ItemsControl.ItemsSource = GetModelImages();
	}

	private void btn_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue && _callProcess && _creatNew)
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				OpenDlg();
				_creatNew = false;
			}));
		}
		_callProcess = false;
	}

	private void ClearModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null)
		{
			return;
		}
		if (VMRun != null && VMRun.IsRunning)
		{
			HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
			return;
		}
		ObservableCollection<ModelImage> observableCollection = ItemsControl.ItemsSource as ObservableCollection<ModelImage>;
		bool flag = false;
		if (observableCollection != null && observableCollection.Count > 0 && HikMessageBox.Show("Lang_Sure", "是否清空已有的颜色模板？", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true) == VM.Controls.DialogResult.Yes)
		{
			flag = true;
		}
		if (flag && !ClearModel())
		{
		}
	}

	private void Process()
	{
	}

	private void EditModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param != null)
		{
			if (VMRun != null && VMRun.IsRunning)
			{
				HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
			}
			else if (ItemsControl.ItemsSource is ObservableCollection<ModelImage> { Count: >0 })
			{
				OpenDlg();
			}
			else
			{
				HikMessageBox.Show("模板文件不存在", MessageIcons.Error, isTranslate: true);
			}
		}
	}
}
