using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using HikExternalCall.MvbOCR;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSImgStitchCalibModuControl;

public class ImageStitchData : BaseNode, INotifyPropertyChanged
{
	public const int MVBSTITCH_MIN_IMG_NUM = 2;

	public const int MVBSTITCH_MAX_IMG_NUM = 100;

	public const int ERRORCODE_IMG_NOT_ENOUGH = -1;

	public const int ERRORCODE_PIXFORMAT_NOT_CORRECT = -2;

	public const int ERRORCODE_IMG_NOT_READY = -3;

	private object _lock = new object();

	public bool hasModel;

	public float modelError;

	public byte[] Model = null;

	public int Rows;

	public int Columns;

	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

	private StitchImageDataItem SelectedItem;

	private bool isGetingImage;

	public bool imageFixtureEnable;

	private bool isConfigUIEnabled = true;

	private Enumeration CalibBoardType;

	public string strCalibBoardType;

	private Enumeration ModelOptMethodFlag;

	private Integer ModelOptSampleNum;

	private Integer ModelOptXNegative;

	private Integer ModelOptXPositive;

	private Integer ModelOptYNegative;

	private Integer ModelOptYPositive;

	public string strModelOptMethodFlag;

	public string strModelOptSampleNum;

	public string strModelOptXNegative;

	public string strModelOptXPositive;

	public string strModelOptYNegative;

	public string strModelOptYPositive;

	private ImageData PreviewImage;

	public SDKImageStitchData SDKImportData;

	public int RowsA
	{
		get
		{
			return Rows;
		}
		set
		{
			if (Rows != value)
			{
				Rows = value;
				OnPropertyChanged("RowsA");
				OnPropertyChanged("StitchMethod");
			}
		}
	}

	public int ColumnsA
	{
		get
		{
			return Columns;
		}
		set
		{
			if (Columns != value)
			{
				Columns = value;
				OnPropertyChanged("ColumnsA");
				OnPropertyChanged("StitchMethod");
			}
		}
	}

	public int ImageNumber => RowsA * ColumnsA;

	public int PanelRowsAndColumns => (RowsA > ColumnsA) ? RowsA : ColumnsA;

	public ObservableCollection<object> ItemsData => _ItemsData;

	public StitchImageDataItem SelectedItemA
	{
		get
		{
			return SelectedItem;
		}
		set
		{
			if (SelectedItem != value)
			{
				if (SelectedItem != null)
				{
					SelectedItem.IsSelectedA = false;
				}
				SelectedItem = value;
				if (SelectedItem != null)
				{
					SelectedItem.IsSelectedA = true;
				}
				OnPropertyChanged("SelectedItemA");
				OnPropertyChanged("IsItemsDataNotNull");
			}
		}
	}

	public string StitchMethod => AppLocalizationService.GetLang("Lang_ImgLayout") + RowsA + " X " + ColumnsA;

	public string SuccessRate
	{
		get
		{
			int num = 0;
			foreach (StitchImageDataItem itemsDatum in ItemsData)
			{
				if (itemsDatum.IsReadyA)
				{
					num++;
				}
			}
			return AppLocalizationService.GetLang("特征提取成功") + " " + num + " / " + ItemsData.Count;
		}
	}

	public float ModelError
	{
		get
		{
			return modelError;
		}
		set
		{
			if (modelError != value)
			{
				modelError = value;
				OnPropertyChanged("ModelError");
			}
		}
	}

	public bool IsStitchImageDataNotNull => ItemsData.Any((object p) => p is StitchImageDataItem stitchImageDataItem && stitchImageDataItem.ImageData != null);

	public bool IsFixtureImageDataNotNull => ItemsData.Any((object p) => p is StitchImageDataItem stitchImageDataItem && stitchImageDataItem.ActualImageData != null);

	public bool IsGetingImage
	{
		get
		{
			return isGetingImage;
		}
		set
		{
			if (isGetingImage != value)
			{
				isGetingImage = value;
				OnPropertyChanged("IsGetingImage");
			}
		}
	}

