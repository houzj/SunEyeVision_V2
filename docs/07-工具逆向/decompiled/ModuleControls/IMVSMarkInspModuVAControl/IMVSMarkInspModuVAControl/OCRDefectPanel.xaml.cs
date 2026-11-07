using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Util.Model;
using Hik.Xml;
using HikPcUI.ImageView;
using ModelCommonHelper;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMarkInspModuVAControl;

public partial class OCRDefectPanel : System.Windows.Controls.UserControl, IParentNode, IComponentConnector
{
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
			ItemsControl.ItemsSource = null;
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
		OCRTrainDataMgr.Instance.CurImageData = ModuleCommonMethodMgr.Instance.GetCurImage(Module);
		OCRTrainDataMgr.Instance.Module = Module;
		OCRTrainDataMgr.Instance.Param = Param;
		if (OCRTrainDataMgr.Instance.CurImageData != null && OCRTrainDataMgr.Instance.CurImageData.PixelFormat != PixelFormats.Gray8)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_UnsupportFormatWarning"), MessageButtons.Normal, MessageIcons.Warning);
			OCRTrainDataMgr.Instance.CurImageData = null;
		}
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
			(ParentNode as IRunService)?.PreExecuteRunModel();
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
			byte[] libFile = EncryptionAndDecryptionHelper.GetLibFile(text);
			if (libFile == null)
			{
				return;
			}
			((IUserBytesData)Param)["LoadModelData"] = libFile;
			ItemsControl.ItemsSource = GetModelImages(bImport: true);
			string imagePath = TrainData.ImagePath;
			_trainData = new ModuleTrainData();
			TrainData.ImagePath = imagePath;
			TrainData.IsDetectTarget = true;
			TrainData.IsFinePositioning = true;
			TrainData.IsAddMask = true;
			TrainData.IsStatisticsTrain = true;
			string s = ((IUserStringData)Param)["RoughMatchInfoSour"];
			int num = int.Parse(s);
			MVBI_BOX_F[] roiParam = GetRoiParam("GetRoughRoiData");
			ImageData roughImage = GetRoughImage();
			switch (num)
			{
			case 0:
				if (roiParam != null)
				{
					TrainData.CreateRoiParam = new HikCoverRoiParameter
					{
						Width = roiParam[0].width,
						Height = roiParam[0].height,
						Angle = roiParam[0].angle,
						CenterX = roiParam[0].center_x,
						CenterY = roiParam[0].center_y,
						ForceShowSelected = true
					};
				}
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData = roughImage;
				TrainData.IsCreateModel = true;
				break;
			case 1:
				if (roiParam != null)
				{
					TrainData.InheritRoiParam = new HikCoverRoiParameter
					{
						Width = roiParam[0].width,
						Height = roiParam[0].height,
						Angle = roiParam[0].angle,
						CenterX = roiParam[0].center_x,
						CenterY = roiParam[0].center_y,
						ForceShowSelected = true
					};
				}
				TrainData.InheritImageData = roughImage;
				TrainData.IsInheritModel = true;
				break;
			}
			TrainData.CharRoiData = ((IUserBytesData)Param)["GetModelBoxArr"];
			try
			{
				TrainData.DetectRoiData = ((IUserBytesData)Param)["GetMarkSegmBox"];
			}
			catch (Exception ex)
			{
				int result = 0;
				string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				LogHelper.moduleLog.Error("GetMarkSegmBox error: " + text2);
			}
			try
			{
				TrainData.PositioningMaskData = ((IUserBytesData)Param)["GetExactPatMaskImage"];
			}
			catch (Exception ex2)
			{
				int result2 = 0;
				string text3 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
				LogHelper.moduleLog.Error("GetExactPatMaskImage error: " + text3);
			}
			try
			{
				TrainData.AddMaskData = ((IUserBytesData)Param)["GetMarkInspMask"];
			}
			catch (Exception ex3)
			{
				int result3 = 0;
				string text4 = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result3) ? ErrorCodeInfo.GetErrorInfo(result3) : ex3.Message);
				LogHelper.moduleLog.Error("GetMarkInspMask error: " + text4);
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("加载模板文件成功") + "\r\n" + AppLocalizationService.GetLang("Lang_OCRDefect_LoadTemplateNote"), MessageIcons.Ok);
		}
		catch (VmException ex4)
		{
			string text5 = AppLocalizationService.GetLang("Lang_LoadFail") + "\r\n(";
			text5 += AppLocalizationService.GetLang("errInfo:");
			text5 += ErrorCodeInfo.GetErrorInfo(ex4.errorCode);
			text5 += ")";
			LogHelper.moduleLog.Error(text5);
			HikMessageBox.Show(text5, MessageIcons.Error);
		}
		catch (Exception ex5)
		{
			int result4 = 0;
			string text6 = AppLocalizationService.GetLang("Lang_LoadFail") + "\r\n(";
			text6 += AppLocalizationService.GetLang("errInfo:");
			string text7 = (int.TryParse(ex5.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result4) ? ErrorCodeInfo.GetErrorInfo(result4) : ex5.Message);
			text6 += text7;
			text6 += ")";
			LogHelper.moduleLog.Error(text6);
			HikMessageBox.Show(text6, MessageIcons.Error);
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
				byte[] array = ((IUserBytesData)Param)["SaveModelData"];
				EncryptionAndDecryptionHelper.WriteLibFile(text, array);
				HikMessageBox.Show(AppLocalizationService.GetLang("导出模板文件成功"), MessageIcons.Ok);
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module?.ModuleDir);
			}
			catch (VmException ex2)
			{
				string strMsg2 = string.Format("{0}{1}{2}{3}", AppLocalizationService.GetLang("Savefailed"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), ErrorCodeInfo.GetErrorInfo(ex2.errorCode));
				HikMessageBox.Show(strMsg2, MessageIcons.Error);
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

	private ObservableCollection<ModelImage> GetModelImages(bool bImport = false)
	{
		TrainData.ModelImageCount = 0;
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
				int num = array.Length;
				IntPtr intPtr = Marshal.AllocHGlobal(num);
				if (intPtr == IntPtr.Zero)
				{
					return null;
				}
				try
				{
					Marshal.Copy(array, 0, intPtr, num);
					ObservableCollection<ModelImage> moduleImages = ModelImage.GetModuleImages(intPtr, num);
					TrainData.ModelImageCount = moduleImages.Count;
					Marshal.FreeHGlobal(intPtr);
					return moduleImages;
				}
				catch (Exception ex)
				{
					Marshal.FreeHGlobal(intPtr);
					throw ex;
				}
			}
		}
		catch (Exception ex2)
		{
			int result = 0;
			string text = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
			LogHelper.moduleLog.Error("GetModelImages error: " + text);
			if (bImport)
			{
				throw ex2;
			}
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
			Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: true);
			string moduleTypeName = Module.ModuleTypeName;
			string key = moduleTypeName + "_TrainData";
			Module.UserData[key] = null;
			string key2 = moduleTypeName + "_PointsData";
			Module.UserData[key2] = null;
			string key3 = moduleTypeName + "_FlagsData";
			Module.UserData[key3] = null;
			string key4 = moduleTypeName + "_TrainData_CreatImageBuffer";
			Module.UserData[key4] = null;
			string key5 = moduleTypeName + "_TrainData_InheritImageBuffer";
			Module.UserData[key5] = null;
			byte[] array = null;
			byte[] array2 = null;
			if (((FeaturepatMatchDataBase)(TrainData.FeatureMatchData?)).CurImageData?.Buffer != null)
			{
				array = ((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Buffer;
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Buffer = null;
			}
			if (TrainData.InheritImageData?.Buffer != null)
			{
				array2 = TrainData.InheritImageData.Buffer;
				TrainData.InheritImageData.Buffer = null;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(TrainData.ToXmlStringWithHead(IsShort: true));
			if (bytes != null)
			{
				Module.UserData[key] = bytes;
			}
			if (array != null)
			{
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Buffer = array;
				Module.UserData[key4] = array;
			}
			if (array2 != null)
			{
				TrainData.InheritImageData.Buffer = array2;
				Module.UserData[key5] = array2;
			}
			byte[] array3 = SerializationUnit.SerializeObject(((FeaturepatMatchDataBase)TrainData.FeatureMatchData).PointsData);
			if (array3 != null)
			{
				Module.UserData[key2] = array3;
			}
			byte[] array4 = SerializationUnit.SerializeObject(TrainData.TrainFlagList);
			if (array4 != null)
			{
				Module.UserData[key3] = array4;
			}
		}
		catch
		{
		}
		finally
		{
			Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: false);
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
			string key2 = moduleTypeName + "_TrainData_CreatImageBuffer";
			string text = moduleTypeName + "_TrainData_InheritImageBuffer";
			byte[] array = Module.UserData[key];
			if (array != null)
			{
				TrainData.loadXml(array);
				byte[] array2 = Module.UserData[key2];
				byte[] array3 = Module.UserData[key2];
				if (array2 != null && ((FeaturepatMatchDataBase)(TrainData.FeatureMatchData?)).CurImageData != null)
				{
					((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData.Buffer = array2;
				}
				if (array3 != null && TrainData.InheritImageData != null)
				{
					TrainData.InheritImageData.Buffer = array3;
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
				if (TrainData.TrainFlagList == null || TrainData.TrainFlagList.Count == 0)
				{
					string key4 = moduleTypeName + "_FlagsData";
					byte[] bytes2 = Module.UserData[key4];
					TrainData.TrainFlagList = (List<bool>)SerializationUnit.DeserializeObject(bytes2);
				}
				if (TrainData.TrainFlagList == null)
				{
					TrainData.TrainFlagList = new List<bool>();
				}
				return;
			}
			byte[] array4 = Module.UserData["SDKImportDataFlag"];
			if (array4 == null || array4.Length == 0)
			{
				return;
			}
			byte b = array4[0];
			if (b != Convert.ToByte(1))
			{
				return;
			}
			string imagePath = TrainData.ImagePath;
			_trainData = new ModuleTrainData();
			TrainData.ImagePath = imagePath;
			TrainData.IsDetectTarget = true;
			TrainData.IsFinePositioning = true;
			TrainData.IsAddMask = true;
			TrainData.IsStatisticsTrain = true;
			string s = ((IUserStringData)Param)["RoughMatchInfoSour"];
			int num = int.Parse(s);
			MVBI_BOX_F[] roiParam = GetRoiParam("GetRoughRoiData");
			ImageData roughImage = GetRoughImage();
			switch (num)
			{
			case 0:
				if (roiParam != null)
				{
					TrainData.CreateRoiParam = new HikCoverRoiParameter
					{
						Width = roiParam[0].width,
						Height = roiParam[0].height,
						Angle = roiParam[0].angle,
						CenterX = roiParam[0].center_x,
						CenterY = roiParam[0].center_y,
						ForceShowSelected = true
					};
				}
				((FeaturepatMatchDataBase)TrainData.FeatureMatchData).CurImageData = roughImage;
				TrainData.IsCreateModel = true;
				break;
			case 1:
				if (roiParam != null)
				{
					TrainData.InheritRoiParam = new HikCoverRoiParameter
					{
						Width = roiParam[0].width,
						Height = roiParam[0].height,
						Angle = roiParam[0].angle,
						CenterX = roiParam[0].center_x,
						CenterY = roiParam[0].center_y,
						ForceShowSelected = true
					};
				}
				TrainData.InheritImageData = roughImage;
				TrainData.IsInheritModel = true;
				break;
			}
			TrainData.CharRoiData = ((IUserBytesData)Param)["GetModelBoxArr"];
			try
			{
				TrainData.DetectRoiData = ((IUserBytesData)Param)["GetMarkSegmBox"];
			}
			catch (Exception ex)
			{
				int result = 0;
				string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				LogHelper.moduleLog.Error("GetMarkSegmBox error: " + text2);
			}
			try
			{
				TrainData.PositioningMaskData = ((IUserBytesData)Param)["GetExactPatMaskImage"];
			}
			catch (Exception ex2)
			{
				int result2 = 0;
				string text3 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
				LogHelper.moduleLog.Error("GetExactPatMaskImage error: " + text3);
			}
			try
			{
				TrainData.AddMaskData = ((IUserBytesData)Param)["GetMarkInspMask"];
			}
			catch (Exception ex3)
			{
				int result3 = 0;
				string text4 = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result3) ? ErrorCodeInfo.GetErrorInfo(result3) : ex3.Message);
				LogHelper.moduleLog.Error("GetMarkInspMask error: " + text4);
			}
			byte[] value = new byte[1] { Convert.ToByte(0) };
			Module.UserData["SDKImportDataFlag"] = value;
		}
		catch (VmException ex4)
		{
			string text5 = "LoadModuleData error! (";
			text5 += AppLocalizationService.GetLang("errInfo:");
			text5 += ErrorCodeInfo.GetErrorInfo(ex4.errorCode);
			text5 += ")";
			LogHelper.moduleLog.Error(text5);
		}
		catch (Exception ex5)
		{
			int result4 = 0;
			string text6 = "LoadModuleData error! (";
			text6 += AppLocalizationService.GetLang("errInfo:");
			string text7 = (int.TryParse(ex5.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result4) ? ErrorCodeInfo.GetErrorInfo(result4) : ex5.Message);
			text6 += text7;
			text6 += ")";
			LogHelper.moduleLog.Error(text6);
		}
	}

	private unsafe ImageData GetRoughImage()
	{
		ImageData imageData = null;
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)Param)["GetRoughMatchImage"];
			if (array != null && array.Length != 0)
			{
				IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				byte* ptr = (byte*)intPtr.ToPointer();
				int num = 0;
				int num2 = *(int*)(ptr + num);
				num += 4;
				int num3 = *(int*)(ptr + num);
				num += 4;
				int num4 = num2 * num3;
				if (num4 > 0)
				{
					imageData = new ImageData();
					imageData.ImageBuffer = new byte[num4];
					Marshal.Copy(intPtr + num, imageData.ImageBuffer, 0, num4);
					imageData.Height = num3;
					imageData.Width = num2;
					imageData.PixelFormat = PixelFormats.Gray8;
				}
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			LogHelper.moduleLog.Error("GetRoughImage error: " + text);
		}
		return imageData;
	}
}
