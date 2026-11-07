using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
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

namespace IMVSMachineLearningClassifierModuControl;

public partial class MLClassifyPanel : System.Windows.Controls.UserControl, IParentNode, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private IVmModule _module;

	private IParamsConfig _param;

	private ObservableCollection<CategoryData> _CategoryList = new ObservableCollection<CategoryData>();

	private bool _init = false;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(MLClassifyPanel), new PropertyMetadata(null));

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

	public bool IsExistCategory
	{
		get
		{
			if (CategoryList.Count > 0)
			{
				return true;
			}
			return false;
		}
	}

	public double RegisterButtonWidth => (AppLocalizationService.GetLang("Language") == "English") ? 150 : 100;

	public Thickness SampleImageSpace
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return new Thickness(15.0, 3.0, 15.0, 3.0);
			}
			return new Thickness(2.0, 3.0, 2.0, 3.0);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public MLClassifyPanel()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			LoadModuleData();
			if (!_init)
			{
				_init = true;
				QueryAllCategory();
				OnPropertyChanged("IsExistCategory");
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			SaveModuleData();
			try
			{
				if (Param != null)
				{
					((IUserStringData)Param)["RegisterFinish"] = "";
				}
			}
			catch (VmException ex)
			{
				LogHelper.moduleLog.Error(string.Format("Register finish error, {0}{1}", AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex.errorCode)));
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
		MLClassifyMgr.Instance.CurImageData = ModuleCommonMethodMgr.Instance.GetCurImage(Module);
		MLClassifyMgr.Instance.Param = Param;
		MLClassifyMgr.Instance.Module = Module;
		MLClassifyMgr.Instance.IsEnglish = AppLocalizationService.GetLang("Language") == "English";
		MLImageDialog mLImageDialog = new MLImageDialog();
		MLClassifyMgr.Instance.Owner = mLImageDialog;
		mLImageDialog.Init(CategoryList);
		if (mLImageDialog.ShowDialog() != true)
		{
			return;
		}
		CategoryList.Clear();
		foreach (CategoryData item in ((IRegisterClassify)mLImageDialog).Items.OfType<CategoryData>().ToList())
		{
			if (item.RegisteredNum != 0)
			{
				CategoryList.Add(item);
			}
		}
		QueryAllCategory();
		OnPropertyChanged("IsExistCategory");
		(ParentNode as IRunService)?.PreExecuteRunModel();
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
			openFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.bin";
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
			((IUserStringData)Param)["SetModelData"] = text;
		}
		catch (VmException ex)
		{
			string text2 = string.Format("{0}{1}({2}{3})", AppLocalizationService.GetLang("Lang_LoadFail"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex.errorCode));
			HikMessageBox.Show(text2, MessageIcons.Error);
			LogHelper.moduleLog.Error(text2);
			return;
		}
		QueryAllCategory();
		foreach (SampleImageData sampleImageData in MLClassifyMgr.Instance.SampleImageDataList)
		{
			sampleImageData.ImageLabel = "";
			sampleImageData.IsCalibrated = false;
			sampleImageData.IsOKCalibrated = false;
		}
		MLClassifyMgr.Instance.EmptyCategoryList.Clear();
		OnPropertyChanged("Lang_LoadSuccess");
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GroupImportSuccess"), MessageIcons.Ok);
	}

	private void ExportModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null || Module == null)
		{
			return;
		}
		string text = null;
		using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
		{
			saveFileDialog.Title = AppLocalizationService.GetLang("Save Model File");
			saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.bin";
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
			((IUserStringData)Param)["SaveAsGalleryFile"] = text;
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module.ModuleDir);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportSuccess"), MessageIcons.Ok);
		}
		catch (VmException ex2)
		{
			string text2 = AppLocalizationService.GetLang("Lang_ExportFailed") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			text2 += ")";
			HikMessageBox.Show(text2, MessageIcons.Error);
			LogHelper.moduleLog.Error(text2);
		}
	}

	private void ClearModel_Click(object sender, RoutedEventArgs e)
	{
		if (Module == null || Param == null || CategoryList.Count == 0)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_DeleteCategory", "Lang_DeleteCategoryTip", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult != VM.Controls.DialogResult.Yes && dialogResult != VM.Controls.DialogResult.SDKModel)
		{
			return;
		}
		int count = CategoryList.Count;
		for (int i = 0; i < count; i++)
		{
			if (!DeleteCategory(CategoryList[0]))
			{
				break;
			}
		}
		OnPropertyChanged("IsExistCategory");
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
		string[] array2;
		try
		{
			string empty = string.Empty;
			empty = ((IUserStringData)Param)["GetCurClassNum"];
			string[] array = empty.Split(';');
			if (array.Count() != 2)
			{
				LogHelper.Info("strClassNum  value:" + array.Count());
				return;
			}
			int num = Convert.ToInt32(array[0]);
			array2 = array[1].Split(',');
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("Lang_GetCategoryCountFail") + "\r\n(";
			text += AppLocalizationService.GetLang("errInfo:");
			text += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text += ")";
			HikMessageBox.Show(text, MessageIcons.Error);
			LogHelper.moduleLog.Error(text);
			return;
		}
		string value = string.Empty;
		string categoryName = string.Empty;
		string empty2 = string.Empty;
		string empty3 = string.Empty;
		uint registeredNum = 0u;
		ImageSource imageSource = null;
		byte[] array3 = null;
		for (int i = 0; i < array2.Count(); i++)
		{
			imageSource = null;
			array3 = null;
			IntPtr intPtr = IntPtr.Zero;
			bool flag = false;
			string text2 = array2[i];
			try
			{
				value = ((IUserStringData)Param)["GetQueriedIndex_" + text2.ToString()];
				categoryName = ((IUserStringData)Param)["GetQueriedLabel_" + text2.ToString()];
				empty3 = ((IUserStringData)Param)["GetQueriedColor_" + text2.ToString()];
				empty2 = ((IUserStringData)Param)["GetRegisterNum_" + text2.ToString()];
				registeredNum = Convert.ToUInt32(empty2);
				array3 = ((IUserBytesData)Param)["GetImageSample_" + text2.ToString()];
				if (array3 != null && array3.Length != 0)
				{
					int num2 = array3.Length;
					intPtr = Marshal.AllocHGlobal(num2);
					if (intPtr != IntPtr.Zero)
					{
						Marshal.Copy(array3, 0, intPtr, num2);
						imageSource = MLClassifyMgr.Instance.GetRGBImage(intPtr, num2);
					}
				}
				if (imageSource == null)
				{
					LogHelper.moduleLog.Error($"Category {text2} sample image is null.");
					flag = true;
				}
			}
			catch (VmException ex2)
			{
				LogHelper.moduleLog.Error($"Get category {text2} information error: {ErrorCodeInfo.GetErrorInfo(ex2.errorCode)}");
				flag = true;
			}
			catch (Exception ex3)
			{
				int result = 0;
				string arg = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex3.Message);
				LogHelper.moduleLog.Error($"Get category {text2} information error: {arg}");
				flag = true;
			}
			finally
			{
				if (intPtr != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(intPtr);
					intPtr = IntPtr.Zero;
				}
			}
			if (!flag)
			{
				CategoryData item = new CategoryData
				{
					CategoryName = categoryName,
					CategoryIndex = Convert.ToInt32(value),
					RegisteredNum = registeredNum,
					ImageSource = imageSource
				};
				CategoryList.Add(item);
			}
		}
		OnPropertyChanged("IsExistCategory");
	}

	private void SaveModuleData()
	{
		if (Module == null)
		{
			return;
		}
		try
		{
			byte[] value = SerializationUnit.SerializeObject(MLClassifyMgr.Instance.SampleImageDataList);
			Module.UserData["SampleImagesData"] = value;
			byte[] value2 = SerializationUnit.SerializeObject(MLClassifyMgr.Instance.EmptyCategoryList);
			Module.UserData["EmptyCategoryData"] = value2;
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
			byte[] bytes = Module.UserData["SampleImagesData"];
			MLClassifyMgr.Instance.SampleImageDataList = (ObservableCollection<SampleImageData>)SerializationUnit.DeserializeObject(bytes);
			if (MLClassifyMgr.Instance.SampleImageDataList == null)
			{
				MLClassifyMgr.Instance.SampleImageDataList = new ObservableCollection<SampleImageData>();
			}
			byte[] bytes2 = Module.UserData["EmptyCategoryData"];
			MLClassifyMgr.Instance.EmptyCategoryList = (Dictionary<string, string>)SerializationUnit.DeserializeObject(bytes2);
			if (MLClassifyMgr.Instance.EmptyCategoryList == null)
			{
				MLClassifyMgr.Instance.EmptyCategoryList = new Dictionary<string, string>();
			}
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error("Load module data error: " + ex.Message);
		}
	}

	private bool DeleteCategory(CategoryData category)
	{
		try
		{
			if (Param == null)
			{
				return false;
			}
			((IUserStringData)Param)["GalleryDelete"] = category.CategoryIndex.ToString();
			if (CategoryList.Contains(category))
			{
				CategoryList.Remove(category);
			}
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("Lang_DeleteCategoryFail") + "\r\n(";
			text += AppLocalizationService.GetLang("errInfo:");
			text += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text += ")";
			HikMessageBox.Show(text, MessageIcons.Error);
			LogHelper.moduleLog.Error(text + ", name: " + category.CategoryName);
			return false;
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
}
