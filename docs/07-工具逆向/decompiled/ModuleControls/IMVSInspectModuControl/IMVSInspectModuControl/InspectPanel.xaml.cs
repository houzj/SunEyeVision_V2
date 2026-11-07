using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using Apps.Data;
using Apps.Data.Common;
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Util.Model;
using Hik.Xml;
using HikPcUI.ImageView;
using ModelCommonHelper;
using OpenCvSharp;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSInspectModuControl;

public partial class InspectPanel : System.Windows.Controls.UserControl, IParentNode, INotifyPropertyChanged, IComponentConnector
{
	private IVmModule _module;

	private IParamsConfig _param;

	private ModuleTrainData _trainData = new ModuleTrainData();

	private List<string> modelTypeOptions = new List<string>
	{
		AppLocalizationService.GetLang("Lang_DirectCreate"),
		AppLocalizationService.GetLang("RunParam_Inherit")
	};

	private int _modelType = 0;

	private int _oldModelType = 0;

	private bool isChange = false;

	public static readonly DependencyProperty ModelImageProperty = DependencyProperty.Register("ModelImage", typeof(ImageSource), typeof(InspectPanel), new PropertyMetadata(null));

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(InspectPanel), new PropertyMetadata(null));

	public ImageSource ModelImage
	{
		get
		{
			return (ImageSource)GetValue(ModelImageProperty);
		}
		set
		{
			SetValue(ModelImageProperty, value);
		}
	}

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

	public List<string> ModelTypeOptions => modelTypeOptions;

	public int ModelType
	{
		get
		{
			return _modelType;
		}
		set
		{
			if (value != _modelType)
			{
				_oldModelType = _modelType;
				_modelType = value;
				if (ParentNode is InspectData inspectData)
				{
					inspectData.CurValueA = value;
				}
				OnPropertyChanged("ModelType");
			}
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

	public InspectPanel()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			LoadModuleData();
			if (TrainData.IsTrainFinish)
			{
				ImageData modelImageData = GetModelImageData();
				if (modelImageData != null)
				{
					ModelImage = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
				}
			}
			int num = 0;
			try
			{
				if (Param != null)
				{
					string s2 = ((IUserStringData)Param)["TrainRoiSource"];
					num = int.Parse(s2);
				}
			}
			catch (VmException ex)
			{
				string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
				text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				LogHelper.moduleLog.Error(text);
			}
			if (_modelType != num)
			{
				ModelType = num;
			}
			else
			{
				isChange = true;
			}
			TrainDataMgr.Instance.ClearAllModelEvent += ClearModel;
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			SaveModuleData();
			TrainDataMgr.Instance.ClearAllModelEvent -= ClearModel;
			_trainData = null;
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
		TrainDataMgr.Instance.CurImageData = ModuleCommonMethodMgr.Instance.GetCurImage(Module);
		if (ModelType == 0 && TrainDataMgr.Instance.CurImageData != null && TrainDataMgr.Instance.CurImageData.PixelFormat != PixelFormats.Gray8)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_UnsupportFormatWarning"), MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		TrainDataMgr.Instance.Module = Module;
		TrainDataMgr.Instance.Param = Param;
		TrainDataMgr.Instance.IsEnglish = AppLocalizationService.GetLang("Language") == "English";
		ModelTrainDialog modelTrainDialog = new ModelTrainDialog();
		TrainDataMgr.Instance.Owner = modelTrainDialog;
		modelTrainDialog.CurModelType = (ModelType)ModelType;
		modelTrainDialog.ParentNode = ParentNode;
		modelTrainDialog.Init(TrainData);
		CreatModel.Instance.IsCreatingModel = true;
		if (modelTrainDialog.ShowDialog() == true)
		{
			_trainData = TrainDataMgr.Instance.TrainData;
			if (_trainData.IsTrainFinish)
			{
				ImageData modelImageData = GetModelImageData();
				if (modelImageData != null)
				{
					ModelImage = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
				}
			}
			else
			{
				ModelImage = null;
			}
			(ParentNode as IRunService)?.PreExecuteRunModel();
		}
		CreatModel.Instance.IsCreatingModel = false;
		TrainDataMgr.Instance.ClearTempData();
		GC.Collect();
	}

	private void LoadModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param == null)
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
			openFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.dcm";
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
		ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(text), Module?.ModuleDir);
		HikCoverRoiParameter trainRoiParam = _trainData.TrainRoiParam;
		byte[] array = null;
		try
		{
			array = EncryptionAndDecryptionHelper.GetLibFile(text);
			if (array == null)
			{
				return;
			}
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
		try
		{
			_trainData.TrainRoiParam = null;
			TrainData.loadXml(array);
			if ((((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData == null || ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData.Count == 0) && TrainData._pointsData != null)
			{
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData = (ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>)SerializationUnit.DeserializeObject(TrainData._pointsData);
			}
			try
			{
				if (TrainData._matchModelData != null && !TrainData.FeatureMatchData.IsSameVersion())
				{
					TrainData.FeatureMatchData.CreateModel();
					TrainData._matchModelData = ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).ModelData;
				}
			}
			catch (Exception ex3)
			{
				LogHelper.moduleLog.Error($"Rebuild match model error: {ex3.Message}");
			}
			Param.TimeOut = 2147483647u;
			int modelType = TrainData.ModelType;
			((IUserStringData)Param)["TrainRoiSource"] = modelType.ToString();
			if (TrainData._matchModelData != null)
			{
				((IUserBytesData)Param)["AddMatchModelData"] = TrainData._matchModelData;
			}
			if (modelType == 0)
			{
				((IUserStringData)Param)["SkipHpMatch"] = ((TrainData._matchModelData != null) ? "False" : "True");
			}
			((IUserBytesData)Param)["AddInspModelData"] = TrainData._inspModelData;
			((IUserBytesData)Param)["SetFixPoint"] = TrainDataMgr.Instance.GetPointBuffer((float)((FeaturepatMatchDataBase)TrainData.FeatureMatchData).FixPointX, (float)((FeaturepatMatchDataBase)TrainData.FeatureMatchData).FixPointY);
			if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData != null && modelType == 0)
			{
				((IUserBytesData)Param)["SetROIData"] = ModuleCommonMethodMgr.Instance.GetRoiBuffer(TrainData.TrainRoiParam, (float)((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Width, (float)((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Height);
			}
			if (_modelType != modelType)
			{
				isChange = false;
				ModelType = modelType;
			}
			Param.TimeOut = 3000u;
			ImageData modelImageData = GetModelImageData();
			if (modelImageData == null)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_GetImageDataError"));
			}
			ModelImage = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
			if (ModelType == 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadSuccess"), MessageIcons.Ok);
			}
			else
			{
				HikMessageBox.Show(string.Format("{0}{1}{2}", AppLocalizationService.GetLang("Lang_LoadSuccess"), Environment.NewLine, AppLocalizationService.GetLang("Lang_LoadInheritModelTips")), MessageIcons.Ok);
			}
		}
		catch (Exception ex4)
		{
			int result = 0;
			string text3 = AppLocalizationService.GetLang("Lang_LoadFail") + "\r\n(";
			text3 += AppLocalizationService.GetLang("errInfo:");
			string text4 = (int.TryParse(ex4.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex4.Message);
			text3 += text4;
			text3 += ")";
			HikMessageBox.Show(text3, MessageIcons.Error);
			_trainData.TrainRoiParam = trainRoiParam;
		}
		TrainData.ClearModelData();
		GC.Collect();
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
			saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.dcm";
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
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				return;
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			try
			{
				TrainData.PrepareSDKImportData(Param, ModelType == 0);
				string s = TrainData.ToXmlStringWithHead();
				EncryptionAndDecryptionHelper.WriteLibFile(text, Encoding.UTF8.GetBytes(s));
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
				ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(text), Module?.ModuleDir);
			}
			catch (VmException ex2)
			{
				string strMsg = string.Format("{0}{1}{2}{3}", AppLocalizationService.GetLang("Savefailed"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex2.errorCode));
				HikMessageBox.Show(strMsg, MessageIcons.Error);
			}
			catch (Exception ex3)
			{
				int result = 0;
				string text2 = AppLocalizationService.GetLang("Savefailed") + Environment.NewLine;
				text2 += AppLocalizationService.GetLang("errInfo:");
				string text3 = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex3.Message);
				text2 += text3;
				HikMessageBox.Show(text2, MessageIcons.Error);
			}
			TrainData.ClearModelData();
			GC.Collect();
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
		}
		else if (ModelImage != null)
		{
			VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "是否清空已有的训练数据？", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
			{
				ClearModel();
			}
		}
		else
		{
			ClearModel();
		}
	}

	private void ModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!(sender is System.Windows.Controls.ComboBox) || Param == null)
		{
			return;
		}
		if (!isChange)
		{
			isChange = true;
			return;
		}
		if (ModelImage != null)
		{
			VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "是否清空已有的训练数据？", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
			{
				ClearModel();
			}
			else
			{
				isChange = false;
				ModelType = _oldModelType;
			}
		}
		if (isChange)
		{
			try
			{
				((IUserStringData)Param)["TrainRoiSource"] = ModelType.ToString();
			}
			catch (VmException ex)
			{
				string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
				text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				HikMessageBox.Show(text, MessageIcons.Error);
			}
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

	private unsafe ImageData GetModelImageData()
	{
		try
		{
			if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData == null || ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.ImageBuffer == null)
			{
				return null;
			}
			LogHelper.moduleLog.Info("GetModelImageData bytesToMat.");
			Mat val = ImageSaveOpenCvHelper.bytesToMat(((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData);
			int width = ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Width;
			int height = ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Height;
			int num = ((width < height) ? width : height);
			int num2 = num / 100;
			if (num2 < 2)
			{
				num2 = 2;
			}
			LogHelper.moduleLog.Info("GetModelImageData draw rect1.");
			Apps.Data.ShapeDefine.Rect rect = ModuleCommonMethodMgr.Instance.RoiToRect(TrainData.TrainRoiParam, "#0F6AFC");
			if (rect != null)
			{
				rect.StrokeThickness = num2;
				val.DrawRect(rect);
			}
			LogHelper.moduleLog.Info("GetModelImageData draw rect2.");
			Apps.Data.ShapeDefine.Rect rect2 = ModuleCommonMethodMgr.Instance.RoiToRect(TrainData.MatchBox, "#F76900");
			if (rect2 != null)
			{
				rect2.StrokeThickness = num2;
				val.DrawRect(rect2);
			}
			if (TrainData._matchBoxData != null && TrainData._matchBoxData.Length != 0)
			{
				MVBI_BOX_F[] boxesWithNum = MVBI_BOX_F.GetBoxesWithNum(TrainData._matchBoxData);
				if (boxesWithNum != null && boxesWithNum.Length != 0)
				{
					for (int i = 0; i < boxesWithNum.Length; i++)
					{
						Apps.Data.ShapeDefine.Rect rect3 = new Apps.Data.ShapeDefine.Rect();
						rect3.WidthA = boxesWithNum[i].width;
						rect3.HeightA = boxesWithNum[i].height;
						rect3.CenterXA = boxesWithNum[i].center_x;
						rect3.CenterYA = boxesWithNum[i].center_y;
						rect3.AngleA = boxesWithNum[i].angle;
						rect3.ColorA = "#F76900";
						rect3.StrokeThickness = num2;
						val.DrawRect(rect3);
					}
				}
			}
			LogHelper.moduleLog.Info("GetModelImageData create ImageData.");
			ImageData imageData = new ImageData();
			imageData.Width = width;
			imageData.Height = height;
			imageData.PixelFormat = PixelFormats.Rgb24;
			LogHelper.moduleLog.Info("GetModelImageData get image data start.");
			byte* dataPointer = val.DataPointer;
			if (dataPointer == null)
			{
				return null;
			}
			int num3 = imageData.Width * imageData.Height;
			int num4 = num3 * 3;
			imageData.ImageBuffer = new byte[num4];
			if (imageData.ImageBuffer == null)
			{
				return null;
			}
			for (int j = 0; j < num3; j++)
			{
				imageData.ImageBuffer[3 * j] = dataPointer[3 * j + 2];
				imageData.ImageBuffer[3 * j + 1] = dataPointer[3 * j + 1];
				imageData.ImageBuffer[3 * j + 2] = dataPointer[3 * j];
			}
			LogHelper.moduleLog.Info("GetModelImageData get image data end.");
			return imageData;
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error(ex.Message);
			return null;
		}
	}

	private void ClearModel()
	{
		if (Param == null)
		{
			return;
		}
		try
		{
			byte[] value = new byte[1];
			((IUserBytesData)Param)["ClearModelData"] = value;
			_trainData = new ModuleTrainData();
			ModelImage = null;
		}
		catch
		{
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
			string moduleTypeName = Module.ModuleTypeName;
			string key = moduleTypeName + "_ModelData";
			Module.UserData[key] = null;
			string key2 = moduleTypeName + "_TrainData";
			Module.UserData[key2] = null;
			string key3 = moduleTypeName + "_PointsData";
			Module.UserData[key3] = null;
			if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).ModelData != null)
			{
				Module.UserData[key] = ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).ModelData;
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).ModelData = null;
			}
			if (TrainData._imagesData != null)
			{
				TrainData._imagesData = null;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(TrainData.ToXmlStringWithHead(IsShort: true));
			if (bytes != null)
			{
				Module.UserData[key2] = bytes;
			}
			byte[] array = SerializationUnit.SerializeObject(((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData);
			if (array != null)
			{
				Module.UserData[key3] = array;
			}
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error($"SaveModuleData error: {ex.Message}");
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
				if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).ModelData == null)
				{
					string key2 = moduleTypeName + "_ModelData";
					((FeaturepatMatchDataBase)TrainData.FeatureMatchData).ModelData = Module.UserData[key2];
				}
				if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData == null || ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData.Count == 0)
				{
					string key3 = moduleTypeName + "_PointsData";
					byte[] bytes = Module.UserData[key3];
					((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData = (ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>)SerializationUnit.DeserializeObject(bytes);
				}
				if (((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData == null)
				{
					((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error($"LoadModuleData error: {ex.Message}");
		}
	}
}
