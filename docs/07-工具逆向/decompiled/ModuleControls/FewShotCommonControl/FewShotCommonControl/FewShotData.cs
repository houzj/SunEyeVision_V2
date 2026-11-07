using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Apps.BaseData;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ItemInterface;
using Apps.XmlParser.ParamTab;
using HikExternalCall.Common;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.RenderInterface;
using VMControls.WPF;

namespace FewShotCommonControl;

public class FewShotData : NotifyPropertyChangedBaseNode
{
	private const int COUNT_MAX = 10;

	private const int NAME_LENGTH_MAX = 32;

	private const int LOCAL_IMAGE_WIDTH = 120;

	private const int LOCAL_IMAGE_HEIGHT = 90;

	private string className = "Class1";

	private ImageEntity curImage;

	private IList curROILIst;

	private bool isProcessEnd = false;

	private string showMaskTip = "";

	private bool isShowMask = false;

	private bool hasModel = false;

	private Apps.XmlParser.ParamTab.Boolean angleEnable;

	private Apps.XmlParser.ParamTab.Boolean optimalSizeEnable;

	private Integer optimalSize;

	private bool isItnit = false;

	private bool isSettingParam = false;

	private bool isDetectModu = false;

	private DelegateCommand addCurImageCommand = null;

	private DelegateCommand addCacheImageCommand = null;

	private DelegateCommand addImageCommand = null;

	private DelegateCommand addImageDirCommand = null;

	private DelegateCommand<object> deleteImageCommand = null;

	private DelegateCommand clearImageCommand = null;

	private DelegateCommand<object> deleteInstanceCommand = null;

	private DelegateCommand<string> drawROICommand = null;

	private DelegateCommand roiChangedCommand = null;

	private ICommand _RoiPasteCommand = null;

	private DelegateCommand<string> changeCurImageCommand = null;

	private DelegateCommand matchCommand = null;

	private DelegateCommand closingCommand = null;

	public IVmModule OwnerModule { get; set; }

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

	public ObservableCollection<ImageEntity> ImageList { get; } = new ObservableCollection<ImageEntity>();

	public ObservableCollection<FewShotInstanceImageData> ThumbnailList { get; set; } = new ObservableCollection<FewShotInstanceImageData>();

	public AlgorithmTabRoot ROIRoot { get; set; }

	public IList ROIList { get; set; }

