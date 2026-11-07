using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Util.Model;
using Hik.Xml;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace FewShotCommonControl;

public partial class FewShotEdgeLearningPanel : System.Windows.Controls.UserControl, IParentNode, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private IVmModule _module;

	private IParamsConfig _param;

	private ObservableCollection<CategoryData> _CategoryList = new ObservableCollection<CategoryData>();

	private bool _init = false;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(FewShotEdgeLearningPanel), new PropertyMetadata(null));

	private bool _IsRegisterOK = false;

	private string _IsHasAngle;

	private string _TrainImageNum = "0";

	private string _TrainInstanceNum = "0";

	private string _ClassNum = "0";

	private string _ClassTag;

	private string _Epoch = "100";

	private string _ColorEnhanceEnable;

	private string _FlipEnhanceEnable;

	private string _RotateEnhanceEnable;

	private string _ResizeEnhanceEnable;

	public static readonly DependencyProperty CurModelGUIDProperty = DependencyProperty.Register("CurModelGUID", typeof(int), typeof(FewShotEdgeLearningPanel), new PropertyMetadata(0, OnModelChanged));

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
			if (_module == null)
			{
				_module = GetAlgorithmTabRoot()?.Owner as IVmModule;
			}
			return _module;
		}
	}

	public IParamsConfig Param
	{
		get
		{
			if (_param == null)
			{
				_param = GetAlgorithmTabRoot()?.UserStringData;
			}
			return _param;
		}
	}

	public ObservableCollection<CategoryData> CategoryList => _CategoryList;

	public bool IsExistCategory => CategoryList.Count > 0;

	public double RegisterButtonWidth => (AppLocalizationService.GetLang("Language") == "English") ? 135 : 100;

	public bool IsRegisterOK
	{
		get
		{
			return _IsRegisterOK;
		}
		set
		{
			if (_IsRegisterOK != value)
			{
				_IsRegisterOK = value;
				OnPropertyChanged("IsRegisterOK");
			}
		}
	}

	public string IsHasAngle
	{
		get
		{
			return _IsHasAngle;
		}
		set
		{
			if (_IsHasAngle != value)
			{
				_IsHasAngle = value;
				OnPropertyChanged("IsHasAngle");
			}
		}
	}

	public string TrainImageNum
	{
		get
		{
			return _TrainImageNum;
		}
		set
		{
			if (_TrainImageNum != value)
			{
				_TrainImageNum = value;
				OnPropertyChanged("TrainImageNum");
			}
		}
	}

	public string TrainInstanceNum
	{
		get
		{
			return _TrainInstanceNum;
		}
		set
		{
			if (_TrainInstanceNum != value)
			{
				_TrainInstanceNum = value;
				OnPropertyChanged("TrainInstanceNum");
			}
		}
	}

	public string ClassNum
	{
		get
		{
			return _ClassNum;
		}
		set
		{
			if (_ClassNum != value)
			{
				_ClassNum = value;
				OnPropertyChanged("ClassNum");
			}
		}
	}

	public string ClassTag
	{
		get
		{
			return _ClassTag;
		}
		set
		{
			if (_ClassTag != value)
			{
				_ClassTag = value;
				OnPropertyChanged("ClassTag");
			}
		}
	}

	public string Epoch
	{
		get
		{
			return _Epoch;
		}
		set
		{
			if (_Epoch != value)
			{
				_Epoch = value;
				OnPropertyChanged("Epoch");
			}
		}
	}

	public string ColorEnhanceEnable
	{
		get
		{
			return _ColorEnhanceEnable;
		}
		set
		{
			if (_ColorEnhanceEnable != value)
			{
				_ColorEnhanceEnable = value;
				OnPropertyChanged("ColorEnhanceEnable");
			}
		}
	}

	public string FlipEnhanceEnable
	{
		get
		{
			return _FlipEnhanceEnable;
		}
		set
		{
			if (_FlipEnhanceEnable != value)
			{
				_FlipEnhanceEnable = value;
				OnPropertyChanged("FlipEnhanceEnable");
			}
		}
	}

	public string RotateEnhanceEnable
	{
		get
		{
			return _RotateEnhanceEnable;
		}
		set
		{
			if (_RotateEnhanceEnable != value)
			{
				_RotateEnhanceEnable = value;
				OnPropertyChanged("RotateEnhanceEnable");
			}
		}
	}

	public string ResizeEnhanceEnable
	{
		get
		{
			return _ResizeEnhanceEnable;
		}
		set
		{
			if (_ResizeEnhanceEnable != value)
			{
				_ResizeEnhanceEnable = value;
				OnPropertyChanged("ResizeEnhanceEnable");
			}
		}
	}

	public int CurModelGUID
	{
		get
		{
			return (int)GetValue(CurModelGUIDProperty);
		}
		set
		{
			SetValue(CurModelGUIDProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public FewShotEdgeLearningPanel()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			LoadModuleData();
			if (!_init)
			{
				_init = true;
				try
				{
					Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: true);
					QueryAllCategory();
				}
				finally
				{
					Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: false);
				}
				OnPropertyChanged("IsExistCategory");
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: true);
			try
			{
				SaveModuleData();
			}
			catch (VmException ex)
			{
				LogHelper.moduleLog.Error(string.Format("Register finish error, {0}{1}", AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex.errorCode)));
			}
			finally
			{
				Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: false);
			}
		};
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private void RegisterImage_Click(object sender, RoutedEventArgs e)
	{
		if (Module == null || Param == null)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		ModuleCommonMethodMgr.Instance.Param = Param;
		EdgeLearningData.Instance.CurImageData = ModuleCommonMethodMgr.Instance.GetCurImage(Module);
		EdgeLearningData.Instance.Param = Param;
		EdgeLearningData.Instance.OwnerModule = Module;
		EdgeLearningData.Instance.IsEnglish = AppLocalizationService.GetLang("Language") == "English";
		RegisterImageDialog registerImageDialog = new RegisterImageDialog();
		EdgeLearningData.Instance.Owner = registerImageDialog;
		registerImageDialog.Init(CategoryList);
		CreatModel.Instance.IsCreatingModel = true;
		if (registerImageDialog.ShowDialog() == true)
		{
			CategoryList.Clear();
			foreach (CategoryData item in ((IEdgeLearning)registerImageDialog).Items.OfType<CategoryData>().ToList())
			{
				if (item.RegisteredNum != 0)
				{
					CategoryList.Add(item);
				}
			}
			try
			{
				CurModelGUID = 0;
				byte[] array = Module.UserData["HasModel"];
				if (array != null)
				{
					string text = StringUtil.UTF8GetString(array);
					if (text == "True")
					{
						GetModelData();
						IsRegisterOK = true;
					}
					else
					{
						IsRegisterOK = false;
					}
				}
			}
			catch (VmException ex)
			{
				LogHelper.moduleLog.Error(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
			}
			(ParentNode as IRunService)?.PreExecuteRunModel();
		}
		CreatModel.Instance.IsCreatingModel = false;
		registerImageDialog = null;
		GC.Collect();
		CurModelGUID = GetHashCode();
	}

	private void GetModelData()
	{
		try
		{
			IsHasAngle = ((IUserStringData)Param)["EnableModelAngle"];
			TrainImageNum = ((IUserStringData)Param)["GetTrainImageNum"];
			TrainInstanceNum = ((IUserStringData)Param)["GetTrainInstanceNum"];
			ClassNum = ((IUserStringData)Param)["GetLabelTypeNum"];
			int num = int.Parse(ClassNum);
			string text = string.Empty;
			for (int i = 0; i < num; i++)
			{
				string text2 = ((IUserStringData)Param)["GetLabelType_" + i];
				string text3 = text2 + ";";
				text += text3;
			}
			if (!string.IsNullOrEmpty(text))
			{
				string classTag = text.Substring(0, text.LastIndexOf(';'));
				ClassTag = classTag;
			}
			Epoch = ((IUserStringData)Param)["Epoch"];
			ColorEnhanceEnable = ((IUserStringData)Param)["ColorEnhanceEnable"];
			FlipEnhanceEnable = ((IUserStringData)Param)["FlipEnhanceEnable"];
			RotateEnhanceEnable = ((IUserStringData)Param)["RotateEnhanceEnable"];
			ResizeEnhanceEnable = ((IUserStringData)Param)["ResizeEnhanceEnable"];
		}
		catch (VmException ex)
		{
			string text4 = AppLocalizationService.GetLang("Lang_ModuGetDataErr") + "\r\n(";
			text4 += AppLocalizationService.GetLang("Lang_ErrCode");
			text4 += ex.errorCode.ToString("X");
			text4 += AppLocalizationService.GetLang("ErrorInfo: ");
			text4 += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text4 += ")";
			HikMessageBox.Show(text4, MessageIcons.Error);
		}
	}

	private void LoadModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null || Module == null)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		string text = null;
		using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog())
		{
			openFileDialog.Title = AppLocalizationService.GetLang("Lang_OpenModelFile");
			openFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.hregm";
			text = ConfigurationHelper.INIGetDefaultOpenPath(Module.ModuleDir);
			if (text != null)
			{
				openFileDialog.InitialDirectory = text;
			}
			if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}
			text = openFileDialog.FileName;
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module.ModuleDir);
		try
		{
			((IUserStringData)Module.Params)["LoadGalleryFilePath"] = text;
			GetModelData();
			IsRegisterOK = true;
			Module.UserData["HasModel"] = StringUtil.UTF8GetBytesPadZero("True");
			if (!string.IsNullOrEmpty(ClassTag))
			{
				Module.UserData["RegisterClassNames"] = Encoding.UTF8.GetBytes(ClassTag);
			}
			GetSingleCategoryData();
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadSuccess"), MessageIcons.Ok);
			CurModelGUID = 0;
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			string text2 = string.Format("{0}{1}{2}{3}{4}", AppLocalizationService.GetLang("errCode:"), errorCode.ToString("X"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(errorCode));
			LogHelper.moduleLog.Error(text2);
			HikMessageBox.Show(text2, MessageIcons.Error);
			return;
		}
		catch (Exception ex2)
		{
			string lang = AppLocalizationService.GetLang("读取文件失败");
			if (ex2 is EndOfStreamException)
			{
				lang = AppLocalizationService.GetLang("文件格式异常");
			}
			LogHelper.moduleLog.Error(lang);
			HikMessageBox.Show(lang, MessageIcons.Error);
			return;
		}
		QueryAllCategory();
		OnPropertyChanged("IsExistCategory");
		CurModelGUID = GetHashCode();
	}

	private void GetSingleCategoryData()
	{
		int num = int.Parse(ClassNum);
		string[] array = ClassTag.Split(';');
		EdgeLearningData.Instance.UICategoryList.Clear();
		string empty = string.Empty;
		string empty2 = string.Empty;
		string empty3 = string.Empty;
		uint registeredNum = 0u;
		uint registeredROINum = 0u;
		try
		{
			for (int i = 0; i < num; i++)
			{
				empty = array[i];
				empty2 = ((IUserStringData)Param)["GetTrainImageNum_" + i];
				if (!string.IsNullOrEmpty(empty2))
				{
					registeredNum = Convert.ToUInt32(empty2);
				}
				empty3 = ((IUserStringData)Param)["GetTrainInstanceNum_" + i];
				if (!string.IsNullOrEmpty(empty3))
				{
					registeredROINum = Convert.ToUInt32(empty3);
				}
				UICategoryData item = new UICategoryData
				{
					CategoryName = empty,
					RegisteredNum = registeredNum,
					RegisteredROINum = registeredROINum
				};
				EdgeLearningData.Instance.UICategoryList.Add(item);
			}
		}
		catch (VmException ex)
		{
			string errorInfo = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			LogHelper.moduleLog.Error(errorInfo);
		}
		catch (Exception ex2)
		{
			LogHelper.moduleLog.Error(ex2.Message);
		}
	}

	private void ExportModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null || Module == null)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		string text = null;
		using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
		{
			saveFileDialog.Title = AppLocalizationService.GetLang("Save Model File");
			saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.hregm";
			text = ConfigurationHelper.INIGetDefaultOpenPath(Module.ModuleDir);
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
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.moduleLog.Error("Get file name error: " + ex.Message);
				return;
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			((IUserStringData)Module.Params)["SaveGalleryFile"] = text;
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportSuccess"), MessageIcons.Ok);
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module.ModuleDir);
		}
		catch (VmException ex2)
		{
			string text2 = string.Format("{0}{1}{2}{3}", AppLocalizationService.GetLang("Lang_ExportFailed"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex2.errorCode));
			HikMessageBox.Show(text2, MessageIcons.Error);
			LogHelper.moduleLog.Error(text2);
		}
		catch (Exception ex3)
		{
			int result = 0;
			string text3 = AppLocalizationService.GetLang("Lang_ExportFailed") + Environment.NewLine;
			text3 += AppLocalizationService.GetLang("errInfo:");
			string text4 = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex3.Message);
			text3 += text4;
			HikMessageBox.Show(text3, MessageIcons.Error);
			LogHelper.moduleLog.Error(text3);
		}
	}

	private void ClearModel_Click(object sender, RoutedEventArgs e)
	{
		if (Module == null || Param == null)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
		}
		else if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.OkOrCancel, MessageIcons.Question) == VM.Controls.DialogResult.Ok)
		{
			try
			{
				((IUserStringData)Param)["GalleryDelete"] = "";
				((IUserStringData)Param)["ResetTrainParams"] = "";
				CategoryList.Clear();
				CurModelGUID = 0;
				IsRegisterOK = false;
				Module.UserData["HasModel"] = StringUtil.UTF8GetBytesPadZero("False");
				Module.UserData["RegisterClassNames"] = null;
				EdgeLearningData.Instance.UICategoryList.Clear();
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = AppLocalizationService.GetLang("Lang_DeleteCategoryFail") + Environment.NewLine;
				text += AppLocalizationService.GetLang("errInfo:");
				string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				text += text2;
				HikMessageBox.Show(text, MessageIcons.Error);
			}
			CurModelGUID = GetHashCode();
		}
	}

	private void DeleteCategory_OnClick(object sender, RoutedEventArgs e)
	{
		if (Param == null || Module == null)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_DeleteCategory", "Lang_DeleteCategoryTip", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
		{
			System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
			if (button.DataContext is CategoryData category)
			{
				DeleteCategory(category);
				OnPropertyChanged("IsExistCategory");
			}
		}
	}

	private void CategoryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				scrollViewer.Focus();
			}
			e.Handled = true;
		}
	}

	public object GetParentNode()
	{
		throw new NotImplementedException();
	}

	public void SetParentNode(object parentNode)
	{
		throw new NotImplementedException();
	}

	private void QueryAllCategory()
	{
		if (Param == null)
		{
			return;
		}
		CategoryList.Clear();
		int num = 0;
		string text = string.Empty;
		try
		{
			string empty = string.Empty;
			empty = ((IUserStringData)Param)["GetLabelTypeNum"];
			num = int.Parse(empty);
			for (int i = 0; i < num; i++)
			{
				string text2 = ((IUserStringData)Param)["GetLabelType_" + i];
				string text3 = text2 + ";";
				text += text3;
			}
		}
		catch (VmException ex)
		{
			string text4 = AppLocalizationService.GetLang("Lang_GetCategoryCountFail") + "\r\n(";
			text4 += AppLocalizationService.GetLang("errInfo:");
			text4 += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text4 += ")";
			LogHelper.moduleLog.Error(text4);
			return;
		}
		string strCategoryName = string.Empty;
		string value = string.Empty;
		string empty2 = string.Empty;
		uint num2 = 0u;
		uint num3 = 0u;
		char[] separator = new char[1] { ';' };
		string[] array = null;
		if (!string.IsNullOrEmpty(text))
		{
			array = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		}
		if (array == null || array.Length == 0 || array.Length != num)
		{
			return;
		}
		for (int j = 0; j < num; j++)
		{
			try
			{
				strCategoryName = array[j];
				UICategoryData uICategoryData = EdgeLearningData.Instance.UICategoryList.FirstOrDefault((UICategoryData p) => p.CategoryName == strCategoryName);
				if (uICategoryData == null)
				{
					continue;
				}
				value = ((IUserStringData)Param)["GetTrainImageNum_" + j];
				num2 = Convert.ToUInt32(value);
				empty2 = ((IUserStringData)Param)["GetTrainInstanceNum_" + j];
				num3 = Convert.ToUInt32(empty2);
				goto IL_0294;
			}
			catch (VmException ex2)
			{
				LogHelper.moduleLog.Error($"Get category {j} information error: {ErrorCodeInfo.GetErrorInfo(ex2.errorCode)}");
				goto IL_0294;
			}
			catch (Exception ex3)
			{
				int result = 0;
				string arg = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex3.Message);
				LogHelper.moduleLog.Error($"Get category {j} information error: {arg}");
				goto IL_0294;
			}
			IL_0294:
			CategoryData item = new CategoryData
			{
				CategoryName = strCategoryName,
				RegisteredNum = Convert.ToUInt32(value),
				RegisteredROINum = Convert.ToUInt32(value)
			};
			CategoryList.Add(item);
		}
	}

	private void SaveModuleData()
	{
		if (Module == null)
		{
			return;
		}
		try
		{
			byte[] value = SerializationUnit.SerializeObject_V2(EdgeLearningData.Instance.SampleImageDataList);
			Module.UserData["SampleImagesData"] = value;
			byte[] value2 = SerializationUnit.SerializeObject(EdgeLearningData.Instance.UICategoryList);
			Module.UserData["UICategoryData"] = value2;
			Module.UserData["IsExportToModel"] = StringUtil.UTF8GetBytesPadZero(EdgeLearningData.Instance.IsExportToModel.ToString());
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error("Save module data error: " + ex.Message);
		}
	}

	private void LoadModuleData()
	{
		if (Module == null)
		{
			return;
		}
		try
		{
			CurModelGUID = 0;
			byte[] bytes = Module.UserData["SampleImagesData"];
			EdgeLearningData.Instance.SampleImageDataList = (ObservableCollection<SampleImageData>)SerializationUnit.DeserializeObject_V2(bytes);
			byte[] bytes2 = Module.UserData["UICategoryData"];
			EdgeLearningData.Instance.UICategoryList = (List<UICategoryData>)SerializationUnit.DeserializeObject(bytes2);
			byte[] array = Module.UserData["IsExportToModel"];
			if (array != null)
			{
				string value = StringUtil.UTF8GetString(array);
				if (bool.TryParse(value, out var result))
				{
					EdgeLearningData.Instance.IsExportToModel = result;
				}
			}
			byte[] array2 = Module.UserData["HasModel"];
			string empty = string.Empty;
			if (array2 != null)
			{
				empty = StringUtil.UTF8GetString(array2);
				if (empty == "True")
				{
					GetModelData();
					IsRegisterOK = true;
				}
				else
				{
					IsRegisterOK = false;
				}
			}
			CurModelGUID = GetHashCode();
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error("Load module data error: " + ex.Message);
		}
	}

	private bool DeleteCategory(CategoryData category)
	{
		if (Param == null)
		{
			LogHelper.moduleLog.Error("DeleteCategory error: Param is null.");
			return false;
		}
		if (CategoryList.Contains(category))
		{
			CategoryList.Remove(category);
		}
		return true;
	}

	private AlgorithmTabRoot GetAlgorithmTabRoot()
	{
		if (ParentNode is NameValueItem nameValueItem)
		{
			return nameValueItem.GetTopBaseNode() as AlgorithmTabRoot;
		}
		return null;
	}

	private void ModelList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				scrollViewer.Focus();
			}
			e.Handled = true;
		}
	}

	private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is FewShotEdgeLearningPanel { CurModelGUID: not 0 } fewShotEdgeLearningPanel) || fewShotEdgeLearningPanel.CurModelGUID == fewShotEdgeLearningPanel.GetHashCode() || fewShotEdgeLearningPanel.Module == null)
		{
			return;
		}
		try
		{
			byte[] array = fewShotEdgeLearningPanel.Module.UserData["HasModel"];
			if (array != null && StringUtil.UTF8GetString(array) == "False")
			{
				fewShotEdgeLearningPanel.IsRegisterOK = false;
				fewShotEdgeLearningPanel.CategoryList.Clear();
			}
			else if (array != null && StringUtil.UTF8GetString(array) == "True")
			{
				fewShotEdgeLearningPanel.CategoryList.Clear();
				fewShotEdgeLearningPanel.GetModelData();
				fewShotEdgeLearningPanel.IsRegisterOK = true;
			}
		}
		catch (VmException ex)
		{
			LogHelper.Error(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
		}
		catch (Exception ex2)
		{
			LogHelper.Error(ex2.Message);
		}
	}
}
