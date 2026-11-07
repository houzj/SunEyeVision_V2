using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Hik.Util.Model;
using Hik.Xml;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMarkInspModuControl;

public partial class OCRDefectPanel : System.Windows.Controls.UserControl, IParentNode, IComponentConnector
{
	private int _modelImageCount = 0;

	private ModuleTrainData _trainData = new ModuleTrainData();

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(OCRDefectPanel), new PropertyMetadata(null));

	private IVmModule _module;

	private IParamsConfig _param;

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

	public ModuleTrainData TrainData => _trainData;

	public OCRDefectPanel()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			LoadModuleData();
			ItemsControl.ItemsSource = GetModelImages();
			OCRTrainDataMgr.Instance.ClearModelEvent += OnClearModel;
			OCRTrainDataMgr.Instance.EmptyModelEvent += OnEmptyModel;
			OCRTrainDataMgr.Instance.ClearAllModelEvent += ClearModel;
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			SaveModuleData();
			OCRTrainDataMgr.Instance.ClearModelEvent -= OnClearModel;
			OCRTrainDataMgr.Instance.EmptyModelEvent -= OnEmptyModel;
			OCRTrainDataMgr.Instance.ClearAllModelEvent -= ClearModel;
		};
	}

	private void ModelTrain_Click(object sender, RoutedEventArgs e)
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
		ImageData curImage = ModuleCommonMethodMgr.Instance.GetCurImage(Module);
		OCRTrainDataMgr.Instance.Module = Module;
		OCRTrainDataMgr.Instance.Param = Param;
		if (curImage == null)
		{
			OCRTrainDataMgr.Instance.SynDataProcess(SynDataOccasion.SetNullImage);
		}
		else
		{
			if (curImage.PixelFormat != PixelFormats.Gray8)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_UnsupportFormatWarning"), MessageButtons.Normal, MessageIcons.Warning);
				return;
			}
			OCRTrainDataMgr.Instance.SynDataProcess(SynDataOccasion.SetCurrentImage);
		}
		OCRTrainDataMgr.Instance.CurImageData = OCRTrainDataMgr.Instance.GetCurImage();
		OCRTrainDataMgr.Instance.IsEnglish = AppLocalizationService.GetLang("Language") == "English";
		OCRModelTrainDialog oCRModelTrainDialog = new OCRModelTrainDialog();
		OCRTrainDataMgr.Instance.Owner = oCRModelTrainDialog;
		oCRModelTrainDialog.Init(TrainData);
		CreatModel.Instance.IsCreatingModel = true;
		if (oCRModelTrainDialog.ShowDialog() == true)
		{
			_trainData = OCRTrainDataMgr.Instance.TrainData;
			ObservableCollection<ModelImage> modelImages = GetModelImages();
			ItemsControl.ItemsSource = modelImages;
			CreatModel.Instance.IsCreatingModel = false;
		}
		CreatModel.Instance.IsCreatingModel = false;
		OCRTrainDataMgr.Instance.ClearTempData();
		GC.Collect();
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
		string text;
		using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog())
		{
			openFileDialog.Title = AppLocalizationService.GetLang("Lang_OpenTemplateFile");
			openFileDialog.Filter = AppLocalizationService.GetLang("Lang_TemplateFile") + "|*.mkd";
			text = ConfigurationHelper.INIGetDefaultOpenPath(Module?.ModuleDir);
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
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module?.ModuleDir);
		try
		{
			((IUserStringData)Param)["LoadModelData"] = text;
			ItemsControl.ItemsSource = GetModelImages();
			TrainData.IsSetTrainRegion = true;
			TrainData.IsSetTrainTarget = true;
			TrainData.IsStatisticsTrain = true;
			TrainData.ModelImageCount = _modelImageCount;
			MVBI_BOX_F[] roiParam = GetRoiParam("GetTrainRoiData");
			if (roiParam != null)
			{
				TrainData.TrainRoiParam = new HikCoverRoiParameter
				{
					Width = roiParam[0].width,
					Height = roiParam[0].height,
					Angle = roiParam[0].angle,
					CenterX = roiParam[0].center_x,
					CenterY = roiParam[0].center_y,
					ForceShowSelected = true
				};
			}
			MVBI_BOX_F[] roiParam2 = GetRoiParam("GetRoughRoiData");
			if (roiParam2 != null)
			{
				TrainData.RoughRoiParam = new HikCoverRoiParameter
				{
					Width = roiParam2[0].width,
					Height = roiParam2[0].height,
					Angle = roiParam2[0].angle,
					CenterX = roiParam2[0].center_x,
					CenterY = roiParam2[0].center_y,
					ForceShowSelected = true
				};
			}
			((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData = OCRTrainDataMgr.Instance.GetRoughImage();
			HikMessageBox.Show(AppLocalizationService.GetLang("加载模板文件成功") + "\r\n" + AppLocalizationService.GetLang("Lang_OCRDefect_LoadTemplateNote"), MessageIcons.Ok);
		}
		catch (VmException ex)
		{
			string text2 = AppLocalizationService.GetLang("Lang_LoadFail") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text2 += ")";
			HikMessageBox.Show(text2, MessageIcons.Error);
		}
	}

	private void ExportModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null || Module == null)
		{
			return;
		}
		string text = null;
		bool flag = false;
		string strMsg = string.Empty;
		using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
		{
			saveFileDialog.Title = AppLocalizationService.GetLang("Lang_SaveTemplateFile");
			saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_TemplateFile") + "|*.mkd";
			text = ConfigurationHelper.INIGetDefaultOpenPath(Module?.ModuleDir);
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
			HikMessageBox.Show(AppLocalizationService.GetLang("导出模板文件错误"), strMsg, MessageIcons.Error);
		}
		else if (!string.IsNullOrEmpty(text))
		{
			try
			{
				((IUserStringData)Param)["SaveModelData"] = text;
				HikMessageBox.Show(AppLocalizationService.GetLang("导出模板文件成功"), MessageIcons.Ok);
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module?.ModuleDir);
			}
			catch
			{
			}
		}
	}

	private void ClearModel_Click(object sender, RoutedEventArgs e)
	{
		if (Module == null)
		{
			return;
		}
		if ((Module as IVMRun).IsRunning)
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Warning, isTranslate: true);
			return;
		}
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "是否清空已有的训练数据？", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
		{
			ClearModel();
			_trainData = new ModuleTrainData();
		}
	}

	private void OnClearModel()
	{
		if (Param == null)
		{
			return;
		}
		try
		{
			((IUserStringData)Param)["DeleteExactMatchModel"] = "";
		}
		catch
		{
		}
	}

	private void OnEmptyModel()
	{
		ItemsControl.ItemsSource = null;
	}

	public object GetParentNode()
	{
		throw new NotImplementedException();
	}

	public void SetParentNode(object parentNode)
	{
		throw new NotImplementedException();
	}

	private ObservableCollection<ModelImage> GetModelImages()
	{
		_modelImageCount = 0;
		if (Param == null)
		{
			return null;
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)Param)["GetModelImage"];
			if (array != null && array.Length != 0)
			{
				int dataLen = array.Length;
				IntPtr dataPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				ObservableCollection<ModelImage> moduleImages = ModelImage.GetModuleImages(dataPtr, dataLen);
				_modelImageCount = moduleImages.Count;
				return moduleImages;
			}
		}
		catch
		{
		}
		return null;
	}

	private void ClearModel()
	{
		if (Param == null)
		{
			return;
		}
		try
		{
			((IUserStringData)Param)["DeleteModel"] = "";
			ItemsControl.ItemsSource = null;
			((IUserStringData)Param)["DeleteMarkInspModel"] = "";
		}
		catch
		{
		}
	}

	private MVBI_BOX_F[] GetRoiParam(string key)
	{
		if (Param == null)
		{
			return null;
		}
		byte[] array = null;
		try
		{
			array = ((IUserBytesData)Param)[key];
		}
		catch
		{
			return null;
		}
		return MVBI_BOX_F.GetBoxesWithoutNum(array);
	}

	private void SaveModuleData()
	{
		if (Module == null)
		{
			return;
		}
		try
		{
			string moduleTypeName = Module.ModuleTypeName;
			string key = moduleTypeName + "_TrainData";
			Module.UserData[key] = null;
			string key2 = moduleTypeName + "_PointsData";
			Module.UserData[key2] = null;
			byte[] bytes = Encoding.UTF8.GetBytes(TrainData.ToXmlStringWithHead(IsShort: true));
			if (bytes != null)
			{
				Module.UserData[key] = bytes;
			}
			byte[] array = SerializationUnit.SerializeObject(((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData);
			if (array != null)
			{
				Module.UserData[key2] = array;
			}
		}
		catch
		{
		}
	}

	private void LoadModuleData()
	{
		if (Param == null || Module == null)
		{
			return;
		}
		try
		{
			string moduleTypeName = Module.ModuleTypeName;
			string key = moduleTypeName + "_TrainData";
			byte[] array = Module.UserData[key];
			if (array != null)
			{
				TrainData.loadXml(array);
				if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData == null || ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData.Count == 0)
				{
					string key2 = moduleTypeName + "_PointsData";
					byte[] bytes = Module.UserData[key2];
					((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData = (ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>)SerializationUnit.DeserializeObject(bytes);
				}
				if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData == null)
				{
					((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
				}
				return;
			}
			byte[] array2 = Module.UserData["SDKImportDataFlag"];
			if (array2 == null || array2.Length == 0)
			{
				return;
			}
			byte b = array2[0];
			if (b == Convert.ToByte(1))
			{
				TrainData.IsSetTrainRegion = true;
				TrainData.IsSetTrainTarget = true;
				TrainData.IsStatisticsTrain = true;
				TrainData.ModelImageCount = _modelImageCount;
				MVBI_BOX_F[] roiParam = GetRoiParam("GetTrainRoiData");
				if (roiParam != null)
				{
					TrainData.TrainRoiParam = new HikCoverRoiParameter
					{
						Width = roiParam[0].width,
						Height = roiParam[0].height,
						Angle = roiParam[0].angle,
						CenterX = roiParam[0].center_x,
						CenterY = roiParam[0].center_y,
						ForceShowSelected = true
					};
				}
				MVBI_BOX_F[] roiParam2 = GetRoiParam("GetRoughRoiData");
				if (roiParam2 != null)
				{
					TrainData.RoughRoiParam = new HikCoverRoiParameter
					{
						Width = roiParam2[0].width,
						Height = roiParam2[0].height,
						Angle = roiParam2[0].angle,
						CenterX = roiParam2[0].center_x,
						CenterY = roiParam2[0].center_y,
						ForceShowSelected = true
					};
				}
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData = OCRTrainDataMgr.Instance.GetRoughImage();
				byte[] value = new byte[1] { Convert.ToByte(0) };
				Module.UserData["SDKImportDataFlag"] = value;
			}
		}
		catch
		{
		}
	}
}