	public string ClassName
	{
		get
		{
			return className;
		}
		set
		{
			if (className != value)
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NameEmpty"), MessageIcons.Warning);
					OnPropertyChanged("ClassName");
				}
				else if (value.Length > 32)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Tab_Length Input") + AppLocalizationService.GetLang("超限"), MessageIcons.Warning);
					OnPropertyChanged("ClassName");
				}
				else if (!CheckClassName(value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CategoryNameIllegalCharTip") + "\n\\ / : * ? \" < > | " + AppLocalizationService.GetLang("Lang_ChineseCharacter"), MessageIcons.Warning);
					OnPropertyChanged("ClassName");
				}
				else
				{
					className = value;
					OnPropertyChanged("ClassName");
				}
			}
		}
	}

	public IList CurROIList
	{
		get
		{
			return curROILIst;
		}
		set
		{
			if (curROILIst != value)
			{
				curROILIst = value;
				OnPropertyChanged("CurROIList");
			}
		}
	}

	public ImageEntity CurImage
	{
		get
		{
			return curImage;
		}
		set
		{
			if (curImage == value)
			{
				return;
			}
			bool flag = false;
			bool flag2 = false;
			if (curImage != null)
			{
				int num = ImageList.IndexOf(curImage);
				if (num >= 0 && ROIRoot.ROISelecters.Count > num)
				{
					((IROIVisible)(ROIRoot.ROISelecters[num] as ROISelecter).NormalizeROIList).IsVisible = false;
					flag = (ROIRoot.ROISelecters[num] as ROISelecter).IsStateForDrawROI;
					flag2 = (ROIRoot.ROISelecters[num] as ROISelecter).DrawROIType == typeof(NormalizedRectROIModel);
					(ROIRoot.ROISelecters[num] as ROISelecter).IsStateForDrawROI = false;
				}
				if (!string.IsNullOrEmpty(curImage.ImagePath))
				{
					curImage.ImageSource = ImageData.GetImageDataFromFile(curImage.ImagePath, 120, 90);
				}
			}
			curImage = value;
			if (curImage != null)
			{
				if (!string.IsNullOrEmpty(curImage.ImagePath))
				{
					curImage.ImageSource = ImageData.GetImageDataFromFile(curImage.ImagePath);
				}
				int num2 = ImageList.IndexOf(curImage);
				if (num2 >= 0 && ROIRoot.ROISelecters.Count > num2)
				{
					CurROIList = (IList)(ROIRoot.ROISelecters[num2] as ROISelecter).NormalizeROIList;
					((IROIVisible)(ROIRoot.ROISelecters[num2] as ROISelecter).NormalizeROIList).IsVisible = true;
					if (flag)
					{
						Apps.XmlParser.ParamTab.RoiType rType = (flag2 ? Apps.XmlParser.ParamTab.RoiType.Box : Apps.XmlParser.ParamTab.RoiType.Polygon);
						(ROIRoot.ROISelecters[num2] as ROISelecter).DrawROI(rType);
					}
				}
			}
			OnPropertyChanged("CurImage");
			OnPropertyChanged("CurIndex");
			OnPropertyChanged("TotalImageCount");
			OnPropertyChanged("AddImageEnable");
			OnPropertyChanged("ROIList");
		}
	}

	public int CurIndex => (CurImage != null) ? (ImageList.IndexOf(CurImage) + 1) : 0;

	public int TotalImageCount => ImageList.Count;

	public bool AddImageEnable => ImageList.Count < 10;

	public bool IsProcessEnd
	{
		get
		{
			return isProcessEnd;
		}
		set
		{
			if (isProcessEnd != value)
			{
				isProcessEnd = value;
				OnPropertyChanged("IsProcessEnd");
			}
		}
	}

	public string ShowMaskTip
	{
		get
		{
			return showMaskTip;
		}
		set
		{
			if (showMaskTip != value)
			{
				showMaskTip = value;
				OnPropertyChanged("ShowMaskTip");
			}
		}
	}

	public bool IsShowMask
	{
		get
		{
			return isShowMask;
		}
		set
		{
			if (isShowMask != value)
			{
				isShowMask = value;
				OnPropertyChanged("IsShowMask");
			}
		}
	}

	public Apps.XmlParser.ParamTab.Boolean AngleEnable
	{
		get
		{
			if (angleEnable == null)
			{
				angleEnable = new Apps.XmlParser.ParamTab.Boolean
				{
					Name = "EnableModelAngle",
					AccessMode = "O",
					CurValue = false,
					DefaultValue = false
				};
				angleEnable.OnCurValueChanged += OnParamCurValueChanged;
			}
			return angleEnable;
		}
	}

	public Apps.XmlParser.ParamTab.Boolean OptimalSizeEnable
	{
		get
		{
			if (optimalSizeEnable == null)
			{
				optimalSizeEnable = new Apps.XmlParser.ParamTab.Boolean
				{
					Name = "OptimalSizeEnable",
					AccessMode = "O",
					CurValue = false,
					DefaultValue = false
				};
				optimalSizeEnable.OnCurValueChanged += OnParamCurValueChangedAsync;
			}
			return optimalSizeEnable;
		}
	}

	public Integer OptimalSize
	{
		get
		{
			if (optimalSize == null)
			{
				optimalSize = new Integer
				{
					Name = "OptimalSize",
					AccessMode = "O",
					SubscribeVisible = false,
					CurValue = 512,
					DefaultValue = 512,
					MinValue = 128,
					MaxValue = 2048,
					IncValue = 1
				};
				optimalSize.OnCurValueChanged += OnParamCurValueChanged;
			}
			return optimalSize;
		}
	}

	public int ParamListHeight
	{
		get
		{
			int num = (OptimalSizeEnable.CurValueA ? 200 : 150);
			return num + AngleEnableParamHeight;
		}
	}

	public int AngleEnableParamHeight => isDetectModu ? 35 : 0;

	public ICommand AddCurImageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = addCurImageCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddCurImageCommandExecute);
				DelegateCommand val3 = val2;
				addCurImageCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand AddCacheImageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = addCacheImageCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddCacheImageCommandExecute);
				DelegateCommand val3 = val2;
				addCacheImageCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand AddImageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = addImageCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddImageCommandExecute);
				DelegateCommand val3 = val2;
				addImageCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand AddImageDirCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = addImageDirCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddImageDirCommandExecute);
				DelegateCommand val3 = val2;
				addImageDirCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand DeleteImageCommand => (ICommand)(deleteImageCommand ?? (deleteImageCommand = new DelegateCommand<object>((Action<object>)DeleteImageCommandExecute)));

	public ICommand ClearImageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = clearImageCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ClearImageCommandExecute);
				DelegateCommand val3 = val2;
				clearImageCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand DeleteInstanceCommand => (ICommand)(deleteInstanceCommand ?? (deleteInstanceCommand = new DelegateCommand<object>((Action<object>)DeleteInstanceCommandExecute)));

	public ICommand DrawROICommand => (ICommand)(drawROICommand ?? (drawROICommand = new DelegateCommand<string>((Action<string>)DrawROICommandExecute)));

	public ICommand ROIChangedCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = roiChangedCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ROIChangedCommandExecute);
				DelegateCommand val3 = val2;
				roiChangedCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand RoiPasteCommand => _RoiPasteCommand ?? (_RoiPasteCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiPasteCommandExecute));

	public ICommand ChangeCurImageCommand => (ICommand)(changeCurImageCommand ?? (changeCurImageCommand = new DelegateCommand<string>((Action<string>)ChangeCurImageCommandExecute)));

	public ICommand MatchCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = matchCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)MatchCommandExecuteAsync);
				DelegateCommand val3 = val2;
				matchCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand ClosingCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = closingCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ClosingCommandExecute);
				DelegateCommand val3 = val2;
				closingCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	private void DoAddImages(string[] fileNames)
	{
		for (int i = 0; i < fileNames.Length && AddImageToList(fileNames[i]); i++)
		{
		}
		UpdateImageListSource();
	}

	private bool AddImageToList(string strResult)
	{
		if (ImageList.Count >= 10)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ImageLimitTip") + "：" + 10, MessageIcons.Error);
			return false;
		}
		if (string.IsNullOrEmpty(strResult))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ImagePathError"), MessageIcons.Error);
			return false;
		}
		if (ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == strResult) == null)
		{
			ImageList.Add(new ImageEntity
			{
				ImagePath = strResult
			});
		}
		return true;
	}

	private bool AddImageByDir(string dir, int folderLayer = 1)
	{
		try
		{
			if (Directory.Exists(dir))
			{
				string[] array = (from x in Directory.GetFileSystemEntries(dir)
					orderby x.Length, x
					select x).ToArray();
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (File.Exists(text))
					{
						if ((!(Path.GetExtension(text).ToUpper() != ".JPG") || !(Path.GetExtension(text).ToUpper() != ".JPEG") || !(Path.GetExtension(text).ToUpper() != ".BMP") || !(Path.GetExtension(text).ToUpper() != ".PNG") || !(Path.GetExtension(text).ToUpper() != ".TIF") || !(Path.GetExtension(text).ToUpper() != ".TIFF")) && !AddImageToList(text))
						{
							return false;
						}
					}
					else if (Directory.Exists(text) && !AddImageByDir(text, folderLayer + 1))
					{
						return false;
					}
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ReadImageFail"), MessageIcons.Error);
			LogHelper.Error("Message:" + ex.Message + "StackTrace:" + ex.StackTrace);
		}
		return true;
	}

	private void UpdateImageListSource()
	{
		Task.Run(() =>
		{
			for (int i = 0; i < ImageList.Count; i++)
			{
				LoadThumbnailImage(i);
			}
			if (CurImage == null && ImageList.Count > 0)
			{
				CurImage = ImageList[0];
			}
			OnPropertyChanged("CurIndex");
			OnPropertyChanged("TotalImageCount");
			OnPropertyChanged("AddImageEnable");
		});
	}

	private void LoadThumbnailImage(int index)
	{
		try
		{
			ImageEntity imageEntity = ImageList[index];
			if (imageEntity != null && imageEntity.ImageSource == null)
			{
				ImageData imageDataFromFile = ImageData.GetImageDataFromFile(imageEntity.ImagePath, 120, 90);
				if (imageDataFromFile != null)
				{
					imageEntity.ImageSource = imageDataFromFile;
				}
				else
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ReadImageFail") + ":" + imageEntity.ImagePath, MessageIcons.Error);
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
	}

	private void PrepareDrawRoi(string roiType)
	{
		int num = ImageList.IndexOf(curImage);
		if (num < 0 || ROIRoot.ROISelecters.Count <= num)
		{
			return;
		}
		ROISelecter rOISelecter = ROIRoot.ROISelecters[num] as ROISelecter;
		if (!(roiType == "Polygon"))
		{
			if (roiType == "Box")
			{
				rOISelecter?.DrawROI(Apps.XmlParser.ParamTab.RoiType.Box);
			}
			else
			{
				rOISelecter.IsStateForDrawROI = false;
			}
		}
		else
		{
			rOISelecter?.DrawROI(Apps.XmlParser.ParamTab.RoiType.Polygon);
		}
	}

	private bool IsHasMark()
	{
		foreach (IROISelecter rOISelecter in ROIRoot.ROISelecters)
		{
			if (((IEnumerable<IROI>)rOISelecter.ROIList).Count((IROI p) =>
			{
				IROISign obj = p as IROISign;
				return obj != null && obj.Sign == true;
			}) > 0)
			{
				return true;
			}
		}
		return false;
	}

	private async Task RegisterAsync(bool isClose = false)
	{
		if (OwnerModule == null)
		{
			return;
		}
		if (!IsHasMark() && !isClose)
		{
			HikMessageBox.Show("至少绘制一个正向ROI", MessageIcons.Warning, isTranslate: true);
			return;
		}
		await Task.Run(() =>
		{
			uint timeOut = OwnerModule.Params.TimeOut;
			OwnerModule.Params.TimeOut = 2147483647u;
			int count = ImageList.Count;
			OwnerModule.UserData["ImageNumber"] = BitConverter.GetBytes(count);
			string text = "";
			bool flag = false;
			List<ImageEntity> list = new List<ImageEntity>();
			foreach (ImageEntity image in ImageList)
			{
				try
				{
					int index = ImageList.IndexOf(image);
					List<byte> list2 = new List<byte>();
					byte[] bytes = BitConverter.GetBytes(1);
					list2.AddRange(bytes);
					ImageData imageData = image.ImageSource;
					if (!string.IsNullOrEmpty(image.ImagePath))
					{
						imageData = ImageData.GetImageDataFromFile(image.ImagePath);
					}
					if (imageData == null)
					{
						throw new Exception(AppLocalizationService.GetLang("Lang_LoadingImageError"));
					}
					if (imageData.DetailedPixelFormat == 0)
					{
						imageData.DetailedPixelFormat = ImageData.GetPixelFormatProtocolCode(imageData.PixelFormat);
					}
					byte[] bytes2 = BitConverter.GetBytes(imageData.Width);
					byte[] bytes3 = BitConverter.GetBytes(imageData.Height);
					byte[] bytes4 = BitConverter.GetBytes(imageData.Buffer.Length);
					byte[] bytes5 = BitConverter.GetBytes(GetAlgorithmPixelFormat(imageData.DetailedPixelFormat));
					list2.AddRange(bytes2);
					list2.AddRange(bytes3);
					list2.AddRange(bytes4);
					list2.AddRange(imageData.Buffer);
					list2.AddRange(bytes5);
					try
					{
						byte[] value = list2.ToArray();
						ObservableRangeCollection<IROI> val = ROIRoot?.ROISelecters[index]?.ROIList;
						GetRoiBuffer(imageData, index, out var roiDatas, out var rectRoiDatas, out var roiTypes);
						OwnerModule.UserData["RegisterImage-" + index] = value;
						OwnerModule.UserData["RegisterROIData-" + index] = roiDatas;
						OwnerModule.UserData["RegisterROIType-" + index] = roiTypes;
						OwnerModule.UserData["RegisterRectROIData-" + index] = rectRoiDatas;
						if (!image.IsCheck && val != null && ((Collection<IROI>)(object)val).Count != 0)
						{
							((IUserBytesData)OwnerModule.Params)["SetRegisterImage"] = value;
							((IUserBytesData)OwnerModule.Params)["SetRegisterROIData"] = roiDatas;
							list.Add(image);
						}
					}
					catch (VmException ex)
					{
						throw new Exception(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
					}
					catch (Exception ex2)
					{
						int result = 0;
						string message = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
						throw new Exception(message);
					}
				}
				catch (Exception ex3)
				{
					text += $"{image.ImageName} ({ex3.Message})\r\n";
				}
				finally
				{
				}
			}
			try
			{
				if (list.Count > 0)
				{
					((IUserStringData)OwnerModule.Params)["SetRegisterClassName"] = ClassName;
					((IUserStringData)OwnerModule.Params)["GalleryRegister"] = "";
					foreach (ImageEntity item in list)
					{
						item.IsCheck = true;
					}
					flag = true;
				}
			}
			catch (Exception ex4)
			{
				string arg = ex4.Message;
				if (ex4 is VmException ex5)
				{
					arg = ErrorCodeInfo.GetErrorInfo(ex5.errorCode);
				}
				text += $"{arg}\r\n";
			}
			if (!string.IsNullOrEmpty(text) && !isClose)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RegisterFail"), text, MessageIcons.Error);
			}
			if (flag)
			{
				UpdateThumbnailList();
				HasModel = ThumbnailList.Count > 0;
			}
			if (isClose)
			{
				IsProcessEnd = true;
			}
			OwnerModule.Params.TimeOut = timeOut;
		});
	}

	private void GetRoiBuffer(ImageData image, int index, out byte[] roiDatas, out byte[] rectRoiDatas, out byte[] roiTypes)
	{
		roiDatas = null;
		rectRoiDatas = null;
		roiTypes = null;
		if (image == null)
		{
			return;
		}
		int width = image.Width;
		int height = image.Height;
		AlgorithmTabRoot rOIRoot = ROIRoot;
		object obj;
		if (rOIRoot == null)
		{
			obj = null;
		}
		else
		{
			IROISelecter iROISelecter = rOIRoot.ROISelecters[index];
			if (iROISelecter == null)
			{
				obj = null;
			}
			else
			{
				ObservableRangeCollection<IROI> rOIList = iROISelecter.ROIList;
				obj = ((rOIList != null) ? ((IEnumerable<IROI>)rOIList).OrderByDescending((IROI p) => (p as IROISign).Sign).ToList() : null);
			}
		}
		List<IROI> list = (List<IROI>)obj;
		if (list == null || list.Count <= 0)
		{
			return;
		}
		IMVS_POLYGON_ROI_LIST structure = new IMVS_POLYGON_ROI_LIST
		{
			nCount = list.Count,
			stPolygonROIs = new IMVS_POLYGON_ROI[10]
		};
		string text = "";
		string text2 = "";
		for (int num = 0; num < list.Count; num++)
		{
			if (list[num] is IPolygonROI)
			{
				ObservableCollection<Point> polygonPoints = (list[num] as IPolygonROI).PolygonPoints;
				structure.stPolygonROIs[num] = GetPolygonStruct(polygonPoints, (list[num] as IROISign).Sign, width, height);
				text += "0";
				continue;
			}
			List<Point> list2 = new List<Point>();
			IRectROI rectROI = list[num] as IRectROI;
			double pointX = rectROI.CenterPoint.X - rectROI.Width / 2.0;
			double pointY = rectROI.CenterPoint.Y + rectROI.Height / 2.0;
			Point item = CalculateRectROIVertex(pointX, pointY, rectROI.CenterPoint, rectROI.Angle);
			list2.Add(item);
			double pointX2 = (float)(rectROI.CenterPoint.X + rectROI.Width / 2.0);
			double pointY2 = (float)(rectROI.CenterPoint.Y + rectROI.Height / 2.0);
			Point item2 = CalculateRectROIVertex(pointX2, pointY2, rectROI.CenterPoint, rectROI.Angle);
			list2.Add(item2);
			double pointX3 = (float)(rectROI.CenterPoint.X + rectROI.Width / 2.0);
			double pointY3 = (float)(rectROI.CenterPoint.Y - rectROI.Height / 2.0);
			Point item3 = CalculateRectROIVertex(pointX3, pointY3, rectROI.CenterPoint, rectROI.Angle);
			list2.Add(item3);
			float num2 = (float)(rectROI.CenterPoint.X - rectROI.Width / 2.0);
			float num3 = (float)(rectROI.CenterPoint.Y - rectROI.Height / 2.0);
			Point item4 = CalculateRectROIVertex(num2, num3, rectROI.CenterPoint, rectROI.Angle);
			list2.Add(item4);
			structure.stPolygonROIs[num] = GetPolygonStruct(list2, (rectROI as IROISign).Sign, width, height);
			text2 = text2 + rectROI.Height + "_";
			text2 = text2 + rectROI.Width + "_";
			text2 = text2 + rectROI.Angle + "_";
			text2 = text2 + rectROI.CenterPoint.X + "_";
			text2 = text2 + rectROI.CenterPoint.Y + ";";
			text += "1";
		}
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			int num4 = Marshal.SizeOf<IMVS_POLYGON_ROI_LIST>();
			intPtr = Marshal.AllocHGlobal(num4);
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
			roiDatas = new byte[num4];
			Marshal.Copy(intPtr, roiDatas, 0, num4);
			rectRoiDatas = Encoding.UTF8.GetBytes(text2);
			roiTypes = Encoding.UTF8.GetBytes(text);
		}
		catch (Exception)
		{
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

	private IMVS_POLYGON_ROI GetPolygonStruct(IList<Point> points, bool? sign, int width, int height)
	{
		int num = points?.Count ?? 0;
		IMVS_POLYGON_ROI result = new IMVS_POLYGON_ROI
		{
			nPointCount = num,
			nRoiType = ((sign != true) ? 1 : 0),
			stPoints = new HKA_POINT_F[32]
		};
		for (int i = 0; i < num; i++)
		{
			result.stPoints[i] = new HKA_POINT_F
			{
				x = (float)points[i].X * (float)width,
				y = (float)points[i].Y * (float)height
			};
		}
		return result;
	}

	private Point CalculateRectROIVertex(double pointX, double pointY, Point centerPoint, double angle)
	{
		double num = Math.Cos(angle * Math.PI / 180.0);
		double num2 = Math.Sin(angle * Math.PI / 180.0);
		double x = num * (pointX - centerPoint.X) - num2 * (pointY - centerPoint.Y) + centerPoint.X;
		double y = num2 * (pointX - centerPoint.X) + num * (pointY - centerPoint.Y) + centerPoint.Y;
		return new Point
		{
			X = x,
			Y = y
		};
	}

	internal void UpdateThumbnailList(bool needPopTip = true)
	{
		try
		{
			string text = ((IUserStringData)OwnerModule.Params)["GetCurClassNum"];
			ClassName = ((IUserStringData)OwnerModule.Params)["GetQueriedLabel"];
			byte[] array = ((IUserBytesData)OwnerModule.Params)["GetImageSample"];
			int num = 0;
			ObservableCollection<FewShotInstanceImageData> observableCollection = new ObservableCollection<FewShotInstanceImageData>();
			while (num < array.Length)
			{
				FewShotInstanceImageData fewShotInstanceImageData = new FewShotInstanceImageData();
				ImageData imageData = new ImageData();
				if (num + 20 > array.Length)
				{
					break;
				}
				imageData.Width = BitConverter.ToInt32(array, num);
				num += 4;
				imageData.Height = BitConverter.ToInt32(array, num);
				num += 4;
				int num2 = BitConverter.ToInt32(array, num);
				num += 4;
				fewShotInstanceImageData.CalssIndex = BitConverter.ToInt32(array, num);
				num += 4;
				fewShotInstanceImageData.ImageIndex = BitConverter.ToInt32(array, num);
				num += 4;
				if (imageData.Width * imageData.Height == num2)
				{
					imageData.PixelFormat = PixelFormats.Gray8;
				}
				else
				{
					if (imageData.Width * imageData.Height * 3 != num2)
					{
						break;
					}
					imageData.PixelFormat = PixelFormats.Rgb24;
				}
				if (num + num2 <= array.Length)
				{
					imageData.Buffer = array.Skip(num).Take(num2).ToArray();
					num += num2;
					fewShotInstanceImageData.ImageData = imageData;
					observableCollection.Add(fewShotInstanceImageData);
					continue;
				}
				break;
			}
			ThumbnailList = observableCollection;
			OnPropertyChanged("ThumbnailList");
		}
		catch (VmException ex)
		{
			if (needPopTip)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetInstanceImageDataError"), AppLocalizationService.GetLang("Lang_ErrCode") + ex.errorCode + AppLocalizationService.GetLang("ErrorInfo: ") + ErrorCodeInfo.GetErrorInfo(ex.errorCode));
			}
		}
		catch (Exception ex2)
		{
			if (needPopTip)
			{
				int result = 0;
				string strMsg = ex2.Message;
				if (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result))
				{
					strMsg = AppLocalizationService.GetLang("Lang_ErrCode") + result + AppLocalizationService.GetLang("ErrorInfo: ") + ErrorCodeInfo.GetErrorInfo(result);
				}
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetInstanceImageDataError"), strMsg);
			}
		}
	}

	private void RefreshThumbnailIndex()
	{
		List<IGrouping<int, FewShotInstanceImageData>> list = (from p in ThumbnailList
			group p by p.CalssIndex).ToList();
		int count = list.Count;
		for (int num = 0; num < count; num++)
		{
			int num2 = 0;
			foreach (FewShotInstanceImageData item in list[num])
			{
				item.ImageIndex = num2;
				num2++;
			}
		}
	}

	private int GetAlgorithmPixelFormat(uint vmPixelFormat)
	{
		int result = 1;
		switch (vmPixelFormat)
		{
		case 35127329u:
			result = 200;
			break;
		case 35127316u:
			result = 201;
			break;
		}
		return result;
	}

	private uint GetVMPixelFormat(int algorithmPixelFormat)
	{
		uint result = 17301505u;
		switch (algorithmPixelFormat)
		{
		case 200:
			result = 35127329u;
			break;
		case 201:
			result = 35127316u;
			break;
		}
		return result;
	}

	private ImageData GetImageDataByBytes(byte[] bytes)
	{
		int num = 4;
		if (num + 12 <= bytes.Length)
		{
			ImageData imageData = new ImageData();
			imageData.Width = BitConverter.ToInt32(bytes, num);
			num += 4;
			imageData.Height = BitConverter.ToInt32(bytes, num);
			num += 4;
			int num2 = BitConverter.ToInt32(bytes, num);
			num += 4;
			if (imageData.Width * imageData.Height == num2)
			{
				imageData.PixelFormat = PixelFormats.Gray8;
			}
			else
			{
				if (imageData.Width * imageData.Height * 3 != num2)
				{
					return null;
				}
				imageData.PixelFormat = PixelFormats.Rgb24;
			}
			if (num + num2 <= bytes.Length)
			{
				imageData.Buffer = new byte[num2];
				Array.Copy(bytes, num, imageData.Buffer, 0, num2);
				num += num2;
				if (num + 4 <= bytes.Length)
				{
					imageData.DetailedPixelFormat = GetVMPixelFormat(BitConverter.ToInt32(bytes, num));
				}
				return imageData;
			}
		}
		return null;
	}

	private void GetROIByBytes(byte[] bytes, ROISelecter roiSelecter, int width, int height, string roiType, byte[] rectROIBytes)
	{
		string text = null;
		if (rectROIBytes != null)
		{
			text = Encoding.UTF8.GetString(rectROIBytes);
		}
		IMVS_POLYGON_ROI_LIST iMVS_POLYGON_ROI_LIST = ExternalCallHelper.BytesToStruct<IMVS_POLYGON_ROI_LIST>(bytes, 0, bytes.Length);
		string[] array = null;
		if (!string.IsNullOrEmpty(text))
		{
			array = text.Split(';');
		}
		int num = 0;
		for (int i = 0; i < iMVS_POLYGON_ROI_LIST.nCount; i++)
		{
			if (roiType[i] == '0')
			{
				NormalizedPolygonROIModel normalizedPolygonROIModel = new NormalizedPolygonROIModel();
				for (int j = 0; j < iMVS_POLYGON_ROI_LIST.stPolygonROIs[i].nPointCount; j++)
				{
					HKA_POINT_F hKA_POINT_F = iMVS_POLYGON_ROI_LIST.stPolygonROIs[i].stPoints[j];
					normalizedPolygonROIModel.PolygonPoints.Add(new Point
					{
						X = hKA_POINT_F.x / (float)width,
						Y = hKA_POINT_F.y / (float)height
					});
				}
				normalizedPolygonROIModel.Sign = iMVS_POLYGON_ROI_LIST.stPolygonROIs[i].nRoiType == 0;
				normalizedPolygonROIModel.UIType = true;
				((Collection<IROI>)(object)roiSelecter.ROIList).Add((IROI)normalizedPolygonROIModel);
			}
			else if (array != null && num < array.Length)
			{
				string[] array2 = array[num].Split('_');
				if (array2.Length != 5)
				{
					LogHelper.Error("Error in obtaining RectROI data");
					break;
				}
				NormalizedRectROIModel normalizedRectROIModel = new NormalizedRectROIModel();
				normalizedRectROIModel.Height = double.Parse(array2[0]);
				normalizedRectROIModel.Width = double.Parse(array2[1]);
				normalizedRectROIModel.Angle = double.Parse(array2[2]);
				double x = double.Parse(array2[3]);
				double y = double.Parse(array2[4]);
				normalizedRectROIModel.CenterPoint = new Point
				{
					X = x,
					Y = y
				};
				normalizedRectROIModel.Sign = iMVS_POLYGON_ROI_LIST.stPolygonROIs[i].nRoiType == 0;
				normalizedRectROIModel.UIType = true;
				((Collection<IROI>)(object)roiSelecter.ROIList).Add((IROI)normalizedRectROIModel);
				num++;
			}
		}
	}

	private void BeforeModelPathChanged(object sender, EventArgs e)
	{
		if (!HasModel)
		{
			return;
		}
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDeleteAllInstance"), MessageButtons.OkOrCancel, MessageIcons.Question) != DialogResult.Ok)
		{
			throw new Exception();
		}
		try
		{
			((IUserStringData)OwnerModule.Params)["GalleryDelete"] = ClassName;
			ThumbnailList.Clear();
			foreach (ImageEntity image in ImageList)
			{
				image.IsCheck = false;
			}
			HasModel = false;
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageButtons.Normal, MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
			HikMessageBox.Show(strMsg, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	private async void OnParamCurValueChangedAsync(object sender, EventArgs e)
	{
		NameValueItem item = default;
		int num;
		if (isItnit && !isSettingParam)
		{
			NameValueItem nameValueItem;
			item = (nameValueItem = sender as NameValueItem);
			num = ((nameValueItem != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		if (num == 0)
		{
			return;
		}
		isSettingParam = true;
		try
		{
			if (sender != OptimalSizeEnable)
			{
				return;
			}
			OnPropertyChanged("ParamListHeight");
			if (HasModel && HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDeleteAllInstance"), MessageButtons.OkOrCancel, MessageIcons.Question) != DialogResult.Ok)
			{
				Apps.XmlParser.ParamTab.Boolean boolean2;
				Apps.XmlParser.ParamTab.Boolean boolean = (boolean2 = sender as Apps.XmlParser.ParamTab.Boolean);
				if (boolean2 != null)
				{
					boolean.CurValueA = !boolean.CurValueA;
					OnPropertyChanged("ParamListHeight");
				}
				return;
			}
			ShowMaskTip = AppLocalizationService.GetLang("正在更新模型分辨率");
			IsShowMask = true;
			((IUserStringData)OwnerModule.Params)[item.NameA] = item.Value;
			if (HasModel)
			{
				DeleteGallery();
			}
			if (IsHasMark())
			{
				await RegisterAsync();
			}
			int size = 0;
			if (int.TryParse(((IUserStringData)OwnerModule.Params)["OptimalSize"], out size))
			{
				OptimalSize.CurValueA = size;
			}
			if (HasModel)
			{
				DeleteGallery();
			}
		}
		catch (VmException ex)
		{
			VmException ex2 = ex;
			HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex2.errorCode));
		}
		catch (Exception ex3)
		{
			Exception ex4 = ex3;
			int err = 0;
			string temp = (int.TryParse(ex4.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out err) ? ErrorCodeInfo.GetErrorInfo(err) : ex4.Message);
			HikMessageBox.Show(temp);
		}
		finally
		{
			isSettingParam = false;
			IsShowMask = false;
		}
	}

	private void OnParamCurValueChanged(object sender, EventArgs e)
	{
		if (!isItnit || isSettingParam || !(sender is NameValueItem nameValueItem))
		{
			return;
		}
		isSettingParam = true;
		try
		{
			if (sender == AngleEnable)
			{
				if (HasModel && HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDeleteAllInstance"), MessageButtons.OkOrCancel, MessageIcons.Question) != DialogResult.Ok)
				{
					if (sender is Apps.XmlParser.ParamTab.Boolean boolean)
					{
						boolean.CurValueA = !boolean.CurValueA;
					}
					return;
				}
				((IUserStringData)OwnerModule.Params)[nameValueItem.NameA] = nameValueItem.Value;
				if (HasModel)
				{
					DeleteGallery();
				}
			}
			else
			{
				((IUserStringData)OwnerModule.Params)[nameValueItem.NameA] = nameValueItem.Value;
			}
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
		}
		catch (Exception ex2)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
			HikMessageBox.Show(strMsg);
		}
		finally
		{
			isSettingParam = false;
		}
	}

	private void DeleteGallery()
	{
		((IUserStringData)OwnerModule.Params)["GalleryDelete"] = ClassName;
		((IUserStringData)OwnerModule.Params)["GalleryDelete"] = "obj_neg";
		ThumbnailList.Clear();
		foreach (ImageEntity image in ImageList)
		{
			image.IsCheck = false;
		}
		HasModel = false;
	}

	private bool CheckClassName(string name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			foreach (char c in name)
			{
				if ((c >= '一' && c <= '龻') || c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
				{
					return false;
				}
			}
		}
		return true;
	}

	private void InitParamList()
	{
		try
		{
			if (OwnerModule != null)
			{
				bool result = false;
				if (isDetectModu && bool.TryParse(((IUserStringData)OwnerModule.Params)["EnableModelAngle"], out result))
				{
					AngleEnable.CurValueA = result;
				}
				if (bool.TryParse(((IUserStringData)OwnerModule.Params)["OptimalSizeEnable"], out result))
				{
					OptimalSizeEnable.CurValueA = result;
					OnPropertyChanged("ParamListHeight");
				}
				int result2 = 0;
				if (int.TryParse(((IUserStringData)OwnerModule.Params)["OptimalSize"], out result2))
				{
					OptimalSize.CurValueA = result2;
				}
			}
		}
		catch (Exception ex)
		{
			string message = ex.Message;
			if (ex is VmException ex2)
			{
				message = $"{ex2.errorCode} {ex2.errorMessage}";
			}
			LogHelper.Error(message);
		}
	}

	public void Init(object node)
	{
		if (node is NameValueItem nameValueItem)
		{
			OwnerModule = (nameValueItem.GetTopBaseNode() as AlgorithmTabRoot)?.Owner as IVmModule;
			IVmModule ownerModule = OwnerModule;
			isDetectModu = ownerModule != null && ownerModule.ModuleTypeName?.Contains("IMVSCnnRegisterDetectModu") == true;
			OnPropertyChanged("AngleEnableParamHeight");
		}
		ROIRoot = new AlgorithmTabRoot();
		for (int i = 0; i < 10; i++)
		{
			ROISelecter rOISelecter = new ROISelecter
			{
				Name = "CustomRoi",
				SelectType = "Mul",
				AccessMode = "O",
				ROISelection = "",
				RoiMaxNum = 10
			};
			rOISelecter.SetParentNode(ROIRoot);
			ROIRoot.ROISelecters.Add(rOISelecter);
			((IROIVisible)rOISelecter.NormalizeROIList).IsVisible = false;
			rOISelecter.OnInniterBusinessFinish();
		}
		ROIList = ROIRoot.ROISelecters.Select((IROISelecter p) => p.ROIList).ToList();
		try
		{
			InitParamList();
			byte[] array = OwnerModule.UserData["ImageNumber"];
			if (array != null)
			{
				int num = BitConverter.ToInt32(array, 0);
				for (int num2 = 0; num2 < num; num2++)
				{
					byte[] array2 = OwnerModule.UserData["RegisterImage-" + num2];
					if (array2 == null)
					{
						continue;
					}
					ImageData imageDataByBytes = GetImageDataByBytes(array2);
					if (imageDataByBytes != null)
					{
						ImageList.Add(new ImageEntity
						{
							ImagePath = "",
							ImageSource = imageDataByBytes,
							IsCheck = HasModel
						});
						byte[] array3 = OwnerModule.UserData["RegisterROIData-" + num2];
						byte[] rectROIBytes = OwnerModule.UserData["RegisterRectROIData-" + num2];
						ROISelecter rOISelecter2 = ROIRoot?.ROISelecters[num2] as ROISelecter;
						if (array3 != null && rOISelecter2 != null)
						{
							string roiType = Encoding.UTF8.GetString(OwnerModule.UserData["RegisterROIType-" + num2]);
							GetROIByBytes(array3, rOISelecter2, imageDataByBytes.Width, imageDataByBytes.Height, roiType, rectROIBytes);
						}
					}
				}
				if (ImageList.Count > 0)
				{
					CurImage = ImageList[0];
				}
			}
			if (HasModel)
			{
				UpdateThumbnailList();
			}
		}
		catch (Exception)
		{
		}
		ROIRoot.CurImageWidth = CurImage?.ImageSource?.Width ?? 0;
		ROIRoot.CurImageHeight = CurImage?.ImageSource?.Height ?? 0;
		isItnit = true;
	}

	public void SetOwnerModule(IVmModule module)
	{
		OwnerModule = module;
	}

	private void AddCurImageCommandExecute()
	{
		if (ImageList.Count >= 10)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ImageLimitTip") + "：" + 10, MessageIcons.Error);
		}
		else if (OwnerModule != null)
		{
			IVmIO iVmIO = (OwnerModule as VmModule).Inputs.FirstOrDefault((VmIO p) => p.TypeName == "IMAGE");
			ImageData imageData = iVmIO.GetCombinationValueByType(uint.MaxValue) as ImageData;
			if (imageData != null && imageData.ImageBuffer == null)
			{
				imageData.ImageBuffer = Byte2ImageSource.GetImageBufferByFileName(imageData.MemoryAddress, imageData.Width, imageData.Height, imageData.PixelFormat);
				imageData = ((imageData.ImageBuffer == null) ? null : imageData);
			}
			if (imageData != null)
			{
				ImageList.Add(new ImageEntity
				{
					ImagePath = "",
					ImageSource = imageData,
					IsCheck = false
				});
				UpdateImageListSource();
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoCurrentImage"), MessageIcons.Warning);
			}
		}
	}

	private void AddCacheImageCommandExecute()
	{
		if (ImageList.Count >= 10)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ImageLimitTip"), MessageIcons.Error);
		}
		else
		{
			if (OwnerModule == null)
			{
				return;
			}
			List<ImageData> cacheImageList = ModuleCommonMethodMgr.Instance.GetCacheImageList(OwnerModule);
			if (cacheImageList != null && cacheImageList.Count > 0)
			{
				foreach (ImageData item in cacheImageList)
				{
					ImageList.Add(new ImageEntity
					{
						ImagePath = "",
						ImageSource = item,
						IsCheck = false
					});
					if (ImageList.Count == 10)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ImageLimitTip") + "：" + 10, MessageIcons.Error);
						break;
					}
				}
				UpdateImageListSource();
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoCacheImage"), MessageIcons.Warning);
			}
		}
	}

	private void AddImageCommandExecute()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Multiselect = true;
		openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpeg;*.jpg;*.png;*.tif;*.tiff";
		string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(openFileDialog, path);
		if (openFileDialog.ShowDialog() != false)
		{
			DoAddImages(openFileDialog.FileNames);
			if (File.Exists(openFileDialog.FileName))
			{
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), OwnerModule?.ModuleDir);
			}
		}
	}

	private void AddImageDirCommandExecute()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		CommonOpenFileDialog val = new CommonOpenFileDialog();
		val.IsFolderPicker = true;
		val.Multiselect = false;
		CommonFileDialogResult val2 = ((CommonFileDialog)val).ShowDialog();
		try
		{
			if ((int)val2 != 2)
			{
				string dir = ((CommonFileDialog)val).FileName.Trim();
				AddImageByDir(dir);
				UpdateImageListSource();
			}
		}
		catch
		{
		}
		finally
		{
			((CommonFileDialog)val).Dispose();
		}
	}

	private void DeleteImageCommandExecute(object param)
	{
		if (!(param is ImageEntity imageEntity) || !ImageList.Contains(imageEntity))
		{
			return;
		}
		int num = ImageList.IndexOf(imageEntity);
		if (num < 10)
		{
			((Collection<IROI>)(object)(ROIRoot.ROISelecters[num] as ROISelecter)?.ROIList).Clear();
		}
		for (int i = num; i < TotalImageCount; i++)
		{
			int num2 = i + 1;
			if (num2 >= TotalImageCount || num2 >= 10 || !(ROIRoot.ROISelecters[i] is ROISelecter) || !(ROIRoot.ROISelecters[num2] is ROISelecter))
			{
				continue;
			}
			((Collection<IROI>)(object)(ROIRoot.ROISelecters[i] as ROISelecter).NormalizeROIList).Clear();
			List<IROI> list = ((IEnumerable<IROI>)(ROIRoot.ROISelecters[num2] as ROISelecter).NormalizeROIList).ToList();
			bool isVisible = ((IROIVisible)(ROIRoot.ROISelecters[i] as ROISelecter).NormalizeROIList).IsVisible;
			bool isVisible2 = ((IROIVisible)(ROIRoot.ROISelecters[num2] as ROISelecter).NormalizeROIList).IsVisible;
			foreach (IROI item in list)
			{
				((Collection<IROI>)(object)(ROIRoot.ROISelecters[i] as ROISelecter).NormalizeROIList).Add(item);
			}
			((IROIVisible)(ROIRoot.ROISelecters[i] as ROISelecter).NormalizeROIList).IsVisible = isVisible2;
			((IROIVisible)(ROIRoot.ROISelecters[num2] as ROISelecter).NormalizeROIList).IsVisible = isVisible;
			((Collection<IROI>)(object)(ROIRoot.ROISelecters[num2] as ROISelecter).NormalizeROIList).Clear();
		}
		ImageEntity imageEntity2 = curImage;
		if (imageEntity == CurImage)
		{
			int num3 = num + 1;
			int num4 = ((num3 < ImageList.Count) ? num3 : 0);
			imageEntity2 = ((ImageList.Count > num4) ? ImageList[num4] : null);
		}
		ImageList.Remove(imageEntity);
		if (imageEntity2 != curImage)
		{
			CurImage = imageEntity2;
		}
		else if (CurImage != null)
		{
			CurROIList = (IList)(ROIRoot.ROISelecters[ImageList.IndexOf(CurImage)] as ROISelecter).NormalizeROIList;
		}
		OnPropertyChanged("CurIndex");
		OnPropertyChanged("TotalImageCount");
		OnPropertyChanged("AddImageEnable");
	}

	private void ClearImageCommandExecute()
	{
		CurImage = null;
		ImageList.Clear();
		for (int i = 0; i < 10; i++)
		{
			((Collection<IROI>)(object)(ROIRoot.ROISelecters[i] as ROISelecter)?.ROIList).Clear();
		}
		OnPropertyChanged("CurIndex");
		OnPropertyChanged("TotalImageCount");
		OnPropertyChanged("AddImageEnable");
	}

	private void DeleteInstanceCommandExecute(object param)
	{
		if (!(param is FewShotInstanceImageData fewShotInstanceImageData) || !ThumbnailList.Contains(fewShotInstanceImageData))
		{
			return;
		}
		try
		{
			((IUserStringData)OwnerModule.Params)["GalleryInstanceDelete"] = fewShotInstanceImageData.CalssIndex + "-" + fewShotInstanceImageData.ImageIndex;
			ThumbnailList.Remove(fewShotInstanceImageData);
			RefreshThumbnailIndex();
			HasModel = ThumbnailList.Count != 0;
			if (!HasModel)
			{
				DeleteGallery();
			}
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageButtons.Normal, MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(ex2.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	private void DrawROICommandExecute(string param)
	{
		if (param == "1" || param == "2" || param == "3")
		{
			bool curSign = param != "2";
			int num = ROIRoot?.ROISelecters?.Select((IROISelecter p) => ((IEnumerable<IROI>)p.ROIList).Count((IROI q) =>
			{
				IROISign obj = q as IROISign;
				return obj != null && obj.Sign == curSign;
			}))?.ToList().Sum() ?? 0;
			if (num < 10)
			{
				string roiType = ((param == "3") ? "Box" : "Polygon");
				PrepareDrawRoi(roiType);
			}
			else
			{
				PrepareDrawRoi("");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit"), MessageIcons.Error);
			}
		}
		else
		{
			PrepareDrawRoi("");
		}
	}

	private void ROIChangedCommandExecute()
	{
		if (CurImage != null)
		{
			CurImage.IsCheck = false;
		}
	}

	private void RoiPasteCommandExecute(RoiWithTagEventArgs args)
	{
		object ls = args.Tag;
		IROI roi = args.Roi;
		IROISign obj = roi as IROISign;
		bool curSign = obj != null && obj.Sign == true;
		int num = ROIRoot?.ROISelecters?.Select((IROISelecter p) => ((IEnumerable<IROI>)p.ROIList).Count((IROI q) =>
		{
			IROISign obj2 = q as IROISign;
			return obj2 != null && obj2.Sign == curSign;
		}))?.ToList().Sum() ?? 0;
		if (num > 10)
		{
			int num2 = ImageList.IndexOf(curImage);
			if (num2 >= 0 && ROIRoot.ROISelecters.Count > num2 && ROIRoot.ROISelecters[num2] is ROISelecter rOISelecter)
			{
				((Collection<IROI>)(object)rOISelecter.ROIList).Remove(roi);
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit"), MessageIcons.Error);
			return;
		}
		IROISelecter iROISelecter;
		if ((iROISelecter = ROIRoot.ROISelecters.SingleOrDefault((IROISelecter p) => p.ROIList == ls)) != null)
		{
			iROISelecter.DoRoiPaste(roi);
		}
		if (CurImage != null)
		{
			CurImage.IsCheck = false;
		}
	}

	private void ChangeCurImageCommandExecute(string param)
	{
		if (ImageList.Count >= 2)
		{
			if (CurImage == null)
			{
				CurImage = ImageList[0];
				return;
			}
			int num = ImageList.IndexOf(CurImage);
			if (param == "Last")
			{
				num = ((num == 0) ? (ImageList.Count - 1) : (num - 1));
			}
			else
			{
				num = ((num != ImageList.Count - 1) ? (num + 1) : 0);
			}
			CurImage = ImageList[num];
		}
	}

	private async void MatchCommandExecuteAsync()
	{
		ShowMaskTip = AppLocalizationService.GetLang("Lang_RegisterProgressText");
		IsShowMask = true;
		await RegisterAsync();
		IsShowMask = false;
	}

	private void ClosingCommandExecute()
	{
		try
		{
			int count = ImageList.Count;
			OwnerModule.UserData["ImageNumber"] = BitConverter.GetBytes(count);
			for (int i = count; i < 10; i++)
			{
				OwnerModule.UserData["RegisterImage-" + i] = null;
				OwnerModule.UserData["RegisterROIData-" + i] = null;
				OwnerModule.UserData["RegisterROIType-" + i] = null;
				OwnerModule.UserData["RegisterRectROIData-" + i] = null;
			}
			foreach (ImageEntity image in ImageList)
			{
				int index = ImageList.IndexOf(image);
				List<byte> list = new List<byte>();
				byte[] bytes = BitConverter.GetBytes(1);
				list.AddRange(bytes);
				ImageData imageData = image.ImageSource;
				if (!string.IsNullOrEmpty(image.ImagePath))
				{
					imageData = ImageData.GetImageDataFromFile(image.ImagePath);
				}
				if (imageData != null)
				{
					if (imageData.DetailedPixelFormat == 0)
					{
						imageData.DetailedPixelFormat = ImageData.GetPixelFormatProtocolCode(imageData.PixelFormat);
					}
					byte[] bytes2 = BitConverter.GetBytes(imageData.Width);
					byte[] bytes3 = BitConverter.GetBytes(imageData.Height);
					byte[] bytes4 = BitConverter.GetBytes(imageData.Buffer.Length);
					byte[] bytes5 = BitConverter.GetBytes(GetAlgorithmPixelFormat(imageData.DetailedPixelFormat));
					list.AddRange(bytes2);
					list.AddRange(bytes3);
					list.AddRange(bytes4);
					list.AddRange(imageData.Buffer);
					list.AddRange(bytes5);
					try
					{
						byte[] value = list.ToArray();
						ObservableRangeCollection<IROI> val = ROIRoot?.ROISelecters[index]?.ROIList;
						GetRoiBuffer(imageData, index, out var roiDatas, out var rectRoiDatas, out var roiTypes);
						OwnerModule.UserData["RegisterImage-" + index] = value;
						OwnerModule.UserData["RegisterROIData-" + index] = roiDatas;
						OwnerModule.UserData["RegisterROIType-" + index] = roiTypes;
						OwnerModule.UserData["RegisterRectROIData-" + index] = rectRoiDatas;
					}
					catch (VmException ex)
					{
						throw new Exception(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
					}
					catch (Exception ex2)
					{
						int result = 0;
						string message = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
						throw new Exception(message);
					}
					continue;
				}
				throw new Exception(AppLocalizationService.GetLang("Lang_LoadingImageError"));
			}
		}
		catch (Exception ex3)
		{
			LogHelper.Error(ex3.Message);
		}
	}
}