	public bool ImageFixtureEnable
	{
		get
		{
			return imageFixtureEnable;
		}
		set
		{
			if (imageFixtureEnable != value)
			{
				imageFixtureEnable = value;
				OnPropertyChanged("ImageFixtureEnable");
			}
		}
	}

	public bool HasModel
	{
		get
		{
			return hasModel;
		}
		set
		{
			if (hasModel != value)
			{
				hasModel = value;
				OnPropertyChanged("HasModel");
			}
		}
	}

	public bool IsConfigUIEnabled
	{
		get
		{
			return isConfigUIEnabled;
		}
		set
		{
			if (isConfigUIEnabled != value)
			{
				isConfigUIEnabled = value;
				OnPropertyChanged("IsConfigUIEnabled");
			}
		}
	}

	public Enumeration CalibBoardTypeA
	{
		get
		{
			if (CalibBoardType == null)
			{
				CalibBoardType = new Enumeration
				{
					DisplayName = "标定板类型",
					Description = "标定板类型",
					AccessMode = "O",
					CurValue = 1,
					DefaultValue = 1
				};
				CalibBoardType.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntry
					{
						Description = "RunParam_Hikvision_II Calibration Board",
						DisplayName = "RunParam_Hikvision_II Calibration Board",
						Value = 1
					},
					new EnumEntry
					{
						Description = "RunParam_Hikvision_I Calibration Board",
						DisplayName = "RunParam_Hikvision_I Calibration Board",
						Value = 2
					},
					new EnumEntry
					{
						Description = "RunParam_Hikvision_IIV2 Calibration Board",
						DisplayName = "RunParam_Hikvision_IIV2 Calibration Board",
						Value = 3
					},
					new EnumEntry
					{
						Description = "RunParam_Hikvision_IV2 Calibration Board",
						DisplayName = "RunParam_Hikvision_IV2 Calibration Board",
						Value = 4
					}
				};
			}
			return CalibBoardType;
		}
	}

	public Enumeration ModelOptMethodFlagA
	{
		get
		{
			if (ModelOptMethodFlag == null)
			{
				ModelOptMethodFlag = new Enumeration
				{
					DisplayName = "RunParam_ModelOptMethodFlag",
					Description = "RunParam_ModelOptMethodFlag",
					AccessMode = "O",
					CurValue = 2,
					DefaultValue = 2
				};
				ModelOptMethodFlag.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntry
					{
						Description = "RunParam_SimpleFast",
						DisplayName = "RunParam_SimpleFast",
						Value = 1
					},
					new EnumEntry
					{
						Description = "RunParam_SimpleComplete",
						DisplayName = "RunParam_SimpleComplete",
						Value = 2
					}
				};
			}
			return ModelOptMethodFlag;
		}
	}

	public Integer ModelOptSampleNumA
	{
		get
		{
			if (ModelOptSampleNum == null)
			{
				ModelOptSampleNum = new Integer
				{
					DisplayName = "RunParam_ModelOptSampleNum",
					Description = "RunParam_ModelOptSampleNum",
					AccessMode = "O",
					CurValue = 100,
					DefaultValue = 100,
					IncValue = 1,
					MinValue = 3,
					MaxValue = 1000,
					SubscribeVisible = false
				};
			}
			return ModelOptSampleNum;
		}
	}

	public Integer ModelOptXNegativeA
	{
		get
		{
			if (ModelOptXNegative == null)
			{
				ModelOptXNegative = new Integer
				{
					DisplayName = "RunParam_ModelOptXNegative",
					Description = "RunParam_ModelOptXNegative",
					AccessMode = "O",
					CurValue = 40,
					DefaultValue = 40,
					IncValue = 1,
					MinValue = 1,
					MaxValue = 2000,
					SubscribeVisible = false
				};
			}
			return ModelOptXNegative;
		}
	}

	public Integer ModelOptXPositiveA
	{
		get
		{
			if (ModelOptXPositive == null)
			{
				ModelOptXPositive = new Integer
				{
					DisplayName = "RunParam_ModelOptXPositive",
					Description = "RunParam_ModelOptXPositive",
					AccessMode = "O",
					CurValue = 40,
					DefaultValue = 40,
					IncValue = 1,
					MinValue = 1,
					MaxValue = 2000,
					SubscribeVisible = false
				};
			}
			return ModelOptXPositive;
		}
	}

	public Integer ModelOptYNegativeA
	{
		get
		{
			if (ModelOptYNegative == null)
			{
				ModelOptYNegative = new Integer
				{
					DisplayName = "RunParam_ModelOptYNegative",
					Description = "RunParam_ModelOptYNegative",
					AccessMode = "O",
					CurValue = 40,
					DefaultValue = 40,
					IncValue = 1,
					MinValue = 1,
					MaxValue = 2000,
					SubscribeVisible = false
				};
			}
			return ModelOptYNegative;
		}
	}

	public Integer ModelOptYPositiveA
	{
		get
		{
			if (ModelOptYPositive == null)
			{
				ModelOptYPositive = new Integer
				{
					DisplayName = "RunParam_ModelOptYPositive",
					Description = "RunParam_ModelOptYPositive",
					AccessMode = "O",
					CurValue = 40,
					DefaultValue = 40,
					IncValue = 1,
					MinValue = 1,
					MaxValue = 2000,
					SubscribeVisible = false
				};
			}
			return ModelOptYPositive;
		}
	}

	public ImageData PreviewImageA
	{
		get
		{
			return PreviewImage;
		}
		set
		{
			if (PreviewImage != value)
			{
				PreviewImage = value;
				OnPropertyChanged("PreviewImageA");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	protected override void BeforSaveBaseNode()
	{
		strCalibBoardType = CalibBoardTypeA.CurValueA.ToString();
		strModelOptMethodFlag = ModelOptMethodFlagA.CurValueA.ToString();
		strModelOptSampleNum = ModelOptSampleNumA.CurValueA.ToString();
		strModelOptXNegative = ModelOptXNegativeA.CurValueA.ToString();
		strModelOptXPositive = ModelOptXPositiveA.CurValueA.ToString();
		strModelOptYNegative = ModelOptYNegativeA.CurValueA.ToString();
		strModelOptYPositive = ModelOptYPositiveA.CurValueA.ToString();
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		int result = 0;
		if (int.TryParse(strCalibBoardType, out result))
		{
			CalibBoardTypeA.CurValueA = result;
		}
		if (int.TryParse(strModelOptMethodFlag, out result))
		{
			ModelOptMethodFlagA.CurValueA = result;
		}
		if (int.TryParse(strModelOptSampleNum, out result))
		{
			ModelOptSampleNumA.CurValueA = result;
		}
		if (int.TryParse(strModelOptXNegative, out result))
		{
			ModelOptXNegativeA.CurValueA = result;
		}
		if (int.TryParse(strModelOptXPositive, out result))
		{
			ModelOptXPositiveA.CurValueA = result;
		}
		if (int.TryParse(strModelOptYNegative, out result))
		{
			ModelOptYNegativeA.CurValueA = result;
		}
		if (int.TryParse(strModelOptYPositive, out result))
		{
			ModelOptYPositiveA.CurValueA = result;
		}
		OnPropertyChanged("HasModel");
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			if (itemsDatum.IsSelectedA)
			{
				SelectedItemA = itemsDatum;
				break;
			}
		}
		SDKImportData = null;
		return this;
	}

	protected override void AfterSaveBaseNode()
	{
		base.AfterSaveBaseNode();
		SDKImportData = null;
	}

	public int CheckImages()
	{
		PixelFormat pixelFormat = PixelFormats.Gray8;
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			if (itemsDatum.IsNull || (ImageFixtureEnable && itemsDatum.IsActualImageNull))
			{
				return -1;
			}
			pixelFormat = itemsDatum.ImageDataA.PixelFormat;
		}
		foreach (StitchImageDataItem itemsDatum2 in ItemsData)
		{
			if (itemsDatum2.ImageDataA.PixelFormat != pixelFormat || (ImageFixtureEnable && itemsDatum2.ActualImageDataA.PixelFormat != pixelFormat))
			{
				return -2;
			}
		}
		foreach (StitchImageDataItem itemsDatum3 in ItemsData)
		{
			if (!itemsDatum3.IsReadyA)
			{
				return -3;
			}
		}
		return 0;
	}

	public MVBSTITCH_MODEL_CREATE_CALIBBOARD_INPUT ConvertToStruct()
	{
		MVBSTITCH_MODEL_CREATE_CALIBBOARD_INPUT result = new MVBSTITCH_MODEL_CREATE_CALIBBOARD_INPUT
		{
			nImgNum = ItemsData.Count,
			nBoardType = CalibBoardTypeA.CurValueA
		};
		result.astImage = new HKA_IMAGE[100];
		result.astStitchConfig = new MVBSTITCH_CALIBBOARD_CFG[100];
		for (int i = 0; i < ItemsData.Count; i++)
		{
			result.astImage[i] = (ItemsData[i] as StitchImageDataItem).GetImageDataStruct();
			result.astStitchConfig[i] = (ItemsData[i] as StitchImageDataItem).GetParamConfigStruct();
		}
		return result;
	}

	public int SetImageParam(IParamsConfig paramsConfig)
	{
		int result = -1;
		lock (_lock)
		{
			MVBSTITCH_MODEL_CREATE_CALIBBOARD_INPUT structure = ConvertToStruct();
			int num = Marshal.SizeOf(structure);
			byte[] array = new byte[num];
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				intPtr = Marshal.AllocHGlobal(num);
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
				Marshal.Copy(intPtr, array, 0, num);
				((IUserBytesData)paramsConfig)["InputModelData"] = array;
				((IUserStringData)paramsConfig)["ModelOptMethodFlag"] = ModelOptMethodFlagA.CurValueA.ToString();
				((IUserStringData)paramsConfig)["ModelOptSampleNum"] = ModelOptSampleNumA.CurValueA.ToString();
				((IUserStringData)paramsConfig)["ModelOptXNegative"] = ModelOptXNegativeA.CurValueA.ToString();
				((IUserStringData)paramsConfig)["ModelOptXPositive"] = ModelOptXPositiveA.CurValueA.ToString();
				((IUserStringData)paramsConfig)["ModelOptYNegative"] = ModelOptYNegativeA.CurValueA.ToString();
				((IUserStringData)paramsConfig)["ModelOptYPositive"] = ModelOptYPositiveA.CurValueA.ToString();
				result = 0;
			}
			catch (Exception ex)
			{
				int result2 = 0;
				int.TryParse(ex.Message, out result2);
				result = result2;
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(result2), MessageIcons.Error, isTranslate: true);
			}
			finally
			{
				if (intPtr != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(intPtr);
					intPtr = IntPtr.Zero;
				}
			}
		}
		return result;
	}

	public int GetFeatureData(IParamsConfig paramsConfig)
	{
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			string key = "OutputCornersImg-" + ItemsData.IndexOf(itemsDatum);
			string key2 = "OutputCornersObj-" + ItemsData.IndexOf(itemsDatum);
			string key3 = "OutputCornersNum-" + ItemsData.IndexOf(itemsDatum);
			byte[] array = null;
			byte[] array2 = null;
			string text = null;
			try
			{
				array = ((IUserBytesData)paramsConfig)[key];
				array2 = ((IUserBytesData)paramsConfig)[key2];
				text = ((IUserStringData)paramsConfig)[key3];
			}
			catch (Exception)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FeatureDataGetFail"), MessageIcons.Error, isTranslate: true);
				return -1;
			}
			itemsDatum.CornersImg = array;
			itemsDatum.CornersObj = array2;
			itemsDatum.CornersNum = text;
		}
		return 0;
	}

	public int SetFeatureCommand(IParamsConfig paramsConfig)
	{
		for (int i = 0; i < ItemsData.Count; i++)
		{
			try
			{
				string key = "ExtractFeature-" + i;
				paramsConfig.TimeOut = 10000u;
				((IUserBytesData)paramsConfig)[key] = new byte[1];
				paramsConfig.TimeOut = 3000u;
			}
			catch (Exception)
			{
				return -1;
			}
		}
		return 0;
	}

	public int SetFeatureData(IParamsConfig paramsConfig)
	{
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			int num = ItemsData.IndexOf(itemsDatum);
			string key = "InputCornersImg-" + num;
			string key2 = "InputCornersObj-" + num;
			string key3 = "InputCornersNum-" + num;
			string key4 = "ImageStatus-" + num;
			byte[] cornersImg = itemsDatum.CornersImg;
			byte[] cornersObj = itemsDatum.CornersObj;
			string cornersNum = itemsDatum.CornersNum;
			string value = (itemsDatum.IsReadyA ? 1 : 0).ToString();
			try
			{
				((IUserBytesData)paramsConfig)[key] = cornersImg;
				((IUserBytesData)paramsConfig)[key2] = cornersObj;
				((IUserStringData)paramsConfig)[key3] = cornersNum;
				((IUserStringData)paramsConfig)[key4] = value;
			}
			catch (Exception)
			{
				return -1;
			}
		}
		return 0;
	}

	public int SetImageBuffer(IParamsConfig paramsConfig)
	{
		int result = -1;
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			string key = "InputImage-" + ItemsData.IndexOf(itemsDatum);
			byte[] array = new byte[itemsDatum.ImageDataA.ImageBuffer.Length];
			itemsDatum.ImageDataA.ImageBuffer.CopyTo(array, 0);
			try
			{
				((IUserBytesData)paramsConfig)[key] = array;
			}
			catch (Exception)
			{
				return -1;
			}
			result = 0;
		}
		return result;
	}

	public async Task ExtractFeatureAsync(IParamsConfig paramsConfig)
	{
		try
		{
			IsConfigUIEnabled = false;
			ResetImageIsReady();
			if (SetImageParam(paramsConfig) != 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamSetFail"), MessageIcons.Error, isTranslate: true);
				return;
			}
			((IUserStringData)paramsConfig)["ImageFixtureEnable"] = (ImageFixtureEnable ? "1" : "0");
			foreach (StitchImageDataItem item in ItemsData)
			{
				int index = ItemsData.IndexOf(item);
				string strParam1 = "InputImage-" + index;
				string strParam2 = "ExtractFeature-" + index;
				string strParam3 = "ImageStatus-" + index;
				byte[] bytes = new byte[item.ImageDataA.ImageBuffer.Length];
				item.ImageDataA.ImageBuffer.CopyTo(bytes, 0);
				string res = "";
				if (await Task.Run(() =>
				{
					try
					{
						((IUserBytesData)paramsConfig)[strParam1] = bytes;
						if (ImageFixtureEnable)
						{
							string key = "InputOptImage-" + index;
							string key2 = "OffsetX-" + index;
							string key3 = "OffsetY-" + index;
							byte[] array = new byte[item.ActualImageDataA.ImageBuffer.Length];
							item.ActualImageDataA.ImageBuffer.CopyTo(array, 0);
							((IUserBytesData)paramsConfig)[key] = array;
							((IUserStringData)paramsConfig)[key2] = item.OffsetXA.CurValueA.ToString();
							((IUserStringData)paramsConfig)[key3] = item.OffsetYA.CurValueA.ToString();
						}
						paramsConfig.TimeOut = 10000u;
						((IUserBytesData)paramsConfig)[strParam2] = new byte[1];
						paramsConfig.TimeOut = 3000u;
						res = ((IUserStringData)paramsConfig)[strParam3];
					}
					catch (Exception)
					{
						return false;
					}
					return true;
				}))
				{
					char[] chs = res.ToArray();
					int result = 0;
					if (chs.Length != 0 && int.TryParse(chs[0].ToString(), out result))
					{
						if (result == 1)
						{
							item.IsReadyA = true;
						}
						else
						{
							item.IsReadyA = false;
						}
					}
					continue;
				}
				OnPropertyChanged("SuccessRate");
				HikMessageBox.Show(AppLocalizationService.GetLang("提取特征失败"), MessageIcons.Error, isTranslate: true);
				return;
			}
			GetFeatureData(paramsConfig);
			OnPropertyChanged("SuccessRate");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FeatureExtractionEnd"), MessageIcons.Ok, isTranslate: true);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			HikMessageBox.Show(ex2.Message, MessageIcons.Error, isTranslate: true);
		}
		finally
		{
			IsConfigUIEnabled = true;
		}
	}

	private void ResetImageIsReady()
	{
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			itemsDatum.IsReadyA = false;
		}
	}

	public bool CreateModel(IParamsConfig paramsConfig)
	{
		try
		{
			((IUserStringData)paramsConfig)["CreateModel"] = "";
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("创建模型失败"), ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageIcons.Error, isTranslate: true);
			return false;
		}
		catch (Exception)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("创建模型失败"), MessageIcons.Error, isTranslate: true);
			return false;
		}
		string text = null;
		float result = 0f;
		try
		{
			text = ((IUserStringData)paramsConfig)["ModelError"];
			if (!float.TryParse(text, out result))
			{
				throw new Exception();
			}
		}
		catch (Exception)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetModelErrorFail"), MessageIcons.Error, isTranslate: true);
			return false;
		}
		ModelError = result;
		HasModel = true;
		return true;
	}

	public bool PreView(IParamsConfig paramsConfig, out string errorInfo)
	{
		errorInfo = null;
		try
		{
			paramsConfig.TimeOut = 2147483647u;
			((IUserBytesData)paramsConfig)["Preview"] = new byte[1];
			paramsConfig.TimeOut = 3000u;
			string s = ((IUserStringData)paramsConfig)["PreviewImageWidth"];
			string s2 = ((IUserStringData)paramsConfig)["PreviewImageHeight"];
			byte[] array = ((IUserBytesData)paramsConfig)["PreviewImage"];
			ImageData imageData = new ImageData();
			imageData.Width = int.Parse(s);
			imageData.Height = int.Parse(s2);
			imageData.ImageBuffer = array;
			imageData.PixelFormat = ((imageData.Width * imageData.Height * 3 == array.Length) ? PixelFormats.Rgb24 : PixelFormats.Gray8);
			PreviewImageA = imageData;
			return true;
		}
		catch (Exception ex)
		{
			if (ex is OutOfMemoryException)
			{
				errorInfo = AppLocalizationService.GetLang("程序可用内存不足");
			}
			if (ex is VmException ex2)
			{
				errorInfo = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			return false;
		}
	}

	internal void ClearData(IParamsConfig paramsConfig, bool isItemsClear = false, IVmModule module = null, DeleteOperationType delType = DeleteOperationType.All)
	{
		SelectedItemA = null;
		PreviewImageA = null;
		HasModel = false;
		Model = null;
		ModelError = 0f;
		if (isItemsClear)
		{
			CalibBoardTypeA.CurValueA = CalibBoardTypeA.DefaultValueA;
			ModelOptMethodFlagA.CurValueA = ModelOptMethodFlagA.DefaultValueA;
			ModelOptSampleNumA.CurValueA = ModelOptSampleNumA.DefaultValueA;
			ModelOptXNegativeA.CurValueA = ModelOptXNegativeA.DefaultValueA;
			ModelOptXPositiveA.CurValueA = ModelOptXPositiveA.DefaultValueA;
			ModelOptYNegativeA.CurValueA = ModelOptYNegativeA.DefaultValueA;
			ModelOptYPositiveA.CurValueA = ModelOptYPositiveA.DefaultValueA;
			ItemsData.Clear();
			try
			{
				((IUserBytesData)paramsConfig)["DeleteModel"] = new byte[1];
				if (module != null)
				{
					module.UserData["ImageStitchData"] = null;
				}
				return;
			}
			catch (Exception)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DeleteFail"), MessageIcons.Error, isTranslate: true);
				return;
			}
		}
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			if (delType != DeleteOperationType.OnlyFixtureImage)
			{
				itemsDatum.ImageDataA = null;
				itemsDatum.FilteringEnabledA = true;
				itemsDatum.GrayscaleContrastA.CurValueA = itemsDatum.GrayscaleContrastA.DefaultValueA;
				itemsDatum.SubpixelWindowA.CurValueA = itemsDatum.SubpixelWindowA.DefaultValueA;
				itemsDatum.SubpixelWindowSizeA.CurValueA = itemsDatum.SubpixelWindowSizeA.DefaultValueA;
				itemsDatum.OffsetXA.CurValueA = itemsDatum.OffsetXA.DefaultValueA;
				itemsDatum.OffsetYA.CurValueA = itemsDatum.OffsetYA.DefaultValueA;
				itemsDatum.CornersImg = null;
				itemsDatum.CornersObj = null;
				itemsDatum.CornersNum = null;
			}
			if (delType != DeleteOperationType.OnlyStitchImage)
			{
				itemsDatum.ActualImageDataA = null;
				itemsDatum.OffsetXA.CurValueA = itemsDatum.OffsetXA.DefaultValueA;
				itemsDatum.OffsetYA.CurValueA = itemsDatum.OffsetYA.DefaultValueA;
			}
			itemsDatum.IsReadyA = false;
			itemsDatum.IsSelectedA = false;
		}
		OnPropertyChanged("SuccessRate");
	}

	public static ImageStitchData CopyWithoutBuffer(ImageStitchData curModelData)
	{
		if (curModelData == null)
		{
			return null;
		}
		ImageStitchData imageStitchData = new ImageStitchData();
		imageStitchData.CalibBoardTypeA.CurValue = curModelData.CalibBoardTypeA.CurValue;
		imageStitchData.ModelOptMethodFlagA.CurValue = curModelData.ModelOptMethodFlagA.CurValue;
		imageStitchData.ModelOptSampleNumA.CurValue = curModelData.ModelOptSampleNumA.CurValue;
		imageStitchData.ModelOptXNegativeA.CurValue = curModelData.ModelOptXNegativeA.CurValue;
		imageStitchData.ModelOptXPositiveA.CurValue = curModelData.ModelOptXPositiveA.CurValue;
		imageStitchData.ModelOptYNegativeA.CurValue = curModelData.ModelOptYNegativeA.CurValue;
		imageStitchData.ModelOptYPositiveA.CurValue = curModelData.ModelOptYPositiveA.CurValue;
		imageStitchData.Columns = curModelData.Columns;
		imageStitchData.Rows = curModelData.Rows;
		imageStitchData.hasModel = curModelData.hasModel;
		imageStitchData.modelError = curModelData.modelError;
		imageStitchData.imageFixtureEnable = curModelData.imageFixtureEnable;
		foreach (StitchImageDataItem itemsDatum in curModelData._ItemsData)
		{
			StitchImageDataItem stitchImageDataItem2 = new StitchImageDataItem();
			stitchImageDataItem2.FilteringEnabled = itemsDatum.FilteringEnabled;
			stitchImageDataItem2.GrayscaleContrastA.CurValue = itemsDatum.GrayscaleContrastA.CurValue;
			stitchImageDataItem2.IsReady = itemsDatum.IsReady;
			stitchImageDataItem2.IsSelected = itemsDatum.IsSelected;
			stitchImageDataItem2.SubpixelWindowA.CurValue = itemsDatum.SubpixelWindowA.CurValue;
			stitchImageDataItem2.SubpixelWindowSizeA.CurValue = itemsDatum.SubpixelWindowSizeA.CurValue;
			stitchImageDataItem2.OffsetXA.CurValue = itemsDatum.OffsetXA.CurValue;
			stitchImageDataItem2.OffsetYA.CurValue = itemsDatum.OffsetYA.CurValue;
			stitchImageDataItem2.CornersImg = itemsDatum.CornersImg;
			stitchImageDataItem2.CornersObj = itemsDatum.CornersObj;
			stitchImageDataItem2.CornersNum = itemsDatum.CornersNum;
			if (itemsDatum.ImageData != null)
			{
				stitchImageDataItem2.ImageData = new ImageData();
				stitchImageDataItem2.ImageData.Width = itemsDatum.ImageData.Width;
				stitchImageDataItem2.ImageData.Height = itemsDatum.ImageData.Height;
				stitchImageDataItem2.ImageData.PixelFormat = itemsDatum.ImageData.PixelFormat;
			}
			if (itemsDatum.ActualImageData != null)
			{
				stitchImageDataItem2.ActualImageData = new ImageData();
				stitchImageDataItem2.ActualImageData.Width = itemsDatum.ImageData.Width;
				stitchImageDataItem2.ActualImageData.Height = itemsDatum.ImageData.Height;
				stitchImageDataItem2.ActualImageData.PixelFormat = itemsDatum.ImageData.PixelFormat;
			}
			imageStitchData._ItemsData.Add(stitchImageDataItem2);
		}
		return imageStitchData;
	}

	public void ReplaceAllImageParam()
	{
		if (SelectedItemA == null)
		{
			return;
		}
		foreach (StitchImageDataItem itemsDatum in ItemsData)
		{
			if (itemsDatum != SelectedItemA && itemsDatum.ImageDataA != null)
			{
				itemsDatum.FilteringEnabledA = SelectedItemA.FilteringEnabledA;
				itemsDatum.GrayscaleContrastA.CurValueA = SelectedItemA.GrayscaleContrastA.CurValueA;
				itemsDatum.SubpixelWindowA.CurValueA = SelectedItemA.SubpixelWindowA.CurValueA;
				itemsDatum.SubpixelWindowSizeA.CurValueA = SelectedItemA.SubpixelWindowSizeA.CurValueA;
				itemsDatum.OffsetXA.CurValueA = SelectedItemA.OffsetXA.CurValueA;
				itemsDatum.OffsetYA.CurValueA = SelectedItemA.OffsetYA.CurValueA;
			}
		}
	}

	public void PrepareSDKImportData(IVmModule module)
	{
		SDKImportData = new SDKImageStitchData();
		SDKImportData.UserInterfaceData = new SDKImageStitchUserData();
		SDKImportData.UserInterfaceData.ImageStitchData_ByteArray = module?.UserData["ImageStitchData"];
		SDKImportData.ModuleModelData = new SDKImageStitchModelData();
		SDKImportData.ModuleModelData.ImportModel_ByteArray = ((IUserBytesData)(module?.Params))?["ExportModel"];
		SDKImportData.ModuleModelData.ModelRow = RowsA.ToString();
		SDKImportData.ModuleModelData.ModelCol = ColumnsA.ToString();
		SDK_IMPORT_IMAGE_INFO structure = new SDK_IMPORT_IMAGE_INFO
		{
			nImgNum = ItemsData.Count,
			nImgWidth = new int[100],
			nImgHeight = new int[100]
		};
		for (int i = 0; i < structure.nImgNum; i++)
		{
			structure.nImgWidth[i] = (ItemsData[i] as StitchImageDataItem).ImageDataA.Width;
			structure.nImgHeight[i] = (ItemsData[i] as StitchImageDataItem).ImageDataA.Height;
		}
		int num = Marshal.SizeOf(structure);
		byte[] array = new byte[num];
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			intPtr = Marshal.AllocHGlobal(num);
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
			Marshal.Copy(intPtr, array, 0, num);
			SDKImportData.ModuleModelData.SDKImportImageInfo_ByteArray = array;
		}
		catch (Exception ex)
		{
			throw ex;
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
				intPtr = IntPtr.Zero;
			}
		}
	}
}
