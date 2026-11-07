using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlUI;
using Hik.Xml;
using HikPcUI.ImageView;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.ModuleControl.Controls.FeatureMatch;
using VM.PlatformSDKCS;
using VM.Utility;
using VMBaseControls;
using VMBaseControls.Base.ImageView;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace IMVSMarkInspModuVAControl;

public partial class OCRLocationModelPage : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public byte[] ModelData;

	public List<object> needSkipData = new List<object>();

	private System.Windows.Controls.Image tempImage = new System.Windows.Controls.Image();

	private bool IsSetCrossByManual = false;

	private List<string> history = new List<string>();

	private int historyIndex = -1;

	private RubberControl rubber;

	private Byte2ImageSource MaskSourceConvertor = new Byte2ImageSource();

	private object lockobj = new object();

	private int _PanRectWidth = 10;

	private FeaturepatMatchDialogData featurepatMatchDialogData;

	private bool _IsInit = false;

	private bool _IsDisplaySearchParam = false;

	private bool _IsEnterROIMode = false;

	private bool _IsHasCurImage = false;

	private bool _IsGenerateModel = false;

	private bool _IsSettingSelectBox = false;

	private bool _selectbox = false;

	private HikCoverRoiParameter _createRoi = null;

	private HikCoverRoiParameter _inheritRoi = null;

	private ImageData _createImage;

	private ImageData _inheritImage;

	private string _curSelectModelName = "";

	private bool _IsCreateModel = false;

	private bool _IsInheritModel = false;

	private MVBI_BOX_F[] _inheritBox = null;

	private bool _isDetectTarget = false;

	private bool _isFinePositioning = false;

	private bool _isAddMask = false;

	private bool _isStatisticsTrain = false;

	public static readonly DependencyProperty CurModelTypeProperty = DependencyProperty.Register("CurModelType", typeof(RoughModelType), typeof(OCRLocationModelPage), new PropertyMetadata(RoughModelType.Create));

	public static readonly DependencyProperty IsDrawRectProperty = DependencyProperty.Register("IsDrawRect", typeof(bool), typeof(OCRLocationModelPage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectEnableProperty = DependencyProperty.Register("RectEnable", typeof(bool), typeof(OCRLocationModelPage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectRoisProperty = DependencyProperty.Register("RectRois", typeof(ObservableCollection<object>), typeof(OCRLocationModelPage), new PropertyMetadata(new ObservableCollection<object>()));

	public static readonly DependencyProperty SelectedBoxProperty = DependencyProperty.Register("SelectedBox", typeof(HikCoverRoiParameter), typeof(OCRLocationModelPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty IsStateForDrawROIProperty = DependencyProperty.Register("IsStateForDrawROI", typeof(bool), typeof(OCRLocationModelPage), new PropertyMetadata(false));

	public static readonly DependencyProperty DrawROITypeProperty = DependencyProperty.Register("DrawROIType", typeof(Type), typeof(OCRLocationModelPage), new PropertyMetadata(null));

	public static readonly DependencyProperty SelectedROIListProperty = DependencyProperty.Register("SelectedROIList", typeof(IList), typeof(OCRLocationModelPage), new PropertyMetadata(null));

	private List<IList> _ROILists;

	private ObservableCollection<object> shapeList = new ObservableCollection<object>();

	private List<string> modelTypeOptions = new List<string>
	{
		AppLocalizationService.GetLang("Lang_DirectCreate"),
		AppLocalizationService.GetLang("RunParam_Inherit")
	};

	private List<string> roughModelList = new List<string>();

	private ICommand _AddROICommand;

	private ICommand _RoiSelectedCommand = null;

	internal ResultOverviewPopupButton setBtn;

	internal ParamListView paramSearchUI;

	public RoughModelType CurModelType
	{
		get
		{
			return (RoughModelType)GetValue(CurModelTypeProperty);
		}
		set
		{
			SetValue(CurModelTypeProperty, value);
		}
	}

	public bool IsDrawRect
	{
		get
		{
			return (bool)GetValue(IsDrawRectProperty);
		}
		set
		{
			SetValue(IsDrawRectProperty, value);
		}
	}

	public bool RectEnable
	{
		get
		{
			return (bool)GetValue(RectEnableProperty);
		}
		set
		{
			SetValue(RectEnableProperty, value);
		}
	}

	public ObservableCollection<object> RectRois
	{
		get
		{
			return (ObservableCollection<object>)GetValue(RectRoisProperty);
		}
		set
		{
			SetValue(RectRoisProperty, value);
		}
	}

	public HikCoverRoiParameter SelectedBox
	{
		get
		{
			return (HikCoverRoiParameter)GetValue(SelectedBoxProperty);
		}
		set
		{
			SetValue(SelectedBoxProperty, value);
		}
	}

	public bool IsStateForDrawROI
	{
		get
		{
			return (bool)GetValue(IsStateForDrawROIProperty);
		}
		set
		{
			SetValue(IsStateForDrawROIProperty, value);
		}
	}

	public Type DrawROIType
	{
		get
		{
			return (Type)GetValue(DrawROITypeProperty);
		}
		set
		{
			SetValue(DrawROITypeProperty, value);
		}
	}

	public IList SelectedROIList
	{
		get
		{
			return (IList)GetValue(SelectedROIListProperty);
		}
		set
		{
			SetValue(SelectedROIListProperty, value);
		}
	}

	public object ROILists
	{
		get
		{
			if (_ROILists == null)
			{
				FeaturepatMatchDialogData val = featurepatMatchDialogData;
				if (((val != null) ? ((FeaturepatMatchDataBase)val).ItemsData : null) != null)
				{
					RebuildROILists();
				}
			}
			return _ROILists;
		}
	}

	public ObservableCollection<object> ShapeList => shapeList;

	public ImageData CreateImage
	{
		get
		{
			return _createImage;
		}
		set
		{
			_createImage = value;
			if (_createImage != null)
			{
				ShowImage();
				historyIndex = -1;
				RecordHistory();
				NotifyPropertyChanged("BackEnable");
				NotifyPropertyChanged("ForwardEnable");
			}
		}
	}

	public ImageData InheritImage
	{
		get
		{
			return _inheritImage;
		}
		set
		{
			try
			{
				_inheritImage = value;
				if (_inheritImage != null)
				{
					roiImageViewBox.ImageDataSource = _inheritImage;
				}
			}
			catch
			{
			}
		}
	}

	public int PanRectWidth
	{
		get
		{
			return _PanRectWidth;
		}
		set
		{
			if (_PanRectWidth != value)
			{
				if (CreateImage != null && (value > CreateImage.Width || value > CreateImage.Height))
				{
					_PanRectWidth = Convert.ToInt32((CreateImage.Width > CreateImage.Height) ? CreateImage.Height : CreateImage.Width);
					NotifyPropertyChanged("PanRectWidth");
				}
				else
				{
					_PanRectWidth = value;
					NotifyPropertyChanged("PanRectWidth");
				}
			}
		}
	}

	public bool IsDisplaySearchParam
	{
		get
		{
			return _IsDisplaySearchParam;
		}
		set
		{
			if (_IsDisplaySearchParam != value)
			{
				_IsDisplaySearchParam = value;
				NotifyPropertyChanged("IsDisplaySearchParam");
			}
		}
	}

	public bool IsEnterROIMode
	{
		get
		{
			return _IsEnterROIMode;
		}
		set
		{
			if (_IsEnterROIMode == value)
			{
				return;
			}
			_IsEnterROIMode = value;
			if (_IsEnterROIMode)
			{
				roiImageViewBox.ClearExternShapes();
				ShapeList.Clear();
				if (CurModelType == RoughModelType.Create)
				{
					roiImageViewBox.ImageDataSource = imageViewBox.ImageDataSource as ImageData;
					RefreshPoints();
					if (_createRoi != null)
					{
						RectRois.Add(_createRoi);
					}
				}
				else if (CurModelType == RoughModelType.Inherit && _inheritRoi != null)
				{
					RectRois.Add(_inheritRoi);
				}
			}
			else
			{
				if (CurModelType == RoughModelType.Create)
				{
					if (RectRois.Count > 0)
					{
						_createRoi = RectRois[0] as HikCoverRoiParameter;
						_createRoi.IsSelected = false;
					}
					else
					{
						_createRoi = null;
					}
				}
				else if (CurModelType == RoughModelType.Inherit)
				{
					if (RectRois.Count > 0)
					{
						_inheritRoi = RectRois[0] as HikCoverRoiParameter;
						_inheritRoi.IsSelected = false;
					}
					else
					{
						_inheritRoi = null;
					}
					List<object> rects = OCRTrainDataMgr.Instance.GetRects(_inheritBox);
					if (rects != null)
					{
						foreach (object item in rects)
						{
							ShapeList.Add(item);
						}
					}
				}
				RectRois.Clear();
				SelectedBox = null;
			}
			RebuildROILists();
			NotifyPropertyChanged("IsEnterROIMode");
			NotifyPropertyChanged("IsRoiViewVisible");
		}
	}

	public bool IsHasCurImage
	{
		get
		{
			return _IsHasCurImage;
		}
		set
		{
			if (_IsHasCurImage != value)
			{
				_IsHasCurImage = value;
				NotifyPropertyChanged("IsHasCurImage");
			}
		}
	}

	public bool BackEnable => history.Count > historyIndex && historyIndex > 0;

	public bool ForwardEnable => history.Count > historyIndex + 1 && historyIndex > -2;

	public bool IsRoiViewVisible
	{
		get
		{
			if (CurModelType == RoughModelType.Inherit || IsEnterROIMode)
			{
				return true;
			}
			return false;
		}
	}

	public double TextFontSize => OCRTrainDataMgr.Instance.IsEnglish ? 12 : 14;

	public double ParamLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 118 : 90;

	public double PageLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 580 : 598;

	public double ROITextWidth => OCRTrainDataMgr.Instance.IsEnglish ? 125 : 100;

	public bool IsGenerateModel
	{
		get
		{
			return _IsGenerateModel;
		}
		set
		{
			if (_IsGenerateModel != value)
			{
				_IsGenerateModel = value;
				NotifyPropertyChanged("IsGenerateModel");
				OCRTrainDataMgr.Instance.Owner?.UpdateNextStep();
			}
		}
	}

	public List<string> ModelTypeOptions => modelTypeOptions;

	public List<string> RoughModelList => roughModelList;

	public ICommand AddROICommand => _AddROICommand ?? (_AddROICommand = (ICommand)new DelegateCommand<object>((Action<object>)AddROICommandExecute));

	public ICommand RoiSelectedCommand => _RoiSelectedCommand ?? (_RoiSelectedCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiSelectedCommandExecute));

	public event PropertyChangedEventHandler PropertyChanged;

	public OCRLocationModelPage()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!_IsInit)
			{
				FreshOtherMask();
				((ItemsControl)(object)paramSearchUI).ItemsSource = ModuleCommonMethodMgr.Instance.GetItemSource("Tab_Character Model", "Tab_Search Params");
				this.imageViewBox.MouseLeftButtonDown += imageView_MouseLeftButtonDown;
				ImageViewBox imageViewBox = this.imageViewBox;
				imageViewBox.OnFreshExtendContent = (ImageViewBox.FreshExtendContent)Delegate.Combine(imageViewBox.OnFreshExtendContent, new ImageViewBox.FreshExtendContent(imageView_OnFreshExtendContent));
				tempImage.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
				tempImage.PreviewKeyUp += tempImage_PreviewKeyUp;
				tempImage.MouseEnter += tempImage_MouseEnter;
				tempImage.MouseMove += tempImage_MouseMove;
				tempImage.MouseWheel += tempImage_MouseWheel;
				tempImage.IsHitTestVisible = false;
				tempImage.Focusable = true;
				tempImage.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
				(this.imageViewBox.ExtenAbove as Canvas)?.Children.Add(tempImage);
				Panel.SetZIndex(tempImage, 100);
				_IsInit = true;
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			this.imageViewBox.MouseLeftButtonDown -= imageView_MouseLeftButtonDown;
			ImageViewBox imageViewBox = this.imageViewBox;
			imageViewBox.OnFreshExtendContent = (ImageViewBox.FreshExtendContent)Delegate.Remove(imageViewBox.OnFreshExtendContent, new ImageViewBox.FreshExtendContent(imageView_OnFreshExtendContent));
			tempImage.MouseLeftButtonUp -= tempImage_PreviewMouseLeftButtonUp;
			tempImage.PreviewKeyUp -= tempImage_PreviewKeyUp;
			tempImage.MouseEnter -= tempImage_MouseEnter;
			tempImage.MouseMove -= tempImage_MouseMove;
			tempImage.MouseWheel -= tempImage_MouseWheel;
			(this.imageViewBox.ExtenAbove as Canvas)?.Children.Remove(tempImage);
		};
	}

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private void SelectCurImage_OnClick(object sender, RoutedEventArgs e)
	{
		if (ClearModelWarning())
		{
			if (CurModelType == RoughModelType.Create)
			{
				ClearCreateData();
				CreateImage = OCRTrainDataMgr.Instance.CurImageData;
				_createRoi = null;
			}
			else
			{
				ClearInheritData();
				InheritImage = OCRTrainDataMgr.Instance.CurImageData;
				_inheritRoi = null;
			}
			RectEnable = false;
		}
	}

	private void SelectOtherImage_OnClick(object sender, RoutedEventArgs e)
	{
		string text = ConfigurationHelper.INIGetDefaultOpenPath(OCRTrainDataMgr.Instance.Module?.ModuleDir);
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.CheckFileExists = true;
			openFileDialog.CheckPathExists = true;
			openFileDialog.Multiselect = false;
			openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpeg;*.jpg;*.png;*.tif;*.tiff";
			if (text != null && Directory.Exists(text))
			{
				openFileDialog.InitialDirectory = text;
			}
			if (openFileDialog.ShowDialog() == false)
			{
				return;
			}
			text = openFileDialog.FileName;
			imageData = ImageData.GetImageDataFromFile(text, 0, 0, needGC: true);
			if (imageData.ImageBuffer == null)
			{
				return;
			}
			if (ClearModelWarning())
			{
				if (CurModelType == RoughModelType.Create)
				{
					ClearCreateData();
					CreateImage = imageData;
					_createRoi = null;
				}
				else
				{
					ClearInheritData();
					InheritImage = imageData;
					_inheritRoi = null;
				}
				RectEnable = false;
			}
			ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(text), OCRTrainDataMgr.Instance.Module?.ModuleDir);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), ex.Message, MessageIcons.Error);
		}
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		switch ((sender as FrameworkElement).Name)
		{
		case "hikFrame":
			DrawROIType = typeof(HikCoverRoiParameter);
			SelectedROIList = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData;
			IsStateForDrawROI = true;
			break;
		case "hikPolygon":
			DrawROIType = typeof(HikPolygonRoiParameter);
			SelectedROIList = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData;
			IsStateForDrawROI = true;
			break;
		case "hikCoverCircle":
			DrawROIType = typeof(HikCoverCircleRoiParameter);
			SelectedROIList = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData;
			IsStateForDrawROI = true;
			break;
		case "hikSector":
			DrawROIType = typeof(FeatureMathSectorROIModel);
			SelectedROIList = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData;
			IsStateForDrawROI = true;
			break;
		case "hikCross":
			DrawROIType = typeof(CrossROIModel);
			SelectedROIList = ((FeaturepatMatchDataBase)featurepatMatchDialogData).SpecialRoiList;
			IsStateForDrawROI = true;
			break;
		default:
			IsStateForDrawROI = false;
			Arrow.IsChecked = true;
			break;
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		IsStateForDrawROI = false;
	}

	public void GenerateModel_OnClick(object sender, RoutedEventArgs e)
	{
		if (OCRTrainDataMgr.Instance.Param == null || OCRTrainDataMgr.Instance.Owner == null)
		{
			return;
		}
		try
		{
			Arrow.IsChecked = true;
			if (!ClearModelWarning(bChangeType: true))
			{
				return;
			}
			if (CurModelType == RoughModelType.Create)
			{
				_IsCreateModel = false;
				ReFreshMask(((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask);
				FeaturePatCreateByEraser();
				GenerateModel();
				IsGenerateModel = (_IsCreateModel = true);
			}
			else
			{
				if (CurModelType != RoughModelType.Inherit)
				{
					return;
				}
				_IsInheritModel = false;
				if (InheritImage == null)
				{
					HikMessageBox.Show("Lang_OCRDefect_TrainModel", "Lang_NoImageErr", MessageIcons.Error, isTranslate: true);
					throw new Exception();
				}
				if (string.IsNullOrEmpty(_curSelectModelName))
				{
					HikMessageBox.Show("Lang_OCRDefect_TrainModel", "Lang_NoModelSelected", MessageIcons.Warning, isTranslate: true);
					throw new Exception();
				}
				roiImageViewBox.ClearExternShapes();
				ShapeList.Clear();
				GenerateModel();
				IsGenerateModel = (_IsInheritModel = true);
				_inheritBox = GetRoughBox();
				List<object> rects = OCRTrainDataMgr.Instance.GetRects(_inheritBox);
				if (rects == null)
				{
					return;
				}
				{
					foreach (object item in rects)
					{
						ShapeList.Add(item);
					}
					return;
				}
			}
		}
		catch
		{
			IsGenerateModel = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbDetectTarget).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbFinePositioning).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
		}
	}

	private void Rubber_Checked(object sender, RoutedEventArgs e)
	{
		rubber = new RubberControl();
		RubberControl rubberControl = rubber;
		rubberControl.OnRubberFinish = (RubberControl.RubberFinish)Delegate.Combine(rubberControl.OnRubberFinish, new RubberControl.RubberFinish(rubber_OnRubberFinish));
		RubberControl rubberControl2 = rubber;
		rubberControl2.OnWipe = (RubberControl.Wipe)Delegate.Combine(rubberControl2.OnWipe, new RubberControl.Wipe(rubber_OnWipe));
		rubber.DataContext = this;
		rubber.SetBinding(RubberControl.DWidthProperty, "PanRectWidth");
		rubber.SetBinding(RubberControl.DHeightProperty, "PanRectWidth");
		DrawROIType = typeof(ToolROIModel);
		IsStateForDrawROI = true;
		rubber.RegisterRoi(imageViewBox);
		tempImage.IsHitTestVisible = true;
		LogHelper.objLog.Info("Eraser checked done.");
	}

	private void Rubber_Unchecked(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Eraser unchecked.");
		if (rubber != null)
		{
			rubber.UnRegisterRoi(imageViewBox);
		}
		tempImage.IsHitTestVisible = false;
		IsStateForDrawROI = false;
		LogHelper.objLog.Info("Eraser unchecked done.");
	}

	private void rubber_OnRubberFinish(object sender, object Data)
	{
		RecordHistory();
	}

	private void rubber_OnWipe(int[] indexs, List<System.Windows.Point> bagPoints)
	{
		if (featurepatMatchDialogData == null || CreateImage == null)
		{
			return;
		}
		byte[] bytes = BitConverter.GetBytes(1291786778);
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask == null)
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask = new byte[CreateImage.Width * CreateImage.Height];
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask[num5] = 1;
			int num6 = num5 % CreateImage.Width;
			int num7 = num5 / CreateImage.Width;
			if (num > num6)
			{
				num = num6;
			}
			if (num2 > num7)
			{
				num2 = num7;
			}
			if (num3 < num6)
			{
				num3 = num6;
			}
			if (num4 < num7)
			{
				num4 = num7;
			}
		}
		DrawMask(new System.Drawing.Point(num, num2), num3 - num + 1, num4 - num2 + 1, isNormal: false);
	}

	public void ThumbMouseLeftButtonDown()
	{
		if (rubber != null)
		{
			rubber.IsDisplayArea = true;
		}
	}

	public void ThumbMouseLeftButtonUp()
	{
		if (rubber != null)
		{
			rubber.IsDisplayArea = false;
		}
	}

	private void clear_Click(object sender, RoutedEventArgs e)
	{
		if (featurepatMatchDialogData != null)
		{
			if (((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData == null)
			{
				((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData = new ObservableCollection<object>();
			}
			DoClear();
			RecordHistory();
		}
	}

	private void BackStep_Click(object sender, RoutedEventArgs e)
	{
		if (history.Count > historyIndex && historyIndex > 0)
		{
			historyIndex--;
			string path = history[historyIndex];
			ChangeHistory(path);
		}
		NotifyPropertyChanged("BackEnable");
		NotifyPropertyChanged("ForwardEnable");
	}

	private void ForwardStep_Click(object sender, RoutedEventArgs e)
	{
		if (history.Count > historyIndex + 1 && historyIndex > -2)
		{
			historyIndex++;
			string path = history[historyIndex];
			ChangeHistory(path);
		}
		NotifyPropertyChanged("BackEnable");
		NotifyPropertyChanged("ForwardEnable");
	}

	private void zoomOrgBtn_Click(object sender, RoutedEventArgs e)
	{
		if (IsRoiViewVisible)
		{
			roiImageViewBox.Init();
		}
		else
		{
			imageViewBox.Init();
		}
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected Obj, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected Obj, but got Unknown
		if (IsRoiViewVisible)
		{
			FullScreen val = new FullScreen();
			roiViewGrid.Children.Remove(roiImageViewBox);
			val.SetChild((UIElement)roiImageViewBox);
			((Window)(object)val).ShowDialog();
			val.RemoveChild();
			roiViewGrid.Children.Add(roiImageViewBox);
		}
		else
		{
			FullScreen val2 = new FullScreen();
			viewGrid.Children.Remove(imageViewBox);
			val2.SetChild((UIElement)imageViewBox);
			((Window)(object)val2).ShowDialog();
			val2.RemoveChild();
			viewGrid.Children.Add(imageViewBox);
		}
	}

	private void DeleteRoi_OnClick(object sender, RoutedEventArgs e)
	{
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Contains((sender as FrameworkElement).Tag))
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Remove((sender as FrameworkElement).Tag);
		}
	}

	private void ItemsData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		AutoSetFixPoint();
		RecordHistory();
	}

	private void imageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (Arrow.IsChecked != true)
		{
			return;
		}
		System.Windows.Point point = new System.Windows.Point(0.0, 0.0);
		point.X = imageViewBox.PixelX;
		point.Y = imageViewBox.PixelY;
		if (CreateImage == null || point.X < 0.0 || point.X >= (double)CreateImage.Width || point.Y < 0.0 || point.Y >= (double)CreateImage.Height)
		{
			return;
		}
		int num = (int)(point.Y * (double)CreateImage.Width + point.X);
		byte[] otherMask = ((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask;
		if (otherMask != null)
		{
			byte b = otherMask[num];
			if (b > 0 && b < 10)
			{
				ReFreshMask(otherMask);
				uint color = 2583632410u;
				SetUnicomColor(num, otherMask, color, b);
				DrawMask();
				tempImage.Focus();
			}
			else if (b == 0)
			{
				ReFreshMask(otherMask);
			}
		}
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		DrawMask();
	}

	private void tempImage_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseMove(object sender, MouseEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseEnter(object sender, MouseEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Delete)
		{
			byte[] otherMask = ((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask;
			if (otherMask != null)
			{
				ReViewMaskArea(otherMask);
				tempImage.Tag = null;
				RecordHistory();
			}
		}
	}

	private void tempImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void FullScreen_Click(object sender, RoutedEventArgs e)
	{
		IsStateForDrawROI = false;
		IsDrawRect = false;
		RectEnable = false;
		if (RectRois.Count > 0)
		{
			RectRois.Clear();
		}
		SelectedBox = null;
	}

	private void RectROI_Click(object sender, RoutedEventArgs e)
	{
		IsDrawRect = true;
		DrawROIType = typeof(HikCoverRoiParameter);
		SelectedROIList = RectRois;
		IsStateForDrawROI = true;
	}

	private void imageView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!_selectbox)
		{
			IEnumerable<RoiParameter> enumerable = from p in RectRois.OfType<RoiParameter>()
				where p.IsSelected
				select p;
			foreach (RoiParameter item in enumerable)
			{
				item.IsSelected = false;
			}
			SelectedBox = null;
		}
		_selectbox = false;
		if (RectRois.Count > 1)
		{
			RectRois.RemoveAt(0);
		}
	}

	private void DisplayOrHide_Click(object sender, RoutedEventArgs e)
	{
		if (IsDisplaySearchParam)
		{
			IsDisplaySearchParam = false;
			IsEnterROIMode = false;
		}
		else
		{
			IsDisplaySearchParam = true;
			IsEnterROIMode = true;
		}
	}

	private void SwitchMode_Click(object sender, RoutedEventArgs e)
	{
		if (IsEnterROIMode)
		{
			IsEnterROIMode = false;
		}
		else
		{
			IsEnterROIMode = true;
		}
	}

	private void ModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!(sender is ComboBox comboBox) || OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		if (comboBox.SelectedIndex == 0)
		{
			CurModelType = RoughModelType.Create;
			IsGenerateModel = _IsCreateModel;
			if (CreateImage == null && InheritImage != null)
			{
				ClearCreateData();
				CreateImage = InheritImage;
			}
			if (_createRoi != null)
			{
				RectEnable = true;
			}
			else
			{
				RectEnable = false;
			}
		}
		else if (comboBox.SelectedIndex == 1)
		{
			CurModelType = RoughModelType.Inherit;
			IsGenerateModel = _IsInheritModel;
			IsDisplaySearchParam = true;
			if (InheritImage == null)
			{
				InheritImage = CreateImage;
			}
			else
			{
				roiImageViewBox.ImageDataSource = InheritImage;
			}
			roiImageViewBox.ClearExternShapes();
			ShapeList.Clear();
			List<object> rects = OCRTrainDataMgr.Instance.GetRects(_inheritBox);
			if (rects != null)
			{
				foreach (object item in rects)
				{
					ShapeList.Add(item);
				}
			}
			if (_inheritRoi != null)
			{
				RectEnable = true;
			}
			else
			{
				RectEnable = false;
			}
			GetRoughModelList();
		}
		if (IsGenerateModel)
		{
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbDetectTarget).IsEnabled = _isDetectTarget;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbFinePositioning).IsEnabled = _isFinePositioning;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled = _isAddMask;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = _isStatisticsTrain;
		}
		else
		{
			_isDetectTarget = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbDetectTarget).IsEnabled;
			_isFinePositioning = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbFinePositioning).IsEnabled;
			_isAddMask = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled;
			_isStatisticsTrain = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbDetectTarget).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbFinePositioning).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
		}
		RebuildROILists();
		NotifyPropertyChanged("IsRoiViewVisible");
		try
		{
			SetRoughModelType();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), ex.Message, MessageIcons.Error);
		}
	}

	private void RoughModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is ComboBox { SelectedValue: not null } comboBox)
		{
			_curSelectModelName = comboBox.SelectedValue.ToString();
		}
	}

	private void InitData()
	{
		((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask = null;
		tempImage.Source = null;
		((FeaturepatMatchDataBase)featurepatMatchDialogData).PointsData.Clear();
		((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Clear();
		((FeaturepatMatchDataBase)featurepatMatchDialogData).NotifyPropertyChanged("EditOutlinePointsEnable");
	}

	private void DoClear()
	{
		if (featurepatMatchDialogData != null)
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Clear();
			((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask = null;
		}
		ClearMaskData();
	}

	private void ClearMaskData()
	{
		tempImage.Source = null;
		MaskSourceConvertor.Clear();
	}

	private void ReFreshMask(byte[] otherMask)
	{
		if (otherMask != null)
		{
			ReFreshMaskData(otherMask);
			DrawMask();
		}
	}

	private void ReFreshMaskData(byte[] otherMask)
	{
		byte[] bytes = BitConverter.GetBytes(1291786778);
		for (int i = 0; i < otherMask.Length; i++)
		{
			if (otherMask[i] > 10)
			{
				otherMask[i] -= 10;
			}
		}
	}

	private void DrawMask()
	{
		if (CreateImage != null)
		{
			DrawMask(new System.Drawing.Point(0, 0), CreateImage.Width, CreateImage.Height);
		}
	}

	private unsafe void DrawMask(System.Drawing.Point point, int sWidth, int sHeight, bool isNormal = true)
	{
		if (imageViewBox == null || imageViewBox.Source == null || ((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask == null)
		{
			return;
		}
		System.Windows.Point point2 = new System.Windows.Point(((imageViewBox.MapCanvas as Canvas).ActualWidth - (imageViewBox.Viewbox as Viewbox).ActualWidth) / 2.0 + Canvas.GetLeft(imageViewBox.Viewbox as Viewbox) + imageViewBox.ImageOffSetX, ((imageViewBox.MapCanvas as Canvas).ActualHeight - (imageViewBox.Viewbox as Viewbox).ActualHeight) / 2.0 + Canvas.GetTop(imageViewBox.Viewbox as Viewbox) + imageViewBox.ImageOffSetY);
		double onePixelWidth = imageViewBox.OnePixelWidth;
		int num = 0;
		int num2 = 0;
		int num3 = (int)((imageViewBox.MapCanvas as Canvas)?.ActualWidth).Value;
		int num4 = (int)((imageViewBox.MapCanvas as Canvas)?.ActualHeight).Value;
		if (num3 == 0 || num4 == 0)
		{
			return;
		}
		WriteableBitmap writeableBitmap = MaskSourceConvertor.Init(num3, num4, PixelFormats.Bgra32);
		if (writeableBitmap == null)
		{
			return;
		}
		tempImage.Source = writeableBitmap;
		writeableBitmap.Lock();
		byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
		int num5 = writeableBitmap.BackBufferStride / 4;
		uint* ptr2 = (uint*)ptr;
		if (isNormal)
		{
			for (int i = 0; i < num4; i++)
			{
				uint* ptr3 = ptr2 + i * num5;
				for (int j = 0; j < num3; j++)
				{
					ptr3[j] = 0u;
				}
			}
		}
		fixed (byte* otherMask = ((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask)
		{
			uint num6 = 0u;
			int num7 = (int)Math.Ceiling(onePixelWidth);
			int width = CreateImage.Width;
			int height = CreateImage.Height;
			int num8 = 0;
			int num9 = Math.Max((int)((double)point.X * onePixelWidth + point2.X), 0);
			int num10 = Math.Max((int)((double)point.Y * onePixelWidth + point2.Y), 0);
			int num11 = Math.Min((int)((double)(point.X + sWidth) * onePixelWidth + point2.X) + 1, num3);
			int num12 = Math.Min((int)((double)(point.Y + sHeight) * onePixelWidth + point2.Y) + 1, num4);
			for (int k = num10; k < num12; k++)
			{
				for (int l = num9; l < num11; l++)
				{
					num = (int)(((double)l - point2.X) / onePixelWidth);
					if (num < 0 || num >= width)
					{
						continue;
					}
					num2 = (int)(((double)k - point2.Y) / onePixelWidth);
					if (num2 >= 0 && num2 < height)
					{
						num8 = num2 * width + num;
						if (otherMask[num8] != 0)
						{
							num6 = ((otherMask[num8] <= 10) ? 1291786778u : 2583632410u);
							uint* ptr4 = ptr2 + k * num5 + l;
							*ptr4 = num6;
						}
					}
				}
			}
			if (isNormal)
			{
				writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
			}
			else
			{
				try
				{
					writeableBitmap.AddDirtyRect(new Int32Rect(num9, num10, num11 - num9, num12 - num10));
				}
				catch
				{
					writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
				}
			}
			writeableBitmap.Unlock();
		}
	}

	private void FeaturePatCreateByEraser()
	{
		((FeaturepatMatchDataBase)featurepatMatchDialogData).PointsData.Clear();
		((FeaturepatMatchDataBase)featurepatMatchDialogData).NotifyPropertyChanged("EditOutlinePointsEnable");
		ShowPoints(featurepatMatchDialogData, needPopTip: true);
	}

	private int ShowPoints(FeaturepatMatchDialogData featurepatMatchData, bool needPopTip, bool needOkPopTip = true, bool needApply = false)
	{
		Dispatcher.Invoke(() =>
		{
			windowShadow.Visibility = Visibility.Visible;
		});
		int num = -1;
		try
		{
			if (CreateImage == null)
			{
				HikMessageBox.Show("Lang_OCRDefect_TrainModel", "Lang_NoImageErr", MessageIcons.Error, isTranslate: true);
				throw new Exception();
			}
			if ((((FeaturepatMatchDataBase)featurepatMatchData).ItemsData == null || ((FeaturepatMatchDataBase)featurepatMatchData).ItemsData.Count == 0) && ((FeaturepatMatchDataBase)featurepatMatchData).otherMask == null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), AppLocalizationService.GetLang("Lang_NoROIRegion"), MessageIcons.Error);
				throw new Exception();
			}
			((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData = CreateImage;
			featurepatMatchDialogData.CreateModel();
			RefreshPoints();
			num = 0;
		}
		catch (Exception ex)
		{
			Dispatcher.Invoke(() =>
			{
				windowShadow.Visibility = Visibility.Collapsed;
			});
			throw ex;
		}
		Dispatcher.Invoke(() =>
		{
			windowShadow.Visibility = Visibility.Collapsed;
		});
		return num;
	}

	protected void RefreshPoints()
	{
		if (!ShapeList.Any((object p) => p is IMultiPoint))
		{
			MultiPoint multiPoint = new MultiPoint();
			ShapeList.Add(multiPoint);
			multiPoint.OutlinePoints = new ObservableCollection<IOutlinePoint>();
			multiPoint.MicroPoints = new ObservableCollection<IMicroPoint>();
			multiPoint.Rectangles = new ObservableCollection<IRectangle>();
		}
		if (ShapeList.FirstOrDefault((object p) => p is IMultiPoint) is MultiPoint multiPoint2)
		{
			multiPoint2.MicroPoints.Clear();
			foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in ((FeaturepatMatchDataBase)featurepatMatchDialogData).PointsData)
			{
				MicroPoint microPoint = new MicroPoint();
				microPoint.CenterX = pointsDatum.x;
				microPoint.CenterY = pointsDatum.y;
				microPoint.Color = "#66FF00";
				microPoint.Opacity = 1.0;
				multiPoint2.MicroPoints.Add(microPoint);
			}
		}
		UpdateShapeDisplay();
	}

	private void UpdateShapeDisplay(bool isSelfSet = false)
	{
		object obj = ShapeList.FirstOrDefault((object p) => p is IMultiPoint);
		ShapeList.Clear();
		imageViewBox?.ClearExternShapes();
		if (obj != null)
		{
			ShapeList.Add(obj);
		}
	}

	private void RebuildROILists()
	{
		if (IsRoiViewVisible)
		{
			_ROILists = new List<IList>();
			_ROILists.Add(RectRois);
		}
		else
		{
			_ROILists = new List<IList>();
			_ROILists.Add(((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData);
			_ROILists.Add(((FeaturepatMatchDataBase)featurepatMatchDialogData).SpecialRoiList);
		}
		NotifyPropertyChanged("ROILists");
	}

	protected virtual void AddROICommandExecute(object roi)
	{
		IsStateForDrawROI = false;
		if (IsRoiViewVisible)
		{
			if (CurModelType == RoughModelType.Inherit || IsEnterROIMode)
			{
				IsDrawRect = false;
				RectEnable = true;
			}
			return;
		}
		if (roi is ICrossROI crossROI)
		{
			List<IROI> list = ((FeaturepatMatchDataBase)featurepatMatchDialogData).SpecialRoiList.Where((IROI p) => p is ICrossROI).ToList();
			foreach (IROI item in list)
			{
				if (item != roi)
				{
					((FeaturepatMatchDataBase)featurepatMatchDialogData).SpecialRoiList.Remove(item);
				}
			}
			IsSetCrossByManual = true;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointX = crossROI.CenterPoint.X;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointY = crossROI.CenterPoint.Y;
		}
		else
		{
			AddRoiDelegate(roi as IROI);
		}
		Arrow.IsChecked = true;
	}

	private void RoiSelectedCommandExecute(RoiWithTagEventArgs args)
	{
		if (!_IsSettingSelectBox)
		{
			IROI roi = args.Roi;
			_IsSettingSelectBox = true;
			SelectBox(roi as HikCoverRoiParameter);
			_IsSettingSelectBox = false;
		}
	}

	protected virtual void RoiParamChange(IROI obj)
	{
		AutoSetFixPoint();
		if (needSkipData.Contains(obj))
		{
			needSkipData.Remove(obj);
		}
		else
		{
			RecordHistory();
		}
	}

	protected void AddRoiDelegate(IROI roi)
	{
		roi.DataChangedCommand = (ICommand)new DelegateCommand<IROI>((Action<IROI>)RoiParamChange);
		(roi as RoiBaseParameter)?.NotifyPropertyChanged("DataChangedCommand");
		(roi as RoiParameter)?.NotifyPropertyChanged("DataChangedCommand");
	}

	private void RecordHistory()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected Obj, but got Unknown
		try
		{
			FeaturepatMatchDialogData val = new FeaturepatMatchDialogData
			{
				ItemsData = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData,
				otherMask = ((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask
			};
			string text = System.IO.Path.GetFullPath("./Temporary") + "\\FeaturepatMatch_" + (historyIndex + 1) + ".xml";
			((BaseNode)(object)val).Save(text);
			historyIndex++;
			history.Insert(historyIndex, text);
			history.RemoveRange(historyIndex + 1, history.Count - (historyIndex + 1));
			NotifyPropertyChanged("BackEnable");
			NotifyPropertyChanged("ForwardEnable");
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	private void ChangeHistory(string path)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		FeaturepatMatchDialogData val = new FeaturepatMatchDialogData();
		if (val != null)
		{
			((BaseNode)(object)val).loadXml(path, false);
			if (((FeaturepatMatchDataBase)val).ItemsData != null)
			{
				needSkipData.AddRange(((FeaturepatMatchDataBase)val).ItemsData);
			}
			((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData = ((FeaturepatMatchDataBase)val).ItemsData;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask = ((FeaturepatMatchDataBase)val).otherMask;
			FreshOtherMask();
		}
	}

	private void FreshOtherMask()
	{
		if (featurepatMatchDialogData != null)
		{
			if (((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask != null)
			{
				DrawMask();
			}
			else
			{
				ClearMaskData();
			}
		}
	}

	private void AutoSetFixPoint()
	{
		if (featurepatMatchDialogData == null)
		{
			return;
		}
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Count > 0)
		{
			if (IsSetCrossByManual)
			{
				return;
			}
			PointCollection pointCollection = new PointCollection();
			foreach (IROI itemsDatum in ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData)
			{
				System.Windows.Point value = default;
				if (itemsDatum is HikCoverRoiParameter)
				{
					HikCoverRoiParameter hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
					value.X = hikCoverRoiParameter.OriginPoint.X + hikCoverRoiParameter.Width / 2.0;
					value.Y = hikCoverRoiParameter.OriginPoint.Y + hikCoverRoiParameter.Height / 2.0;
				}
				if (itemsDatum is HikPolygonRoiParameter)
				{
					IPolygonROI polygonROI = itemsDatum as IPolygonROI;
					double num = polygonROI.PolygonPoints.Min((System.Windows.Point r) => r.X);
					double num2 = polygonROI.PolygonPoints.Min((System.Windows.Point r) => r.Y);
					double num3 = polygonROI.PolygonPoints.Max((System.Windows.Point r) => r.X);
					double num4 = polygonROI.PolygonPoints.Max((System.Windows.Point r) => r.Y);
					value.X = (num + num3) / 2.0;
					value.Y = (num2 + num4) / 2.0;
				}
				if (itemsDatum is HikCoverCircleRoiParameter)
				{
					HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
					value.X = hikCoverCircleRoiParameter.CenterPoint.X;
					value.Y = hikCoverCircleRoiParameter.CenterPoint.Y;
				}
				FeatureMathSectorROIModel val;
				if ((val = (FeatureMathSectorROIModel)((itemsDatum is FeatureMathSectorROIModel) ? itemsDatum : null)) != null)
				{
					value = ((SectorROIModel)(object)val).CenterPoint;
				}
				pointCollection.Add(value);
			}
			((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointX = pointCollection.Sum((System.Windows.Point r) => r.X) / (double)pointCollection.Count;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointY = pointCollection.Sum((System.Windows.Point r) => r.Y) / (double)pointCollection.Count;
		}
		else
		{
			IsSetCrossByManual = false;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointX = 0.0;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointY = 0.0;
		}
	}

	private void SetUnicomColor(int index, byte[] otherMask, uint color, byte masknum)
	{
		int[] BorderArray = new int[1];
		if (index > 0 && index < otherMask.Length && IsSamePoint(otherMask[index], masknum))
		{
			SetHighLightColor(index, otherMask, color);
			BorderArray[0] = index;
			int Dindex = 1;
			while (Dindex > 0)
			{
				SetUnicomColor(otherMask, color, masknum, ref BorderArray, ref Dindex);
			}
		}
	}

	private void SetUnicomColor(byte[] otherMask, uint color, byte masknum, ref int[] BorderArray, ref int Dindex)
	{
		int num = 0;
		int[] array = new int[Dindex];
		Buffer.BlockCopy(BorderArray, 0, array, 0, Dindex * 4);
		for (int i = 0; i < Dindex; i++)
		{
			int num2 = array[i];
			int num3 = num2 / CreateImage.Width;
			int num4 = num2 % CreateImage.Width;
			int num5 = num2 / CreateImage.Width * CreateImage.Width;
			int num6 = num5 + CreateImage.Width - 1;
			if (num3 - 1 >= 0)
			{
				int k = (num3 - 1) * CreateImage.Width + num4;
				SetNewBordPoint(otherMask, masknum, k, color, ref BorderArray, ref num);
			}
			if (num3 - 1 >= 0 && num4 + 1 < CreateImage.Width)
			{
				int k2 = num4 + 1 + (num3 - 1) * CreateImage.Width;
				SetNewBordPoint(otherMask, masknum, k2, color, ref BorderArray, ref num);
			}
			if (num4 + 1 < CreateImage.Width)
			{
				int k3 = num5 + num4 + 1;
				SetNewBordPoint(otherMask, masknum, k3, color, ref BorderArray, ref num);
			}
			if (num4 + 1 < CreateImage.Width && num3 + 1 < CreateImage.Height)
			{
				int k4 = num4 + 1 + (num3 + 1) * CreateImage.Width;
				SetNewBordPoint(otherMask, masknum, k4, color, ref BorderArray, ref num);
			}
			if (num3 + 1 < CreateImage.Height)
			{
				int k5 = (num3 + 1) * CreateImage.Width + num4;
				SetNewBordPoint(otherMask, masknum, k5, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0 && num3 + 1 < CreateImage.Height)
			{
				int k6 = (num3 + 1) * CreateImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k6, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0)
			{
				int k7 = num3 * CreateImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k7, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0 && num3 - 1 >= 0)
			{
				int k8 = (num3 - 1) * CreateImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k8, color, ref BorderArray, ref num);
			}
		}
		Dindex = num;
	}

	private void SetNewBordPoint(byte[] otherMask, byte masknum, int k, uint color, ref int[] BorderArray, ref int num)
	{
		if (IsSamePoint(otherMask[k], masknum))
		{
			SetHighLightColor(k, otherMask, color);
			JudgeArray(ref BorderArray, num);
			BorderArray[num] = k;
			num++;
		}
	}

	private void JudgeArray(ref int[] BorderArray, int num)
	{
		if (BorderArray.Length < num + 1)
		{
			int[] src = BorderArray;
			BorderArray = new int[BorderArray.Length + 10000];
			Buffer.BlockCopy(src, 0, BorderArray, 0, num * 4);
		}
	}

	private void SetHighLightColor(int index, byte[] otherMask, uint color)
	{
		otherMask[index] += 10;
	}

	private bool IsSamePoint(byte point, byte cPoint)
	{
		return point > 0 && point < 10 && point == cPoint;
	}

	private void ReViewMaskArea(byte[] otherMask)
	{
		byte[] bytes = BitConverter.GetBytes(0);
		for (int i = 0; i < otherMask.Length; i++)
		{
			if (otherMask[i] > 10)
			{
				otherMask[i] = 0;
			}
		}
		DrawMask();
	}

	public void ShowImage()
	{
		if (CreateImage != null)
		{
			try
			{
				imageViewBox.ImageDataSource = CreateImage;
			}
			catch
			{
			}
		}
	}

	private bool ClearModelWarning(bool bChangeType = false)
	{
		if (OCRTrainDataMgr.Instance.Owner == null)
		{
			return true;
		}
		if (bChangeType)
		{
			if (CurModelType == RoughModelType.Create && !_IsInheritModel)
			{
				return true;
			}
			if (CurModelType == RoughModelType.Inherit && !_IsCreateModel)
			{
				return true;
			}
		}
		else if (!IsGenerateModel)
		{
			return true;
		}
		string text = "";
		text = ((!bChangeType) ? "Lang_ChangeImageTip" : "Lang_ChangeModelTypeTip");
		DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", text, MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == DialogResult.Yes || dialogResult == DialogResult.SDKModel)
		{
			OCRTrainDataMgr.Instance.OnClearAllModelEvent();
			if (bChangeType)
			{
				if (CurModelType == RoughModelType.Create)
				{
					ClearInheritData();
				}
				else if (CurModelType == RoughModelType.Inherit)
				{
					ClearCreateData();
				}
			}
			else
			{
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbDetectTarget).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbFinePositioning).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
			}
			OCRTrainDataMgr.Instance.Owner.ClearAllData();
			return true;
		}
		return false;
	}

	private void SelectBox(HikCoverRoiParameter box)
	{
		foreach (RoiParameter rectRoi in RectRois)
		{
			rectRoi.IsSelected = false;
			if (!rectRoi.ForceShowSelected)
			{
				rectRoi.ForceShowSelected = true;
			}
		}
		if (box != null)
		{
			box.IsSelected = true;
			_selectbox = true;
		}
		else
		{
			_selectbox = false;
		}
		SelectedBox = box;
	}

	private void Dispose()
	{
	}

	private void GenerateModel()
	{
		try
		{
			SetRoughModelType();
			if (CurModelType == RoughModelType.Create)
			{
				OCRTrainDataMgr.Instance.SetDatumImage(CreateImage);
			}
			else if (CurModelType == RoughModelType.Inherit)
			{
				OCRTrainDataMgr.Instance.SetDatumImage(InheritImage);
			}
			SetRoiData();
			RoughModelTrain();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), ex.Message, MessageIcons.Error);
			throw new Exception();
		}
	}

	private void SetRoughModelType()
	{
		try
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)["RoughMatchInfoSour"] = ((int)CurModelType).ToString();
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + "\r\n";
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			throw new Exception(text);
		}
	}

	private void SetRoiData()
	{
		try
		{
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetRoughRoiData"] = GetRoiBuffer();
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("Lang_SetROIError") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			throw new Exception(text2);
		}
	}

	private void RoughModelTrain()
	{
		try
		{
			OCRTrainDataMgr.Instance.Param.TimeOut = 2147483647u;
			if (CurModelType == RoughModelType.Create)
			{
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["RoughModelTrain"] = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ModelData;
			}
			else if (CurModelType == RoughModelType.Inherit)
			{
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["RoughModelTrain"] = StringUtil.UTF8GetBytesPadZero(_curSelectModelName);
			}
			OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("Lang_SetCoarsePositioningFailure") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			throw new Exception(text2);
		}
	}

	private byte[] GetRoiBuffer()
	{
		if (CurModelType == RoughModelType.Create)
		{
			if (CreateImage != null)
			{
				return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(_createRoi, (float)CreateImage.Width, (float)CreateImage.Height);
			}
			return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(_createRoi, 0f, 0f);
		}
		if (CurModelType == RoughModelType.Inherit)
		{
			if (InheritImage != null)
			{
				return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(_inheritRoi, (float)InheritImage.Width, (float)InheritImage.Height);
			}
			return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(_inheritRoi, 0f, 0f);
		}
		return null;
	}

	private unsafe void GetRoughModelList()
	{
		if (roughModelList.Count > 0 || OCRTrainDataMgr.Instance.Module == null || OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		string text = "";
		foreach (object frontModule in OCRTrainDataMgr.Instance.Module.FrontModules)
		{
			if (frontModule is IVmModule { ModuleTypeName: "IMVSHPFeatureMatchModu" } vmModule)
			{
				if (!string.IsNullOrEmpty(text))
				{
					text += ";";
				}
				text += vmModule.ID;
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)["SetRoughMatchModuIDList"] = text;
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetRoughMatchPatList"];
			if (array != null && array.Length != 0)
			{
				IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				int num = 0;
				byte* ptr = (byte*)intPtr.ToPointer();
				int num2 = array.Length;
				while (num < num2)
				{
					int num3 = *(int*)(ptr + num);
					num += 4;
					if (num3 > 0 && num + num3 <= num2)
					{
						byte[] array2 = new byte[num3];
						Marshal.Copy(intPtr + num, array2, 0, num3);
						roughModelList.Add(StringUtil.UTF8GetString(array2));
					}
					num += num3;
					if (num <= 0)
					{
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string message = "GetRoughModelList error: " + text2;
			LogHelper.moduleLog.Error(message);
		}
		if (roughModelList.Count > 0)
		{
			if (roughModelList.Contains(_curSelectModelName))
			{
				comboModel.SelectedItem = _curSelectModelName;
			}
			else if (_IsInheritModel)
			{
				_curSelectModelName = "";
			}
			else
			{
				comboModel.SelectedIndex = 0;
			}
		}
		else
		{
			_curSelectModelName = "";
		}
	}

	private void ClearCreateData()
	{
		InitData();
		DoClear();
		needSkipData.Clear();
		IsGenerateModel = (_IsCreateModel = false);
	}

	private void ClearInheritData()
	{
		roiImageViewBox.ClearExternShapes();
		ShapeList.Clear();
		_inheritBox = null;
		IsGenerateModel = (_IsInheritModel = false);
	}

	private MVBI_BOX_F[] GetRoughBox()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return null;
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetRoughMatchBox"];
			if (array != null && array.Length != 0)
			{
				return MVBI_BOX_F.GetBoxesWithoutNum(array);
			}
		}
		catch
		{
		}
		return null;
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected Obj, but got Unknown
		IsHasCurImage = OCRTrainDataMgr.Instance.CurImageData != null;
		roiImageViewBox.ROITypeUI = false;
		_createRoi = moduleTrainData.CreateRoiParam;
		_inheritRoi = moduleTrainData.InheritRoiParam;
		_curSelectModelName = moduleTrainData.CurSelectModelName;
		_IsCreateModel = moduleTrainData.IsCreateModel;
		_IsInheritModel = moduleTrainData.IsInheritModel;
		_isDetectTarget = moduleTrainData.IsDetectTarget;
		_isFinePositioning = moduleTrainData.IsFinePositioning;
		_isAddMask = moduleTrainData.IsAddMask;
		_isStatisticsTrain = moduleTrainData.IsStatisticsTrain;
		featurepatMatchDialogData = new FeaturepatMatchDialogData();
		featurepatMatchDialogData = moduleTrainData.FeatureMatchData.CopyModel(false);
		CollectionChangedEventManager.AddHandler(((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData, ItemsData_CollectionChanged);
		DataContext = featurepatMatchDialogData;
		imageViewBox.DataContext = featurepatMatchDialogData;
		((FeaturepatMatchDataBase)featurepatMatchDialogData).version = FeaturepatMatchDataBase.curVersion;
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData != null)
		{
			CreateImage = ((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData;
		}
		if (moduleTrainData.InheritImageData != null)
		{
			InheritImage = moduleTrainData.InheritImageData;
		}
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).PointsData != null)
		{
			RefreshPoints();
		}
		int num = 0;
		try
		{
			string s = ((IUserStringData)OCRTrainDataMgr.Instance.Param)["RoughMatchInfoSour"];
			num = int.Parse(s);
		}
		catch
		{
		}
		switch (num)
		{
		case 0:
			if (CreateImage == null)
			{
				CreateImage = OCRTrainDataMgr.Instance.CurImageData;
			}
			roiImageViewBox.ImageDataSource = CreateImage;
			break;
		case 1:
			if (InheritImage == null)
			{
				InheritImage = OCRTrainDataMgr.Instance.CurImageData;
			}
			_inheritBox = GetRoughBox();
			break;
		}
		comboModelType.SelectedIndex = num;
		FeaturepatMatchDialogData val = featurepatMatchDialogData;
		if (((val != null) ? ((FeaturepatMatchDataBase)val).ItemsData : null) != null)
		{
			foreach (object itemsDatum in ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData)
			{
				if (itemsDatum is IROIUIType { UIType: false } iROIUIType)
				{
					iROIUIType.UIType = true;
				}
			}
		}
		NotifyPropertyChanged("ROILists");
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		if (RectRois.Count > 0)
		{
			if (CurModelType == RoughModelType.Create)
			{
				_createRoi = RectRois[0] as HikCoverRoiParameter;
			}
			else if (CurModelType == RoughModelType.Inherit)
			{
				_inheritRoi = RectRois[0] as HikCoverRoiParameter;
			}
		}
		((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData = CreateImage;
		moduleTrainData.CreateRoiParam = _createRoi;
		moduleTrainData.InheritImageData = InheritImage;
		moduleTrainData.InheritRoiParam = _inheritRoi;
		moduleTrainData.CurSelectModelName = _curSelectModelName;
		moduleTrainData.IsCreateModel = _IsCreateModel;
		moduleTrainData.IsInheritModel = _IsInheritModel;
		moduleTrainData.FeatureMatchData = featurepatMatchDialogData;
		if (IsGenerateModel)
		{
			moduleTrainData.IsDetectTarget = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbDetectTarget).IsEnabled;
			moduleTrainData.IsFinePositioning = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbFinePositioning).IsEnabled;
			moduleTrainData.IsAddMask = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled;
			moduleTrainData.IsStatisticsTrain = ((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled;
		}
		else
		{
			moduleTrainData.IsDetectTarget = _isDetectTarget;
			moduleTrainData.IsFinePositioning = _isFinePositioning;
			moduleTrainData.IsAddMask = _isAddMask;
			moduleTrainData.IsStatisticsTrain = _isStatisticsTrain;
		}
		Dispose();
		DataContext = null;
		imageViewBox.DataContext = null;
		roiImageViewBox.DataContext = null;
		ModelData = null;
		((ItemsControl)(object)paramSearchUI).ItemsSource = null;
		CreateImage = null;
		InheritImage = null;
		RectRois.Clear();
	}
}
