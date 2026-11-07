using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using HikExternalCall.Common;
using HikExternalCall.ImageProcess;
using HikExternalCall.MvbOCR;
using HikPcUI.Controls;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSOcrModuControl;

public partial class OCRTrainUI : System.Windows.Controls.UserControl, IFixSize, IOwner, IParentNode, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private Size _FixSize = new Size(900.0, 600.0);

	private object ParentNode;

	private Expand _MyExpan = new Expand();

	private object _Polarity;

	private object _RangCharWidth;

	private object _RangCharHeight;

	private object _BinaryCoef;

	private object _MainLineDistThresh;

	private object _OrientHalfRange;

	private object _SlantHalfRange;

	private object _GapMergeEnable;

	private object _MinInterTextGap;

	private object _MinInterCharGap;

	private object _MaxLengthWidthRatio;

	private object _ClassMethod;

	private object _WidthType;

	private object _IgnoreBorderFragmentEnable;

	private object _StrokeFilterEnable;

	private object _RangStrokeWidth;

	private object _RangCharArea;

	private object _SimilarFun;

	private Integer _MaxCharHeight;

	private Integer _MaxCharWidth;

	private OCRTrainUIData myOCRTrainUIData = new OCRTrainUIData();

	private ImageData _Image;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	private Byte2ImageSource b2SConvertor2 = new Byte2ImageSource();

	private int acceptThreshold;

	private List<HikPasterParameter> CharacterList = new List<HikPasterParameter>();

	private string OldString = "";

	private int curSelectedIndex = -1;

	private string CurFilePath = "";

	public uint[] nFontTranNum = new uint[1000];

	private Dictionary<MVBOCR_PARAM_KEY, string> MvbOCR_Map = new Dictionary<MVBOCR_PARAM_KEY, string>();

	private static int imgMaxWidth = 640;

	private static int imgMaxHeight = 480;

	private static int charMaxWidth = 4;

	private static int charMaxHeight = 8;

	private AlgorithmTabRoot _Root;

	private static bool IsNeedInitDefaultOCR = true;

	private bool _IsRecognizeEnable = false;

	private bool _IsTrainEnable = false;

	private string _DefaultLibPath = "";

	public static int MaxEnhanceCount = 5;

	private int _SelectedItemsCount;

	public Size FixSize => _FixSize;

	public object Owner { get; set; }

	public Expand MyExpan => _MyExpan;

	public object Polarity
	{
		get
		{
			if (_Polarity == null && Root != null)
			{
				_Polarity = Root.FindBaseNodeByName("Polarity");
			}
			return _Polarity;
		}
	}

	public object RangCharWidth
	{
		get
		{
			if (_RangCharWidth == null && Root != null)
			{
				_RangCharWidth = Root.FindBaseNodeByName("RangCharWidth");
				if (_RangCharWidth is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _RangCharWidth;
		}
	}

	public object RangCharHeight
	{
		get
		{
			if (_RangCharHeight == null && Root != null)
			{
				_RangCharHeight = Root.FindBaseNodeByName("RangCharHeight");
				if (_RangCharHeight is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _RangCharHeight;
		}
	}

	public object BinaryCoef
	{
		get
		{
			if (_BinaryCoef == null && Root != null)
			{
				_BinaryCoef = Root.FindBaseNodeByName("BinaryCoef");
				if (_BinaryCoef is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _BinaryCoef;
		}
	}

	public object MainLineDistThresh
	{
		get
		{
			if (_MainLineDistThresh == null && Root != null)
			{
				_MainLineDistThresh = Root.FindBaseNodeByName("MainLineDistThresh");
				if (_MainLineDistThresh is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _MainLineDistThresh;
		}
	}

	public object OrientHalfRange
	{
		get
		{
			if (_OrientHalfRange == null && Root != null)
			{
				_OrientHalfRange = Root.FindBaseNodeByName("OrientHalfRange");
				if (_OrientHalfRange is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _OrientHalfRange;
		}
	}

	public object SlantHalfRange
	{
		get
		{
			if (_SlantHalfRange == null && Root != null)
			{
				_SlantHalfRange = Root.FindBaseNodeByName("SlantHalfRange");
				if (_SlantHalfRange is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _SlantHalfRange;
		}
	}

	public object GapMergeEnable
	{
		get
		{
			if (_GapMergeEnable == null && Root != null)
			{
				_GapMergeEnable = Root.FindBaseNodeByName("GapMergeEnable");
			}
			return _GapMergeEnable;
		}
	}

	public object MinInterTextGap
	{
		get
		{
			if (_MinInterTextGap == null && Root != null)
			{
				_MinInterTextGap = Root.FindBaseNodeByName("MinInterTextGap");
				if (_MinInterTextGap is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _MinInterTextGap;
		}
	}

	public object MinInterCharGap
	{
		get
		{
			if (_MinInterCharGap == null && Root != null)
			{
				_MinInterCharGap = Root.FindBaseNodeByName("MinInterCharGap");
				if (_MinInterCharGap is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _MinInterCharGap;
		}
	}

	public object MaxLengthWidthRatio
	{
		get
		{
			if (_MaxLengthWidthRatio == null && Root != null)
			{
				_MaxLengthWidthRatio = Root.FindBaseNodeByName("MaxLengthWidthRatio");
				if (_MaxLengthWidthRatio is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _MaxLengthWidthRatio;
		}
	}

	public object ClassMethod
	{
		get
		{
			if (_ClassMethod == null && Root != null)
			{
				_ClassMethod = Root.FindBaseNodeByName("ClassMethod");
			}
			return _ClassMethod;
		}
	}

	public object WidthType
	{
		get
		{
			if (_WidthType == null && Root != null)
			{
				_WidthType = Root.FindBaseNodeByName("WidthType");
			}
			return _WidthType;
		}
	}

	public object IgnoreBorderFragmentEnable
	{
		get
		{
			if (_IgnoreBorderFragmentEnable == null && Root != null)
			{
				_IgnoreBorderFragmentEnable = Root.FindBaseNodeByName("IgnoreBorderFragmentEnable");
			}
			return _IgnoreBorderFragmentEnable;
		}
	}

	public object StrokeFilterEnable
	{
		get
		{
			if (_StrokeFilterEnable == null && Root != null)
			{
				_StrokeFilterEnable = Root.FindBaseNodeByName("StrokeFilterEnable");
			}
			return _StrokeFilterEnable;
		}
	}

	public object RangStrokeWidth
	{
		get
		{
			if (_RangStrokeWidth == null && Root != null)
			{
				_RangStrokeWidth = Root.FindBaseNodeByName("RangStrokeWidth");
				if (_RangStrokeWidth is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _RangStrokeWidth;
		}
	}

	public object RangCharArea
	{
		get
		{
			if (_RangCharArea == null && Root != null)
			{
				_RangCharArea = Root.FindBaseNodeByName("RangCharArea");
				if (_RangCharArea is NameValueItem nameValueItem)
				{
					nameValueItem.SubscribeVisible = false;
				}
			}
			return _RangCharArea;
		}
	}

	public object SimilarFun
	{
		get
		{
			if (_SimilarFun == null && Root != null)
			{
				_SimilarFun = Root.FindBaseNodeByName("SimilarFun");
			}
			return _SimilarFun;
		}
	}

	public Integer MaxCharHeight
	{
		get
		{
			if (_MaxCharHeight == null && Root != null)
			{
				_MaxCharHeight = Root.FindBaseNodeByName("MaxCharHeight") as Integer;
			}
			return _MaxCharHeight;
		}
	}

	public Integer MaxCharWidth
	{
		get
		{
			if (_MaxCharWidth == null && Root != null)
			{
				_MaxCharWidth = Root.FindBaseNodeByName("MaxCharWidth") as Integer;
			}
			return _MaxCharWidth;
		}
	}

	public ImageData CurImage
	{
		get
		{
			return _Image;
		}
		set
		{
			_Image = value;
			if (_Image != null)
			{
				imageView.ImageDataSource = _Image;
				IsRecognizeEnable = true;
			}
		}
	}

	private AlgorithmTabRoot Root
	{
		get
		{
			if (_Root == null && ParentNode != null && ParentNode is NameValueItem)
			{
				_Root = (ParentNode as NameValueItem).GetTopBaseNode() as AlgorithmTabRoot;
			}
			return _Root;
		}
	}

	public bool IsRecognizeEnable
	{
		get
		{
			return _IsRecognizeEnable;
		}
		set
		{
			if (_IsRecognizeEnable != value)
			{
				_IsRecognizeEnable = value;
				NotifyPropertyChanged("IsRecognizeEnable");
			}
		}
	}

	public bool IsTrainEnable
	{
		get
		{
			return _IsTrainEnable;
		}
		set
		{
			if (_IsTrainEnable != value)
			{
				_IsTrainEnable = value;
				NotifyPropertyChanged("IsTrainEnable");
			}
		}
	}

	private string DefaultLibPath
	{
		get
		{
			if (_DefaultLibPath == "" && Root != null && Root.Owner is IVmModule { ID: var iD })
			{
				string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				if (!Directory.Exists(folderPath + "\\VMtemp"))
				{
					Directory.CreateDirectory(folderPath + "\\VMtemp");
				}
				_DefaultLibPath = folderPath + "\\VMtemp\\" + iD + ".fblib";
			}
			return _DefaultLibPath;
		}
	}

	public int SelectedItemsCount
	{
		get
		{
			return _SelectedItemsCount;
		}
		set
		{
			if (_SelectedItemsCount != value)
			{
				_SelectedItemsCount = value;
				NotifyPropertyChanged("SelectedItemsCount");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public object GetParentNode()
	{
		return ParentNode;
	}

	public void SetParentNode(object parentNode)
	{
		ParentNode = parentNode;
	}

	public OCRTrainUI()
	{
		InitializeComponent();
		if (AppLocalizationService.GetLang("Language") == "English")
		{
			_FixSize = new Size(980.0, 600.0);
			baseParam.Width = 170.0;
		}
		Init();
	}

	private bool GetCharMaxSize(out int maxCharWidth, out int maxCharHeight)
	{
		maxCharWidth = 4;
		maxCharHeight = 8;
		if (MaxCharHeight == null)
		{
			LogHelper.Error("OCR:获取MaxCharHeight失败");
			return false;
		}
		if (MaxCharWidth == null)
		{
			LogHelper.Error("OCR:获取MaxCharWidth失败");
			return false;
		}
		maxCharHeight = MaxCharHeight.CurValueA;
		maxCharWidth = MaxCharWidth.CurValueA;
		return true;
	}

	private int SetOcrParam()
	{
		int cb = Marshal.SizeOf<OCR_SEGMENT_PARAM>();
		IntPtr intPtr = Marshal.AllocHGlobal(cb);
		int result = 0;
		try
		{
			OCR_SEGMENT_PARAM structure = default;
			if (Root != null)
			{
				uint result2 = 0u;
				structure.nMainLineDistThresh = (uint.TryParse(Root.GetParamByName("MainLineDistThresh"), out result2) ? result2 : 0u);
				structure.nPolarity = (uint.TryParse(Root.GetParamByName("Polarity"), out result2) ? result2 : 0u);
				structure.nMaxCharHeight = (uint.TryParse(Root.GetParamByName("MaxCharHeight"), out result2) ? result2 : 0u);
				structure.nMaxCharWidth = (uint.TryParse(Root.GetParamByName("MaxCharWidth"), out result2) ? result2 : 0u);
				structure.nMinCharHeight = (uint.TryParse(Root.GetParamByName("MinCharHeight"), out result2) ? result2 : 0u);
				structure.nMinCharWidth = (uint.TryParse(Root.GetParamByName("MinCharWidth"), out result2) ? result2 : 0u);
				structure.nMinInterCharGap = (uint.TryParse(Root.GetParamByName("MinInterCharGap"), out result2) ? result2 : 0u);
				structure.nBinaryCoef = (uint.TryParse(Root.GetParamByName("BinaryCoef"), out result2) ? result2 : 0u);
				structure.nIgnoreBorder = (uint.TryParse(Root.GetParamByName("IgnoreBorderFragmentEnable"), out result2) ? result2 : 0u);
				structure.nMaxCharArea = (uint.TryParse(Root.GetParamByName("MaxCharArea"), out result2) ? result2 : 0u);
				structure.nMaxLengthWidthRatio = (uint.TryParse(Root.GetParamByName("MaxLengthWidthRatio"), out result2) ? result2 : 0u);
				structure.nMaxStrokeWidth = (uint.TryParse(Root.GetParamByName("MaxStrokeWidth"), out result2) ? result2 : 0u);
				structure.nMinCharArea = (uint.TryParse(Root.GetParamByName("MinCharArea"), out result2) ? result2 : 0u);
				structure.nMinStrokeWidth = (uint.TryParse(Root.GetParamByName("MinStrokeWidth"), out result2) ? result2 : 0u);
				structure.nOrientHalfRange = (uint.TryParse(Root.GetParamByName("OrientHalfRange"), out result2) ? result2 : 0u);
				structure.nSlantHalfRange = (uint.TryParse(Root.GetParamByName("SlantHalfRange"), out result2) ? result2 : 0u);
				structure.nStrokeFilter = (uint.TryParse(Root.GetParamByName("StrokeFilterEnable"), out result2) ? result2 : 0u);
				structure.nTextGap = (uint.TryParse(Root.GetParamByName("MinInterTextGap"), out result2) ? result2 : 0u);
				structure.nWidthType = (uint.TryParse(Root.GetParamByName("WidthType"), out result2) ? result2 : 0u);
			}
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
			result = OCRManager.MVBOCR_SetConfig(intPtr);
		}
		catch (Exception ex)
		{
			return int.TryParse(ex.Message, out result) ? result : (-1);
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
				intPtr = IntPtr.Zero;
			}
		}
		return result;
	}

	private void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
		NotifyPropertyChanged("");
		Button_Click_OpenImage(null, null);
		if (Root == null)
		{
			return;
		}
		if (MaxCharHeight != null)
		{
			PropertyChangedEventManager.AddHandler(MaxCharHeight, PropertyChanged_MaxCharSize, "CurValueA");
		}
		if (MaxCharWidth != null)
		{
			PropertyChangedEventManager.AddHandler(MaxCharWidth, PropertyChanged_MaxCharSize, "CurValueA");
		}
		try
		{
			InitDefaultOCR();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), ex.Message, MessageIcons.Error);
		}
		byte[] array = null;
		try
		{
			array = ((IUserBytesData)Root.UserStringData)["LoadFontPath"];
			try
			{
				array = EncryptionAndDecryptionHelper.GetDecryptedData(array);
			}
			catch (VmException ex2)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("Lang_SapLibErrtoData") + ErrorCodeInfo.GetErrorInfo(ex2.errorCode), MessageIcons.Error);
			}
			if (array == null || array.Length == 0)
			{
				return;
			}
			using MemoryStream memoryStream = new MemoryStream(array);
			memoryStream.Seek(0L, SeekOrigin.Begin);
			BinaryReader binaryReader = new BinaryReader(memoryStream);
			int num = binaryReader.ReadInt32();
			if (num > 0 && array.Length - num - 4 > 0)
			{
				binaryReader.ReadBytes(num);
				IntPtr inPtr = ExternalCallHelper.Bytes2IntPtr(array, out var hObject);
				int num2 = OCRManager.OCR_ImportFontE(inPtr, array.Length);
				ExternalCallHelper.FreeGCHandle(hObject);
				if (num2 != 0)
				{
					string strMsg = AppLocalizationService.GetLang("读取样本库失败") + " :" + ErrorCodeInfo.GetErrorInfo(num2);
					HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), strMsg, MessageIcons.Error);
				}
				else
				{
					byte[] libData = binaryReader.ReadBytes(array.Length - num - 4);
					SetLibData(libData);
				}
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("Lang_SapLibErrtoData"), MessageIcons.Error);
			}
		}
		catch (Exception ex3)
		{
			string message = ex3.Message;
			LogHelper.Error("字符识别:获取样本库失败" + message);
		}
	}

	private void PropertyChanged_MaxCharSize(object sender, PropertyChangedEventArgs e)
	{
		IsNeedInitDefaultOCR = true;
	}

	private void Init()
	{
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_POLARITY, "Polarity");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_MIN_WIDTH, "MinCharWidth");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_MIN_HEIGHT, "MinCharHeight");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_MAX_HEIGHT, "MaxCharHeight");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_MAX_WIDTH, "MaxCharWidth");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_STROKE_WIDTH_FILTER_ENABLE, "StrokeFilterEnable");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_MIN_STROKE_WIDTH, "MinStrokeWidth");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_MAX_STROKE_WIDTH, "MaxStrokeWidth");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_FRAGMENT_MIN_AREA, "MinCharArea");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_CHARACTER_FRAGMENT_MAX_AREA, "MaxCharArea");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_BINARY_COEF, "BinaryCoef");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_IGNORE_BORDER_FRAGMENT_ENABLE, "IgnoreBorderFragmentEnable");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_MAIN_LINE_DIST_THRE, "MainLineDistThresh");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_ORIENT_HALF_RANGE, "OrientHalfRange");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_SLANT_HALF_RANGE, "SlantHalfRange");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_GAP_MERGE_ENABLE, "GapMergeEnable");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_MIN_INTER_CHARACTER_GAP, "MinInterCharGap");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_MIN_INTER_TEXT_GAP, "MinInterTextGap");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_MAX_LENTH_WIDTH_RATIO, "MaxLengthWidthRatio");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_KNN_CLASSES_METHOD, "ClassMethod");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_WIDTH_TYPE, "WidthType");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_ACCEPT_THRESHOLD, "AcceptThreshold");
		MvbOCR_Map.Add(MVBOCR_PARAM_KEY.MVBOCR_IDX_KNN_DISTANCE, "SimilarFun");
		DataContext = myOCRTrainUIData;
		imageView.DataContext = myOCRTrainUIData;
		imageView.ROITypeUI = ROITypeUIEnum.Transparent;
		HikImageView hikImageView = imageView;
		hikImageView.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChange));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView2.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
	}

	private void imageView_OnPixelChange(int X, int Y)
	{
		try
		{
			string text = "--";
			string text2 = "--";
			if (X >= 0)
			{
				text = X.ToString();
			}
			if (Y >= 0)
			{
				text2 = Y.ToString();
			}
			xyVal.Content = "( " + text + " , " + text2 + " )";
			byte b = 0;
			if (CurImage != null)
			{
				int num = CurImage.Width * CurImage.PixelFormat.BitsPerPixel / 8;
				int num2 = 0;
				if (ImageData.GetPixelFormatCode(CurImage.PixelFormat) == MPixelFormat.Gray8)
				{
					num2 = Y * num + X;
					if (num2 >= 0 && num2 < CurImage.ImageBuffer.Length)
					{
						b = CurImage.ImageBuffer[num2];
					}
				}
			}
			grayVal.Content = b.ToString();
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
	}

	private void imageView_OnRoiCountChange(object Roi, EventArgs e = null)
	{
		if (hikFrame.IsChecked == true)
		{
			Arrow.IsChecked = true;
		}
	}

	private void OK_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (ApplyLib() >= 0)
			{
				(Owner as Window).DialogResult = true;
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		(Owner as Window).Close();
	}

	private int ApplyLib()
	{
		IParamsConfig userStringData = Root.UserStringData;
		if (userStringData == null)
		{
			return -1;
		}
		if (myOCRTrainUIData.Collection.Count > 0)
		{
			byte[] array = TranLib();
			if (array == null)
			{
				LogHelper.Error("字符库为空！");
				HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("字符库为空"), MessageIcons.Error);
				return -1;
			}
			try
			{
				array = EncryptionAndDecryptionHelper.GetDecryptedData(array);
				((IUserBytesData)userStringData)["LoadFontPath"] = array;
				try
				{
				}
				catch (Exception)
				{
				}
				return 0;
			}
			catch (Exception ex2)
			{
				string strMsg = ex2.Message;
				if (ex2 is VmException ex3)
				{
					strMsg = ErrorCodeInfo.GetErrorInfo(ex3.errorCode);
				}
				HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), strMsg, MessageIcons.Error);
				return -1;
			}
		}
		try
		{
			((IUserStringData)userStringData)["ClearFontPath"] = DefaultLibPath;
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("字符库为空"), MessageIcons.Error);
			return -1;
		}
		catch (Exception)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("清空字符库失败"), MessageIcons.Error);
			return -1;
		}
	}

	private void Button_Click_Clear(object sender, RoutedEventArgs e)
	{
		DeleteSelectedItem();
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		List<object> list = new List<object>();
		foreach (object itemsDatum in myOCRTrainUIData.ItemsData)
		{
			if (!(itemsDatum is HikPasterParameter))
			{
				list.Add(itemsDatum);
			}
		}
		foreach (object item in list)
		{
			myOCRTrainUIData.ItemsData.Remove(item);
		}
		string name = (sender as FrameworkElement).Name;
		if (name == "hikFrame")
		{
			imageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
		}
		else
		{
			imageView.UnPrepareRawRoi();
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		imageView.UnPrepareRawRoi();
	}

	private void zoomOrgBtn_Click(object sender, RoutedEventArgs e)
	{
		imageView.Init();
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		FullScreen instance = FullScreen.Instance;
		viewGrid.Children.Remove(imageView);
		instance.SetChild(imageView);
		instance.ShowDialog();
		instance.RemoveChild();
		viewGrid.Children.Add(imageView);
	}

	private void Button_Click_OpenImage(object sender, RoutedEventArgs e)
	{
		try
		{
			InitData();
			GetCurImage();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public void GetCurImage()
	{
		if (Root.Owner == null)
		{
			CurImgBtn.Visibility = Visibility.Collapsed;
		}
		else
		{
			if (!(VmSolution.Instance[(Root.Owner as IVmFullPath)?.FullPath + ".[Display]"] is ParamRoot paramRoot))
			{
				return;
			}
			IList<object> displayList = paramRoot.GetDisplayList();
			foreach (Apps.XmlParser.Object item in displayList)
			{
				if (item.TypeA == "image" || item.TypeA == "pixelinfoimage")
				{
					ImageData imageData = (ImageData)item.GetValue(uint.MaxValue);
					if (imageData != null && imageData.ImageBuffer == null)
					{
						imageData.ImageBuffer = Byte2ImageSource.GetImageBufferByFileName(imageData.MemoryAddress, imageData.Width, imageData.Height, imageData.PixelFormat);
						imageData = ((imageData.ImageBuffer == null) ? null : imageData);
					}
					if (imageData != null && imageData.PixelFormat != PixelFormats.Gray8)
					{
						throw new Exception(AppLocalizationService.GetLang("Lang_UnsupportFormatWarning"));
					}
					CurImage = imageData;
					break;
				}
			}
		}
	}

	private void InitData()
	{
		myOCRTrainUIData.ItemsData.Clear();
		CharacterList.Clear();
		CharacterText.Content = "";
	}

	private void Button_Click_OpenImage2(object sender, RoutedEventArgs e)
	{
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		string path = ConfigurationHelper.INIGetDefaultOpenPath((Root.Owner as IVmModule)?.ModuleDir);
		try
		{
			ImageProcessManager.GetMono8ImageDataByFileDialog(ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer, ref path);
			if (imageData.ImageBuffer != null)
			{
				InitData();
				CurImage = imageData;
			}
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), (Root.Owner as IVmModule)?.ModuleDir);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	private void Button_Click_Recognize(object sender, RoutedEventArgs e)
	{
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			if (CurImage == null)
			{
				throw new Exception(AppLocalizationService.GetLang("未加载") + AppLocalizationService.GetLang("图片") + "!");
			}
			OCR_IMAGE image = new OCR_IMAGE
			{
				nImgFormat = 1u,
				nImgWidth = (uint)CurImage.Width,
				nImgHeight = (uint)CurImage.Height,
				nImgDataLen = (uint)CurImage.ImageBuffer.Length
			};
			if (CurImage.Width > imgMaxWidth || CurImage.Height > imgMaxHeight)
			{
				if (CurImage.Width > imgMaxWidth)
				{
					imgMaxWidth = CurImage.Width;
				}
				if (CurImage.Height > imgMaxHeight)
				{
					imgMaxHeight = CurImage.Height;
				}
			}
			int maxCharHeight = 0;
			int maxCharWidth = 0;
			GetCharMaxSize(out maxCharWidth, out maxCharHeight);
			int num = -1;
			LogHelper.Info("imgMaxWidth, imgMaxHeight, tempCharWidth, tempCharHeight:" + imgMaxWidth + " " + imgMaxHeight + " " + maxCharWidth + " " + maxCharHeight);
			num = SetOcrParam();
			if (num != 0)
			{
				throw new Exception(AppLocalizationService.GetLang("Params Settings") + AppLocalizationService.GetLang("失败") + ":" + ErrorCodeInfo.GetErrorInfo(num));
			}
			MVBOCR_CHARACTER_SEGMENT_INPUT mVBOCR_CHARACTER_SEGMENT_INPUT = default;
			image.pImgData = ExternalCallHelper.Bytes2IntPtr(CurImage.ImageBuffer, out var hObject);
			mVBOCR_CHARACTER_SEGMENT_INPUT.image = image;
			HikCoverRoiParameter hikCoverRoiParameter = null;
			foreach (object itemsDatum in myOCRTrainUIData.ItemsData)
			{
				if (itemsDatum is HikCoverRoiParameter)
				{
					hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
					break;
				}
			}
			if (hikCoverRoiParameter == null)
			{
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.center_x = (float)image.nImgWidth / 2f;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.center_y = (float)image.nImgHeight / 2f;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.width = image.nImgWidth;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.height = image.nImgHeight;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.angle = 0f;
			}
			else
			{
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.width = (float)hikCoverRoiParameter.Width;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.height = (float)hikCoverRoiParameter.Height;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.center_x = (float)hikCoverRoiParameter.OriginPoint.X + mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.width / 2f;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.center_y = (float)hikCoverRoiParameter.OriginPoint.Y + mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.height / 2f;
				mVBOCR_CHARACTER_SEGMENT_INPUT.text_box.angle = (float)hikCoverRoiParameter.Angle;
			}
			MVBOCR_CHARACTER_SEGMENT_OUTPUT structure = default;
			IntPtr ptr = IntPtr.Zero;
			ExternalCallHelper.StructureToPtr(structure, ref ptr);
			intPtr = ExternalCallHelper.StructureToPtr(mVBOCR_CHARACTER_SEGMENT_INPUT);
			int num2 = OCRManager.OCR_ProcessSegmentE(intPtr, ptr);
			ExternalCallHelper.FreeGCHandle(hObject);
			if (num2 != 0)
			{
				throw new Exception(ErrorCodeInfo.GetErrorInfo(num2));
			}
			ExternalCallHelper.PtrToStructure(ptr, ref structure);
			if (structure.nCharNum == 0)
			{
				throw new Exception(AppLocalizationService.GetLang("未检测到字符") + AppLocalizationService.GetLang("!"));
			}
			MVBI_BOX_F[] array = ExternalCallHelper.IntPtr2Structures<MVBI_BOX_F>(structure.pCharBoxList, (int)structure.nCharNum);
			IntPtr[] array2 = ExternalCallHelper.IntPtr2Structures<IntPtr>(structure.pCharList, (int)structure.nCharNum);
			ClearCharacters();
			for (int i = 0; i < structure.nCharNum; i++)
			{
				HikPasterParameterExtern hikPasterParameterExtern = new HikPasterParameterExtern();
				hikPasterParameterExtern.OriginPoint = new Point((double)array[i].center_x - (double)array[i].width / 2.0, (double)array[i].center_y - (double)array[i].height / 2.0);
				hikPasterParameterExtern.Width = array[i].width;
				hikPasterParameterExtern.Height = array[i].height;
				hikPasterParameterExtern.Angle = array[i].angle;
				hikPasterParameterExtern.GroupName = "myGroup";
				CharacterImageData characterImageData = new CharacterImageData();
				OCR_IMAGE structure2 = default;
				OCR_IMAGE structure3 = default;
				IntPtr ptr2 = IntPtr.Zero;
				IntPtr ptr3 = IntPtr.Zero;
				ExternalCallHelper.StructureToPtr(structure2, ref ptr2);
				ExternalCallHelper.StructureToPtr(structure3, ref ptr3);
				OCRManager.OCR_GetGrayImageE(array2[i], ptr2);
				OCRManager.OCR_GetBinaryImageE(array2[i], ptr3);
				ExternalCallHelper.PtrToStructure(ptr2, ref structure2);
				ExternalCallHelper.PtrToStructure(ptr3, ref structure3);
				characterImageData.Width = (int)structure2.nImgWidth;
				characterImageData.Height = (int)structure2.nImgHeight;
				characterImageData.CharacterGrayDataBytes = ExternalCallHelper.IntPtr2Bytes(structure2.pImgData, characterImageData.Width * characterImageData.Height);
				characterImageData.CharacterBinaryDataBytes = ExternalCallHelper.IntPtr2Bytes(structure3.pImgData, characterImageData.Width * characterImageData.Height);
				hikPasterParameterExtern.CharIntPtr = array2[i];
				hikPasterParameterExtern.Tag = characterImageData;
				CharacterList.Add(hikPasterParameterExtern);
				myOCRTrainUIData.ItemsData.Add(hikPasterParameterExtern);
			}
			IsTrainEnable = CharacterList.Count > 0;
			StringBuilder stringBuilder = new StringBuilder();
			for (int j = 0; j < structure.nCharNum; j++)
			{
				CharacterList[j].State = ROIState.Error;
				CharacterList[j].SubContent = "？";
				stringBuilder.Append('？');
			}
			OldString = stringBuilder.ToString();
			CharacterText.Content = OldString;
		}
		catch (Exception ex)
		{
			ClearCharacters();
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
		finally
		{
			ExternalCallHelper.FreeCoTaskMemIntPtr(intPtr);
		}
	}

	private void ClearCharacters()
	{
		foreach (HikPasterParameter character in CharacterList)
		{
			myOCRTrainUIData.ItemsData.Remove(character);
		}
		CharacterList.Clear();
		IsTrainEnable = false;
	}

	private byte[] TranAndSaveLib(string Path)
	{
		byte[] array = TranLib();
		FileStream fileStream = null;
		try
		{
			if (array != null)
			{
				fileStream = new FileStream(Path, FileMode.Create);
				fileStream.Write(array, 0, array.Length);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("字符训练文件操作异常:" + ex.Message);
			throw new Exception(ex.Message);
		}
		finally
		{
			fileStream?.Close();
		}
		return array;
	}

	private byte[] TranLib()
	{
		OCR_TRAIN_INPUT ocrTrainInput = default;
		List<IntPtr> list = new List<IntPtr>();
		int num = 0;
		for (int i = 0; i < myOCRTrainUIData.Collection.Count; i++)
		{
			if (num >= 1000)
			{
				break;
			}
			SetDataToTrainData(myOCRTrainUIData.Collection[i]);
			list.Add(myOCRTrainUIData.Collection[i].CharIntPtr);
			num++;
			if (myOCRTrainUIData.Collection[i].Collection.Count <= 0)
			{
				continue;
			}
			foreach (CharacterItemData item in myOCRTrainUIData.Collection[i].Collection)
			{
				SetDataToTrainData(item);
				list.Add(item.CharIntPtr);
				num++;
			}
		}
		ocrTrainInput.nCharNum = (uint)num;
		Array array = list.ToArray();
		int num2 = (Environment.Is64BitProcess ? 8 : 4);
		byte[] array2 = new byte[array.Length * num2];
		Buffer.BlockCopy(array, 0, array2, 0, array2.Length);
		ocrTrainInput.pCharList = ExternalCallHelper.ArrayToIntptr(array2);
		byte[] result = FontTrain(ocrTrainInput, GetLibData());
		FreeFontTrainInput();
		return result;
	}

	private void SetDataToTrainData(CharacterItemData data)
	{
		OCRManager.OCR_SetSegmentLabelE(data.CharIntPtr, (char)data.ChFontLabel);
	}

	public static void InitMaxOCR()
	{
		int maxCharHeight = 512;
		int maxCharWidth = 512;
		int num = OCRManager.OcrInit(imgMaxWidth, imgMaxHeight, maxCharWidth, maxCharHeight);
		if (num != 0)
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_CalculatorCInitial") + AppLocalizationService.GetLang("失败") + ":" + ErrorCodeInfo.GetErrorInfo(num));
		}
	}

	public void InitDefaultOCR()
	{
		if (IsNeedInitDefaultOCR)
		{
			int maxCharHeight = 0;
			int maxCharWidth = 0;
			GetCharMaxSize(out maxCharWidth, out maxCharHeight);
			int num = OCRManager.OcrInit(imgMaxWidth, imgMaxHeight, maxCharWidth, maxCharHeight);
			if (num != 0)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_CalculatorCInitial") + AppLocalizationService.GetLang("失败") + ":" + ErrorCodeInfo.GetErrorInfo(num));
			}
			IsNeedInitDefaultOCR = false;
		}
	}

	private void Button_Click_Enhance(object sender, RoutedEventArgs e)
	{
		try
		{
			if (LCon.SelectedItems == null || LCon.SelectedItems.Count <= 0)
			{
				return;
			}
			object[] array = new object[LCon.SelectedItems.Count];
			LCon.SelectedItems.CopyTo(array, 0);
			int num = 0;
			object[] array2 = array;
			foreach (object obj in array2)
			{
				num++;
				if (num > MaxEnhanceCount)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MaxEnhanceCount"), MessageIcons.Warning);
					break;
				}
				EnhanceSelectedItem(obj);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	public void EnhanceSelectedItem(object obj, bool AddToParent = false)
	{
		if (!(obj is CharacterItemData characterItemData))
		{
			return;
		}
		int num = 0;
		OCRManager.OCR_ClearEnhanceCharE();
		OCR_ENHANCE_OUTPUT structure = default;
		IntPtr ptr = IntPtr.Zero;
		ExternalCallHelper.StructureToPtr(structure, ref ptr);
		OCRManager.OCR_SetSegmentLabelE(characterItemData.CharIntPtr, (char)characterItemData.ChFontLabel);
		OCRManager.OCR_AddEnhanceCharE(characterItemData.CharIntPtr);
		num = OCRManager.OCR_ProcessEnhanceE(ptr);
		if (num != 0)
		{
			throw new Exception(ErrorCodeInfo.GetErrorInfo(num));
		}
		ExternalCallHelper.PtrToStructure(ptr, ref structure);
		IntPtr[] array = ExternalCallHelper.IntPtr2Structures<IntPtr>(structure.pCharList, (int)structure.nCharNum);
		for (int i = 0; i < structure.nCharNum; i++)
		{
			CharacterItemData characterItemData2 = new CharacterItemData();
			characterItemData2.CImageData = new CharacterImageData();
			OCR_IMAGE structure2 = default;
			OCR_IMAGE structure3 = default;
			IntPtr ptr2 = IntPtr.Zero;
			IntPtr ptr3 = IntPtr.Zero;
			ExternalCallHelper.StructureToPtr(structure2, ref ptr2);
			ExternalCallHelper.StructureToPtr(structure3, ref ptr3);
			OCRManager.OCR_GetGrayImageE(array[i], ptr2);
			OCRManager.OCR_GetBinaryImageE(array[i], ptr3);
			ExternalCallHelper.PtrToStructure(ptr2, ref structure2);
			ExternalCallHelper.PtrToStructure(ptr3, ref structure3);
			characterItemData2.CImageData.Width = (int)structure2.nImgWidth;
			characterItemData2.CImageData.Height = (int)structure2.nImgHeight;
			characterItemData2.CImageData.CharacterGrayDataBytes = ExternalCallHelper.IntPtr2Bytes(structure2.pImgData, characterItemData2.CImageData.Width * characterItemData2.CImageData.Height);
			characterItemData2.CImageData.CharacterBinaryDataBytes = ExternalCallHelper.IntPtr2Bytes(structure3.pImgData, characterItemData2.CImageData.Width * characterItemData2.CImageData.Height);
			byte ch = 0;
			OCRManager.OCR_GetLabelE(array[i], ref ch);
			characterItemData2.ChFontLabel = ch;
			characterItemData2.FontType = GetFontType((char)ch);
			characterItemData2.Parent = characterItemData;
			characterItemData2.CharIntPtr = array[i];
			if (myOCRTrainUIData.GetAllDataCount() >= 1000)
			{
				throw new Exception("Lang_FontSizeErr");
			}
			AddSubItem(characterItemData, characterItemData2, AddToParent);
		}
	}

	private void AddSubItem(CharacterItemData ParentCharacterItemData, CharacterItemData characterItemData, bool AddToParent = false)
	{
		if (characterItemData == null)
		{
			throw new Exception("");
		}
		byte chFontLabel = characterItemData.ChFontLabel;
		uint fontID = GetFontID(chFontLabel);
		if (nFontTranNum[fontID] >= 100)
		{
			throw new Exception(AppLocalizationService.GetLang("样本") + AppLocalizationService.GetLang("数量") + AppLocalizationService.GetLang("越界") + "!");
		}
		nFontTranNum[fontID]++;
		byte[] array = new byte[characterItemData.CImageData.Width * characterItemData.CImageData.Height];
		Buffer.BlockCopy(characterItemData.CImageData.CharacterBinaryDataBytes, 0, array, 0, array.Length);
		ImageData imageData = new ImageData();
		imageData.Width = characterItemData.CImageData.Width;
		imageData.Height = characterItemData.CImageData.Height;
		imageData.ImageBuffer = array;
		imageData.PixelFormat = PixelFormats.Gray8;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] != 0)
			{
				array[i] = byte.MaxValue;
			}
			else
			{
				array[i] = 0;
			}
		}
		ImageSource imageSource = b2SConvertor2.BuiltImageSource(imageData);
		if (imageSource != null)
		{
			characterItemData.Character_ImageSource = imageSource.Clone();
		}
		array = new byte[array.Length];
		Buffer.BlockCopy(characterItemData.CImageData.CharacterGrayDataBytes, 0, array, 0, array.Length);
		for (int j = 0; j < array.Length; j++)
		{
			array[j] = (byte)(255 - array[j]);
		}
		imageData.ImageBuffer = array;
		ImageSource imageSource2 = b2SConvertor2.BuiltImageSource(imageData);
		if (imageSource2 != null)
		{
			characterItemData.Character_GrayImageSource = imageSource2.Clone();
		}
		if (AddToParent)
		{
			if (ParentCharacterItemData.Parent == null)
			{
				return;
			}
			int num = ParentCharacterItemData.Parent.Collection.IndexOf(ParentCharacterItemData);
			if (num >= 0)
			{
				ParentCharacterItemData.Parent.Collection.Insert(num + 1, characterItemData);
				characterItemData.Parent = ParentCharacterItemData.Parent;
				for (int k = num + 1; k < ParentCharacterItemData.Parent.Collection.Count; k++)
				{
					ParentCharacterItemData.Parent.Collection[k].Character_Tilte = $"{(char)ParentCharacterItemData.Parent.Collection[k].ChFontLabel},{k + 1:d}";
				}
			}
		}
		else
		{
			ParentCharacterItemData.Collection.Add(characterItemData);
			characterItemData.Parent = ParentCharacterItemData;
			characterItemData.Character_Tilte = $"{(char)ParentCharacterItemData.ChFontLabel},{ParentCharacterItemData.Collection.Count:d}";
		}
	}

	private void Button_Click_LoadLib(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Filter = "(*.fblib)|*.fblib";
		string text = ConfigurationHelper.INIGetDefaultOpenPath((Root.Owner as IVmModule)?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		if (openFileDialog.ShowDialog() == true)
		{
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), (Root.Owner as IVmModule)?.ModuleDir);
			LoadLib(openFileDialog.FileName);
		}
	}

	private void LoadLib(string path)
	{
		byte[] array = null;
		int num = 0;
		try
		{
			array = EncryptionAndDecryptionHelper.GetLibFile(path);
			if (array == null || array.Length < 4)
			{
				return;
			}
			int num2 = BitConverter.ToInt32(array, num);
			num += 4;
			if (num2 > 0 && array.Length - num2 - 4 > 0)
			{
				byte[] array2 = array.Skip(num).Take(num2).ToArray();
				num += num2;
				byte[] libData = array.Skip(num).ToArray();
				IntPtr inPtr = ExternalCallHelper.Bytes2IntPtr(array, out var hObject);
				int num3 = OCRManager.OCR_ImportFontE(inPtr, array.Length);
				ExternalCallHelper.FreeGCHandle(hObject);
				if (num3 != 0)
				{
					string strMsg = AppLocalizationService.GetLang("读取样本库失败") + " :" + ErrorCodeInfo.GetErrorInfo(num3);
					HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), strMsg, MessageIcons.Error);
				}
				else
				{
					SetLibData(libData);
					CurFilePath = path;
				}
			}
		}
		catch (Exception ex)
		{
			string text = AppLocalizationService.GetLang("读取样本库失败") + " :" + AppLocalizationService.GetLang("读取文件失败");
			LogHelper.Error(text);
			if (ex is EndOfStreamException)
			{
				text = AppLocalizationService.GetLang("读取样本库失败") + " :" + AppLocalizationService.GetLang("Lang_FileContentError") + "!";
			}
			else if (ex is VmException ex2)
			{
				text = AppLocalizationService.GetLang("读取样本库失败") + " :" + ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), text, MessageIcons.Error);
		}
	}

	private static void FreeFontTrainInput()
	{
	}

	public void SetLibData(byte[] buffer)
	{
		try
		{
			myOCRTrainUIData.Collection.Clear();
			FONT_LIBRARY fONT_LIBRARY = CompatibleLib(buffer, out var indexRead);
			nFontTranNum = new uint[1000];
			for (int i = 0; i < fONT_LIBRARY.nFontCount; i++)
			{
				CharacterItemData characterItemData = new CharacterItemData();
				characterItemData.Character_Tilte = "1";
				IntPtr outPtr = IntPtr.Zero;
				OCRManager.OCR_GetCharHandleE(i, ref outPtr);
				characterItemData.CharIntPtr = outPtr;
				characterItemData.CImageData = new CharacterImageData();
				characterItemData.CImageData.Width = (int)fONT_LIBRARY.nFontImgWidth[i];
				characterItemData.CImageData.Height = (int)fONT_LIBRARY.nFontImgHeight[i];
				byte[] array = new byte[fONT_LIBRARY.nGrayFontImgBufLen[i]];
				Buffer.BlockCopy(buffer, indexRead, array, 0, (int)fONT_LIBRARY.nGrayFontImgBufLen[i]);
				indexRead += (int)fONT_LIBRARY.nGrayFontImgBufLen[i];
				characterItemData.CImageData.CharacterGrayDataBytes = array;
				array = new byte[fONT_LIBRARY.nBitFontImgBufLen[i]];
				Buffer.BlockCopy(buffer, indexRead, array, 0, (int)fONT_LIBRARY.nBitFontImgBufLen[i]);
				indexRead += (int)fONT_LIBRARY.nBitFontImgBufLen[i];
				characterItemData.CImageData.CharacterBinaryDataBytes = array;
				characterItemData.ChFontLabel = fONT_LIBRARY.chFontLabel[i];
				characterItemData.FontType = GetFontType((char)fONT_LIBRARY.chFontLabel[i]);
				if (fONT_LIBRARY.nFontTranNum[i] == 0)
				{
					AddItem(characterItemData);
					continue;
				}
				if (myOCRTrainUIData.Collection == null || myOCRTrainUIData.Collection.Count == 0)
				{
					throw new Exception(AppLocalizationService.GetLang("模型数据不兼容"));
				}
				AddSubItem(myOCRTrainUIData.Collection.Last(), characterItemData);
			}
		}
		catch (Exception)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("模型数据异常"), MessageIcons.Error);
		}
	}

	private static FONT_LIBRARY CompatibleLib(byte[] buffer, out int indexRead)
	{
		indexRead = 0;
		FONT_LIBRARY fONT_LIBRARY = default;
		if (buffer.Length >= "v3.1.0 build0411".Length)
		{
			if (true)
			{
				indexRead = Marshal.SizeOf(fONT_LIBRARY);
				return ExternalCallHelper.BytesToStruct<FONT_LIBRARY>(buffer, 0, buffer.Length);
			}
			fONT_LIBRARY.version = "";
			FONT_LIBRARY_OLD structure = ExternalCallHelper.BytesToStruct<FONT_LIBRARY_OLD>(buffer, 0, buffer.Length);
			indexRead = Marshal.SizeOf(structure);
			fONT_LIBRARY.chFontLabel = structure.chFontLabel;
			fONT_LIBRARY.nFontCount = structure.nFontCount;
			fONT_LIBRARY.nFontImgHeight = structure.nFontImgHeight;
			fONT_LIBRARY.nFontImgWidth = structure.nFontImgWidth;
			fONT_LIBRARY.nFontTranNum = structure.nFontTranNum;
			fONT_LIBRARY.nFontType = new MVBOCR_CHARACTER_FIELD_TYPE[1000];
			for (int i = 0; i < structure.nFontType.Length; i++)
			{
				fONT_LIBRARY.nFontType[i].LOW_U64 = (ulong)structure.nFontType[i];
			}
			fONT_LIBRARY.nGrayFontImgBufLen = structure.nGrayFontImgBufLen;
			fONT_LIBRARY.nBitFontImgBufLen = structure.nBitFontImgBufLen;
			fONT_LIBRARY.chFontLabel = structure.chFontLabel;
		}
		return fONT_LIBRARY;
	}

	private static byte[] FontTrain(OCR_TRAIN_INPUT ocrTrainInput, byte[] libData)
	{
		if (libData == null)
		{
			return null;
		}
		OCR_TRAIN_OUTPUT? oCR_TRAIN_OUTPUT = AlgorithmProcessTrain(ocrTrainInput);
		if (!oCR_TRAIN_OUTPUT.HasValue)
		{
			return null;
		}
		return ExternalCallHelper.IntPtr2Bytes(oCR_TRAIN_OUTPUT.Value.pFontData, (int)oCR_TRAIN_OUTPUT.Value.nFontDataLen);
	}

	private static OCR_TRAIN_OUTPUT? AlgorithmProcessTrain(OCR_TRAIN_INPUT ocrTrainInput)
	{
		int num = 0;
		if (ocrTrainInput.nCharNum < 1 || ocrTrainInput.nCharNum > 1000)
		{
			LogHelper.Error("字符训练输入信息错误，CharNum = " + ocrTrainInput.nCharNum);
			return null;
		}
		OCR_TRAIN_OUTPUT structure = default;
		IntPtr zero = IntPtr.Zero;
		IntPtr ptr = IntPtr.Zero;
		ExternalCallHelper.StructureToPtr(structure, ref ptr);
		zero = ExternalCallHelper.StructureToPtr(ocrTrainInput);
		num = OCRManager.OCR_TrainE(zero, ptr);
		ExternalCallHelper.FreeCoTaskMemIntPtr(zero);
		if (num != 0)
		{
			LogHelper.Error("字符训练失败，错误码：" + num);
			return null;
		}
		ExternalCallHelper.PtrToStructure(ptr, ref structure);
		if (structure.nFontDataLen < 1)
		{
			LogHelper.Error("字符训练失败，字库长度=" + structure.nFontDataLen);
			return null;
		}
		return structure;
	}

	private static void AlgLibDeinit()
	{
		IsNeedInitDefaultOCR = true;
		OCRManager.OcrDeInit();
	}

	private void UserControl_Unloaded(object sender, RoutedEventArgs e)
	{
		DeInit();
		if (_RangCharWidth is NameValueItem nameValueItem)
		{
			nameValueItem.SubscribeVisible = true;
		}
		if (_RangCharHeight is NameValueItem nameValueItem2)
		{
			nameValueItem2.SubscribeVisible = true;
		}
		if (_BinaryCoef is NameValueItem nameValueItem3)
		{
			nameValueItem3.SubscribeVisible = true;
		}
		if (_MainLineDistThresh is NameValueItem nameValueItem4)
		{
			nameValueItem4.SubscribeVisible = true;
		}
		if (_OrientHalfRange is NameValueItem nameValueItem5)
		{
			nameValueItem5.SubscribeVisible = true;
		}
		if (_SlantHalfRange is NameValueItem nameValueItem6)
		{
			nameValueItem6.SubscribeVisible = true;
		}
		if (_MinInterTextGap is NameValueItem nameValueItem7)
		{
			nameValueItem7.SubscribeVisible = true;
		}
		if (_MinInterCharGap is NameValueItem nameValueItem8)
		{
			nameValueItem8.SubscribeVisible = true;
		}
		if (_MaxLengthWidthRatio is NameValueItem nameValueItem9)
		{
			nameValueItem9.SubscribeVisible = true;
		}
		if (_RangStrokeWidth is NameValueItem nameValueItem10)
		{
			nameValueItem10.SubscribeVisible = true;
		}
		if (_RangCharArea is NameValueItem nameValueItem11)
		{
			nameValueItem11.SubscribeVisible = true;
		}
	}

	private void DeInit()
	{
		AlgLibDeinit();
		if (MaxCharHeight != null)
		{
			PropertyChangedEventManager.RemoveHandler(MaxCharHeight, PropertyChanged_MaxCharSize, "CurValueA");
		}
		if (MaxCharWidth != null)
		{
			PropertyChangedEventManager.RemoveHandler(MaxCharWidth, PropertyChanged_MaxCharSize, "CurValueA");
		}
	}

	private void Button_Click_Add2Lib(object sender, RoutedEventArgs e)
	{
		try
		{
			int num = -1;
			for (int i = 0; i < CharacterList.Count; i++)
			{
				if (CharacterList[i].IsSelected)
				{
					num = i;
					break;
				}
			}
			if (num >= 0)
			{
				CharacterItemData characterItemData = SetItemByParam(CharacterList[num]);
				if (characterItemData != null)
				{
					AddItem(characterItemData);
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	private CharacterItemData SetItemByParam(HikPasterParameter hikPasterParameter)
	{
		if (hikPasterParameter.SubContent.Length < 1)
		{
			return null;
		}
		CharacterItemData characterItemData = new CharacterItemData();
		characterItemData.CImageData = hikPasterParameter.Tag as CharacterImageData;
		characterItemData.ChFontLabel = (byte)hikPasterParameter.SubContent[0];
		characterItemData.FontType = GetFontType(hikPasterParameter.SubContent[0]);
		characterItemData.CharIntPtr = (hikPasterParameter as HikPasterParameterExtern)?.CharIntPtr ?? IntPtr.Zero;
		if (myOCRTrainUIData.GetAllDataCount() >= 1000)
		{
			throw new Exception("Lang_FontSizeErr");
		}
		return characterItemData;
	}

	private void AddItem(CharacterItemData characterItemData)
	{
		if (characterItemData == null)
		{
			throw new Exception("");
		}
		byte chFontLabel = characterItemData.ChFontLabel;
		uint fontID = GetFontID(chFontLabel);
		if (nFontTranNum[fontID] >= 100)
		{
			throw new Exception(AppLocalizationService.GetLang("样本") + AppLocalizationService.GetLang("数量") + AppLocalizationService.GetLang("越界") + "!");
		}
		nFontTranNum[fontID]++;
		int num = 0;
		for (int i = 0; i < myOCRTrainUIData.Collection.Count; i++)
		{
			if (myOCRTrainUIData.Collection[i].ChFontLabel > chFontLabel)
			{
				num = i;
				break;
			}
			if (i == myOCRTrainUIData.Collection.Count - 1)
			{
				num = myOCRTrainUIData.Collection.Count;
			}
		}
		byte[] array = new byte[characterItemData.CImageData.Width * characterItemData.CImageData.Height];
		Buffer.BlockCopy(characterItemData.CImageData.CharacterBinaryDataBytes, 0, array, 0, array.Length);
		ImageData imageData = new ImageData();
		imageData.Width = characterItemData.CImageData.Width;
		imageData.Height = characterItemData.CImageData.Height;
		imageData.ImageBuffer = array;
		imageData.PixelFormat = PixelFormats.Gray8;
		for (int j = 0; j < array.Length; j++)
		{
			if (array[j] != 0)
			{
				array[j] = byte.MaxValue;
			}
			else
			{
				array[j] = 0;
			}
		}
		characterItemData.Character_ImageSource = b2SConvertor2.BuiltImageSource(imageData)?.Clone();
		array = new byte[array.Length];
		Buffer.BlockCopy(characterItemData.CImageData.CharacterGrayDataBytes, 0, array, 0, array.Length);
		for (int k = 0; k < array.Length; k++)
		{
			array[k] = (byte)(255 - array[k]);
		}
		imageData.ImageBuffer = array;
		characterItemData.Character_GrayImageSource = b2SConvertor2.BuiltImageSource(imageData)?.Clone();
		PropertyChangedEventManager.RemoveHandler(characterItemData, Character_LabelChanged, "ChFontLabelChar");
		PropertyChangedEventManager.AddHandler(characterItemData, Character_LabelChanged, "ChFontLabelChar");
		myOCRTrainUIData.Collection.Insert(num, characterItemData);
		LCon.SelectedIndex = num;
		int num2 = 1;
		int num3 = num - 1;
		while (num3 >= 0 && myOCRTrainUIData.Collection[num].ChFontLabel == myOCRTrainUIData.Collection[num3].ChFontLabel)
		{
			num2++;
			num3--;
		}
		characterItemData.Character_Tilte = $"{(char)myOCRTrainUIData.Collection[num].ChFontLabel},{num2:d}";
	}

	private void Character_LabelChanged(object sender, PropertyChangedEventArgs e)
	{
		AfterAlterCharacter(sender as CharacterItemData);
	}

	private static MVBOCR_CHARACTER_FIELD_TYPE GetFontType(char str)
	{
		MVBOCR_CHARACTER_FIELD_TYPE result = default;
		if (str >= '0' && str <= '9')
		{
			result.LOW_U64 = (ulong)(1L << str - 48);
		}
		else if (str >= 'A' && str <= 'Z')
		{
			result.LOW_U64 = (ulong)(1L << str - 65 + 10);
		}
		else if (str >= 'a' && str <= 'z')
		{
			result.LOW_U64 = (ulong)(1L << str - 97 + 10 + 26);
		}
		else
		{
			switch (str)
			{
			case ' ':
				result.HIGH_U64 = 1uL;
				break;
			case '[':
				result.HIGH_U64 = 2uL;
				break;
			case ']':
				result.HIGH_U64 = 4uL;
				break;
			case '+':
				result.HIGH_U64 = 8uL;
				break;
			case '-':
				result.HIGH_U64 = 16uL;
				break;
			case '#':
				result.HIGH_U64 = 32uL;
				break;
			case '$':
				result.HIGH_U64 = 64uL;
				break;
			case '(':
				result.HIGH_U64 = 128uL;
				break;
			case ')':
				result.HIGH_U64 = 256uL;
				break;
			case '@':
				result.HIGH_U64 = 512uL;
				break;
			case '=':
				result.HIGH_U64 = 1024uL;
				break;
			case '_':
				result.HIGH_U64 = 2048uL;
				break;
			case '!':
				result.HIGH_U64 = 4096uL;
				break;
			case '?':
				result.HIGH_U64 = 8192uL;
				break;
			case ',':
				result.HIGH_U64 = 16384uL;
				break;
			case ':':
				result.HIGH_U64 = 32768uL;
				break;
			case ';':
				result.HIGH_U64 = 65536uL;
				break;
			case '/':
				result.HIGH_U64 = 131072uL;
				break;
			case '.':
				result.HIGH_U64 = 262144uL;
				break;
			case '%':
				result.HIGH_U64 = 524288uL;
				break;
			case '&':
				result.HIGH_U64 = 1048576uL;
				break;
			case '"':
				result.HIGH_U64 = 2097152uL;
				break;
			case '\'':
				result.HIGH_U64 = 4194304uL;
				break;
			case '*':
				result.HIGH_U64 = 8388608uL;
				break;
			case '<':
				result.HIGH_U64 = 16777216uL;
				break;
			case '>':
				result.HIGH_U64 = 33554432uL;
				break;
			case '\\':
				result.HIGH_U64 = 67108864uL;
				break;
			case '^':
				result.HIGH_U64 = 134217728uL;
				break;
			case '`':
				result.HIGH_U64 = 268435456uL;
				break;
			case '{':
				result.HIGH_U64 = 536870912uL;
				break;
			case '|':
				result.HIGH_U64 = 1073741824uL;
				break;
			case '}':
				result.HIGH_U64 = 2147483648uL;
				break;
			case '~':
				result.HIGH_U64 = 4294967296uL;
				break;
			}
		}
		return result;
	}

	private void Button_Click_5(object sender, RoutedEventArgs e)
	{
		if (myOCRTrainUIData.Collection == null || myOCRTrainUIData.Collection.Count <= 0)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("Lang_LackSaptoSave"), MessageIcons.Error);
			return;
		}
		System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
		saveFileDialog.Filter = "(*.fblib)|*.fblib";
		saveFileDialog.RestoreDirectory = true;
		string text = ConfigurationHelper.INIGetDefaultOpenPath((Root.Owner as IVmModule)?.ModuleDir);
		string text2 = "";
		if (text2 != null)
		{
			saveFileDialog.InitialDirectory = text2;
		}
		try
		{
			if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), (Root.Owner as IVmModule)?.ModuleDir);
				if (TranAndSaveLib(saveFileDialog.FileName) != null)
				{
					HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
				}
				else
				{
					HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang("Savefailed"), MessageIcons.Error);
				}
			}
		}
		catch (Exception ex)
		{
			string strMsg = ex.Message;
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("OcR"), strMsg, MessageIcons.Error);
		}
		finally
		{
			saveFileDialog.Dispose();
		}
	}

	public byte[] GetLibData()
	{
		if (myOCRTrainUIData.Collection == null || myOCRTrainUIData.Collection.Count <= 0)
		{
			return null;
		}
		FONT_LIBRARY fONT_LIBRARY = new FONT_LIBRARY
		{
			version = "v3.1.0 build0411",
			chFontLabel = new byte[1000],
			nFontImgWidth = new uint[1000],
			nFontImgHeight = new uint[1000],
			nFontType = new MVBOCR_CHARACTER_FIELD_TYPE[1000],
			nGrayFontImgBufLen = new uint[1000],
			nBitFontImgBufLen = new uint[1000],
			nFontTranNum = new uint[1000]
		};
		MemoryStream memoryStream = new MemoryStream();
		int num = 0;
		for (int i = 0; i < myOCRTrainUIData.Collection.Count; i++)
		{
			SetDataToLib(memoryStream, fONT_LIBRARY, num, myOCRTrainUIData.Collection[i]);
			num++;
			if (myOCRTrainUIData.Collection[i].Collection.Count <= 0)
			{
				continue;
			}
			foreach (CharacterItemData item in myOCRTrainUIData.Collection[i].Collection)
			{
				SetDataToLib(memoryStream, fONT_LIBRARY, num, item);
				fONT_LIBRARY.nFontTranNum[num] = 1u;
				num++;
			}
		}
		fONT_LIBRARY.nFontCount = (uint)num;
		MemoryStream memoryStream2 = new MemoryStream();
		byte[] array = ExternalCallHelper.StructToBytes(fONT_LIBRARY, Marshal.SizeOf(fONT_LIBRARY));
		memoryStream2.Write(array, 0, array.Length);
		memoryStream.WriteTo(memoryStream2);
		byte[] buffer = memoryStream2.GetBuffer();
		memoryStream.Flush();
		memoryStream.Close();
		memoryStream2.Flush();
		memoryStream2.Close();
		return buffer;
	}

	private void SetDataToLib(MemoryStream memoryStream, FONT_LIBRARY m_stFontLib, int nIdx, CharacterItemData characterItemData)
	{
		m_stFontLib.nFontImgWidth[nIdx] = (uint)characterItemData.CImageData.Width;
		m_stFontLib.nFontImgHeight[nIdx] = (uint)characterItemData.CImageData.Height;
		m_stFontLib.nFontType[nIdx] = characterItemData.FontType;
		m_stFontLib.chFontLabel[nIdx] = characterItemData.ChFontLabel;
		m_stFontLib.nGrayFontImgBufLen[nIdx] = (uint)characterItemData.CImageData.CharacterGrayDataBytes.Length;
		memoryStream.Write(characterItemData.CImageData.CharacterGrayDataBytes, 0, characterItemData.CImageData.CharacterGrayDataBytes.Length);
		m_stFontLib.nBitFontImgBufLen[nIdx] = (uint)characterItemData.CImageData.CharacterBinaryDataBytes.Length;
		memoryStream.Write(characterItemData.CImageData.CharacterBinaryDataBytes, 0, characterItemData.CImageData.CharacterBinaryDataBytes.Length);
	}

	private void DeleteAltedItem(CharacterItemData characterItemData)
	{
		char c = characterItemData.Character_Tilte.Split(',')[0][0];
		int num = int.Parse(characterItemData.Character_Tilte.Split(',')[1]);
		int num2 = myOCRTrainUIData.Collection.IndexOf(characterItemData);
		for (int i = num2 + 1; i < myOCRTrainUIData.Collection.Count; i++)
		{
			CharacterItemData characterItemData2 = myOCRTrainUIData.Collection[i];
			if (characterItemData2.Character_Tilte.Split(',')[0][0] == c)
			{
				characterItemData2.Character_Tilte = $"{c},{num:d}";
				num++;
				continue;
			}
			break;
		}
		uint fontID = GetFontID((byte)c);
		nFontTranNum[fontID]--;
		myOCRTrainUIData.Collection.Remove(characterItemData);
	}

	private void DeleteSelectedItem(object item)
	{
		if (!myOCRTrainUIData.Collection.Contains(item))
		{
			return;
		}
		CharacterItemData characterItemData = item as CharacterItemData;
		char c = characterItemData.Character_Tilte.Split(',')[0][0];
		int num = int.Parse(characterItemData.Character_Tilte.Split(',')[1]);
		for (int i = LCon.SelectedIndex + 1; i < myOCRTrainUIData.Collection.Count; i++)
		{
			CharacterItemData characterItemData2 = myOCRTrainUIData.Collection[i];
			if (characterItemData2.Character_Tilte.Split(',')[0][0] == c)
			{
				characterItemData2.Character_Tilte = $"{c},{num:d}";
				num++;
				continue;
			}
			break;
		}
		uint fontID = GetFontID(characterItemData.ChFontLabel);
		nFontTranNum[fontID]--;
		if (characterItemData.Collection.Count > 0)
		{
			foreach (CharacterItemData item2 in characterItemData.Collection)
			{
				fontID = GetFontID(item2.ChFontLabel);
				nFontTranNum[fontID]--;
			}
		}
		myOCRTrainUIData.Collection.Remove(characterItemData);
	}

	private void LCon_DragEnter(object sender, System.Windows.DragEventArgs e)
	{
		if (!(e.Data?.GetData(typeof(HikPasterParameter)) is HikPasterParameter { State: not ROIState.Error }))
		{
			e.Effects = System.Windows.DragDropEffects.None;
		}
		e.Handled = true;
	}

	private void LCon_DragOver(object sender, System.Windows.DragEventArgs e)
	{
		if (!(e.Data.GetData(typeof(HikPasterParameter)) is HikPasterParameter { State: not ROIState.Error }))
		{
			e.Effects = System.Windows.DragDropEffects.None;
		}
		e.Handled = true;
	}

	private void LCon_Drop(object sender, System.Windows.DragEventArgs e)
	{
		try
		{
			if (e.Data?.GetData(typeof(HikPasterParameter)) is HikPasterParameter itemByParam)
			{
				CharacterItemData characterItemData = SetItemByParam(itemByParam);
				if (characterItemData != null)
				{
					AddItem(characterItemData);
				}
			}
			e.Handled = true;
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	private void LCon_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (Key.Delete == e.Key)
		{
			DeleteSelectedItem();
		}
	}

	private void DeleteSelectedItem()
	{
		if (LCon.SelectedItems != null)
		{
			object[] array = new object[LCon.SelectedItems.Count];
			LCon.SelectedItems.CopyTo(array, 0);
			object[] array2 = array;
			foreach (object item in array2)
			{
				DeleteSelectedItem(item);
			}
		}
	}

	private void AfterAlterCharacter(CharacterItemData characterItemData)
	{
		try
		{
			if (characterItemData != null && myOCRTrainUIData.Collection.Contains(characterItemData))
			{
				DeleteAltedItem(characterItemData);
				characterItemData.FontType = GetFontType(characterItemData.ChFontLabelChar[0]);
				for (int i = 0; i < characterItemData.Collection.Count; i++)
				{
					CharacterItemData characterItemData2 = characterItemData.Collection[i];
					characterItemData2.ChFontLabelChar = characterItemData.ChFontLabelChar;
					characterItemData2.FontType = characterItemData.FontType;
					characterItemData2.Character_Tilte = $"{(char)characterItemData2.ChFontLabel},{i:d}";
				}
				AddItem(characterItemData);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	public static uint GetFontID(byte tchar)
	{
		return tchar;
	}

	private void Button_Click_Train(object sender, RoutedEventArgs e)
	{
		try
		{
			if (CharacterList.Count < 1)
			{
				throw new Exception(AppLocalizationService.GetLang("Character Training") + AppLocalizationService.GetLang("为空") + "!");
			}
			if (CurImage == null)
			{
				throw new Exception(AppLocalizationService.GetLang("未加载") + AppLocalizationService.GetLang("图片") + "!");
			}
			GetTrainCharacter getTrainCharacter = new GetTrainCharacter();
			HikCoverRoiParameter hikCoverRoiParameter = null;
			OCRTrainUIData oCRTrainUIData = new OCRTrainUIData();
			foreach (object itemsDatum in myOCRTrainUIData.ItemsData)
			{
				if (itemsDatum is HikCoverRoiParameter)
				{
					hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
					break;
				}
			}
			if (hikCoverRoiParameter == null)
			{
				getTrainCharacter.CurImage = CurImage;
				foreach (HikPasterParameter character in CharacterList)
				{
					HikPasterParameterExtern hikPasterParameterExtern = new HikPasterParameterExtern
					{
						Angle = character.Angle,
						Width = character.Width,
						Height = character.Height,
						State = character.State,
						GroupName = character.GroupName,
						SubContent = character.SubContent,
						OriginPoint = character.OriginPoint,
						IsFocusble = true
					};
					hikPasterParameterExtern.Tag = character;
					hikPasterParameterExtern.CharIntPtr = (character as HikPasterParameterExtern)?.CharIntPtr ?? IntPtr.Zero;
					oCRTrainUIData.ItemsData.Add(hikPasterParameterExtern);
				}
			}
			else
			{
				Rect rect = new Rect((int)hikCoverRoiParameter.OriginPoint.X, (int)hikCoverRoiParameter.OriginPoint.Y, (int)hikCoverRoiParameter.Width, (int)hikCoverRoiParameter.Height);
				Rotation(CurImage, rect, 0.0 - hikCoverRoiParameter.Angle, out var dstBmp);
				getTrainCharacter.CurImage = dstBmp;
				foreach (HikPasterParameter character2 in CharacterList)
				{
					HikPasterParameterExtern hikPasterParameterExtern2 = new HikPasterParameterExtern
					{
						Angle = character2.Angle - hikCoverRoiParameter.Angle,
						Width = character2.Width,
						Height = character2.Height,
						State = character2.State,
						GroupName = character2.GroupName,
						SubContent = character2.SubContent,
						IsFocusble = true
					};
					double width = character2.Width;
					double height = character2.Height;
					Point point = new Point(character2.OriginPoint.X + width / 2.0, character2.OriginPoint.Y + height / 2.0);
					RotateTransform rotateTransform = new RotateTransform();
					rotateTransform.CenterX = rect.X + rect.Width / 2.0;
					rotateTransform.CenterY = rect.Y + rect.Height / 2.0;
					rotateTransform.Angle = 0.0 - hikCoverRoiParameter.Angle;
					Point point2 = rotateTransform.Transform(point);
					hikPasterParameterExtern2.OriginPoint = new Point(point2.X - width / 2.0 - rect.X, point2.Y - height / 2.0 - rect.Y);
					hikPasterParameterExtern2.Tag = character2;
					hikPasterParameterExtern2.CharIntPtr = (character2 as HikPasterParameterExtern)?.CharIntPtr ?? IntPtr.Zero;
					oCRTrainUIData.ItemsData.Add(hikPasterParameterExtern2);
				}
			}
			(oCRTrainUIData.ItemsData[0] as HikPasterParameterExtern).IsSelected = true;
			getTrainCharacter.DataContext = oCRTrainUIData;
			if (oCRTrainUIData.ItemsData != null)
			{
				int num = 0;
				int num2 = 0;
				foreach (object itemsDatum2 in oCRTrainUIData.ItemsData)
				{
					if (itemsDatum2 is HikPasterParameterExtern && !string.IsNullOrEmpty((itemsDatum2 as HikPasterParameterExtern).SubContent))
					{
						PropertyChangedEventManager.AddHandler(itemsDatum2 as HikPasterParameterExtern, getTrainCharacter.PasterPropertyChanged, "SubContent");
						(itemsDatum2 as HikPasterParameterExtern).IsChosen = (itemsDatum2 as HikPasterParameterExtern).SubContent[0] == '？';
						num++;
						if ((itemsDatum2 as HikPasterParameterExtern).IsChosen)
						{
							num2++;
						}
					}
				}
				if (num == num2 && num != 0)
				{
					getTrainCharacter.SelectAll.IsChecked = true;
				}
			}
			if (getTrainCharacter.ShowDialog() != true)
			{
				return;
			}
			if (oCRTrainUIData.ItemsData != null)
			{
				foreach (object itemsDatum3 in oCRTrainUIData.ItemsData)
				{
					if (itemsDatum3 is HikPasterParameterExtern)
					{
						((itemsDatum3 as HikPasterParameterExtern).Tag as HikPasterParameterExtern).SubContent = (itemsDatum3 as HikPasterParameterExtern).SubContent;
						((itemsDatum3 as HikPasterParameterExtern).Tag as HikPasterParameterExtern).State = (itemsDatum3 as HikPasterParameterExtern).State;
					}
				}
			}
			foreach (HikPasterParameterExtern itemsDatum4 in oCRTrainUIData.ItemsData)
			{
				if (itemsDatum4.IsChosen && itemsDatum4.SubContent[0] != '？')
				{
					CharacterItemData characterItemData = SetItemByParam(itemsDatum4.Tag as HikPasterParameter);
					if (characterItemData != null)
					{
						AddItem(characterItemData);
					}
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner as Window, AppLocalizationService.GetLang("OcR"), AppLocalizationService.GetLang(ex.Message), MessageIcons.Error);
		}
	}

	public unsafe static bool Rotation(ImageData oriImage, Rect rect, double angle, out ImageData dstBmp)
	{
		if (oriImage == null)
		{
			dstBmp = null;
			return false;
		}
		dstBmp = new ImageData
		{
			PixelFormat = PixelFormats.Gray8,
			Width = (int)rect.Width,
			Height = (int)rect.Height,
			ImageBuffer = new byte[(int)rect.Width * (int)rect.Height]
		};
		RotateTransform rotateTransform = new RotateTransform();
		rotateTransform.CenterX = rect.X + rect.Width / 2.0;
		rotateTransform.CenterY = rect.Y + rect.Height / 2.0;
		rotateTransform.Angle = 0.0 - angle;
		fixed (byte* imageBuffer = oriImage.ImageBuffer)
		{
			fixed (byte* imageBuffer2 = dstBmp.ImageBuffer)
			{
				for (int i = 0; i < dstBmp.Height; i++)
				{
					for (int j = 0; j < dstBmp.Width; j++)
					{
						Point point = rotateTransform.Transform(new Point((double)j + rect.X, (double)i + rect.Y));
						if (point.Y < 1.0 || point.Y >= (double)(oriImage.Height - 1) || point.X < 1.0 || point.X >= (double)(oriImage.Width - 1))
						{
							(imageBuffer2 + i * dstBmp.Width)[j] = 150;
							continue;
						}
						int num = (int)point.X;
						int num2 = (int)point.Y;
						double num3 = point.X - (double)num;
						double num4 = point.Y - (double)num2;
						(imageBuffer2 + i * dstBmp.Width)[j] = (byte)((1.0 - num3) * ((1.0 - num4) * (double)(int)(imageBuffer + num2 * oriImage.Width)[num] + num4 * (double)(int)(imageBuffer + (num2 + 1) * oriImage.Width)[num]) + num3 * ((1.0 - num4) * (double)(int)(imageBuffer + num2 * oriImage.Width + num)[1] + num4 * (double)(int)(imageBuffer + (num2 + 1) * oriImage.Width + num)[1]));
					}
				}
			}
		}
		return true;
	}

	private void count_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: CharacterItemData dataContext } frameworkElement && dataContext.Collection.Count > 0)
		{
			OCRSubCollection oCRSubCollection = new OCRSubCollection();
			oCRSubCollection.DataContext = frameworkElement.DataContext;
			oCRSubCollection.parentControl = this;
			oCRSubCollection.ShowDialog();
		}
	}

	private void LCon_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is System.Windows.Controls.ListView { SelectedItems: not null } listView)
		{
			SelectedItemsCount = listView.SelectedItems.Count;
		}
	}
}
