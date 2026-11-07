using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Apps.XmlUI;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility;
using VMControls.BaseInterface;

namespace IMVSMarkInspModuControl;

public partial class OCRTrainTargetPage : UserControl, INotifyPropertyChanged, IComponentConnector
{
	private Integer _grayThreshItem;

	private Apps.XmlParser.ParamTab.Boolean _binaryImageShow;

	private Image _maskLayer = new Image();

	private bool _changedredo;

	private bool _selectbox = false;

	private Rubber2 _rubber;

	private UndoEngine _undoEngine = new UndoEngine();

	private WriteableBitmap _patMaskImage;

	private int _PanRectWidth = 10;

	private bool _IsInit = false;

	private bool _IsDrawBox = false;

	private bool _IsDisplayLocationParam = false;

	public static readonly DependencyProperty IsDisplayBinaryImageProperty = DependencyProperty.Register("IsDisplayBinaryImage", typeof(bool), typeof(OCRTrainTargetPage), new PropertyMetadata(false));

	public static readonly DependencyProperty IsGetBoxProperty = DependencyProperty.Register("IsGetBox", typeof(bool), typeof(OCRTrainTargetPage), new PropertyMetadata(false));

	public static readonly DependencyProperty IsGetFeatureModelProperty = DependencyProperty.Register("IsGetFeatureModel", typeof(bool), typeof(OCRTrainTargetPage), new PropertyMetadata(false));

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BinaryImageProperty = DependencyProperty.Register("BinaryImage", typeof(ImageData), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BoxCoversProperty = DependencyProperty.Register("BoxCovers", typeof(ObservableCollection<object>), typeof(OCRTrainTargetPage), new PropertyMetadata(null));

	public static readonly DependencyProperty SelectedBoxProperty = DependencyProperty.Register("SelectedBox", typeof(HikCoverRoiParameter), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty MaskImageProperty = DependencyProperty.Register("MaskImage", typeof(ImageSource), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	internal ResultOverviewPopupButton setBtn;

	internal ParamListView paramDivideUI;

	internal ParamListView paramTrainUI;

	internal ParamListView paramLocationUI;

	public bool IsDisplayBinaryImage
	{
		get
		{
			return (bool)GetValue(IsDisplayBinaryImageProperty);
		}
		set
		{
			SetValue(IsDisplayBinaryImageProperty, value);
		}
	}

	public bool IsGetBox
	{
		get
		{
			return (bool)GetValue(IsGetBoxProperty);
		}
		set
		{
			SetValue(IsGetBoxProperty, value);
		}
	}

	public bool IsGetFeatureModel
	{
		get
		{
			return (bool)GetValue(IsGetFeatureModelProperty);
		}
		set
		{
			SetValue(IsGetFeatureModelProperty, value);
		}
	}

	public ImageData CurImage
	{
		get
		{
			return (ImageData)GetValue(CurImageProperty);
		}
		set
		{
			SetValue(CurImageProperty, value);
		}
	}

	public ImageData BinaryImage
	{
		get
		{
			return (ImageData)GetValue(BinaryImageProperty);
		}
		set
		{
			SetValue(BinaryImageProperty, value);
		}
	}

	public ObservableCollection<object> BoxCovers
	{
		get
		{
			return (ObservableCollection<object>)GetValue(BoxCoversProperty);
		}
		set
		{
			SetValue(BoxCoversProperty, value);
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

	public ImageSource MaskImage
	{
		get
		{
			return (ImageSource)GetValue(MaskImageProperty);
		}
		set
		{
			SetValue(MaskImageProperty, value);
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
				if (CurImage != null && (value > CurImage.Width || value > CurImage.Height))
				{
					_PanRectWidth = Convert.ToInt32((CurImage.Width > CurImage.Height) ? CurImage.Height : CurImage.Width);
					OnPropertyChanged("PanRectWidth");
				}
				else
				{
					_PanRectWidth = value;
					OnPropertyChanged("PanRectWidth");
				}
			}
		}
	}

	public bool IsDisplayLocationParam
	{
		get
		{
			return _IsDisplayLocationParam;
		}
		set
		{
			if (_IsDisplayLocationParam != value)
			{
				_IsDisplayLocationParam = value;
				OnPropertyChanged("IsDisplayLocationParam");
			}
		}
	}

	public double ButtonWidth => OCRTrainDataMgr.Instance.IsEnglish ? 95 : 74;

	public double PageLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 532 : 590;

	public double ParamLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 172 : 105;

	public event PropertyChangedEventHandler PropertyChanged;

	public OCRTrainTargetPage()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!_IsInit)
			{
				imageView.ROITypeUI = ROITypeUIEnum.Transparent;
				if (BoxCovers == null)
				{
					BoxCovers = new ObservableCollection<object>();
				}
				Button backStep = BackStep;
				object dataContext = (ForwardStep.DataContext = _undoEngine);
				backStep.DataContext = dataContext;
				BoxCovers.CollectionChanged += imageView_OnRoiCountChanged;
				UndoEngine undoEngine2 = _undoEngine;
				undoEngine2.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoEngine2.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				UndoEngine undoEngine3 = _undoEngine;
				undoEngine3.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoEngine3.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				((ItemsControl)(object)paramDivideUI).ItemsSource = ModuleCommonMethodMgr.Instance.GetItemSource("Tab_Character Model", "Tab_Char Sgt");
				((ItemsControl)(object)paramTrainUI).ItemsSource = ModuleCommonMethodMgr.Instance.GetItemSource("Tab_Character Model", "Tab_Train Params");
				((ItemsControl)(object)paramLocationUI).ItemsSource = ModuleCommonMethodMgr.Instance.GetItemSource("Tab_Character Model", "Tab_Exact Location Params");
				_grayThreshItem = ModuleCommonMethodMgr.Instance.GetParamItem(((ItemsControl)(object)paramDivideUI).ItemsSource, "GrayThresh") as Integer;
				_binaryImageShow = ModuleCommonMethodMgr.Instance.GetParamItem(((ItemsControl)(object)paramDivideUI).ItemsSource, "BinaryImageShow") as Apps.XmlParser.ParamTab.Boolean;
				if (_grayThreshItem != null)
				{
					_grayThreshItem.SetValueCompleted += GrayThresh_SetValue;
				}
				if (_binaryImageShow != null)
				{
					_binaryImageShow.CurValue = false;
					_binaryImageShow.OnCurValueChanged += BinaryImageShow_ValueChanged;
				}
				_maskLayer.MouseLeftButtonDown += tempImage_PreviewMouseLeftButtonDown;
				_maskLayer.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
				_maskLayer.MouseEnter += tempImage_MouseEnter;
				_maskLayer.MouseMove += tempImage_MouseMove;
				_maskLayer.MouseWheel += tempImage_MouseWheel;
				_maskLayer.IsHitTestVisible = false;
				_maskLayer.Focusable = false;
				_maskLayer.IsEnabled = false;
				_maskLayer.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
				Panel.SetZIndex(_maskLayer, 100);
				imageView.AddImageLayer(_maskLayer);
				_maskLayer.SetBinding(Image.SourceProperty, new Binding("MaskImage")
				{
					Source = this
				});
				_maskLayer.SetBinding(UIElement.IsEnabledProperty, new Binding("IsChecked")
				{
					Source = mask
				});
				HikImageView hikImageView = imageView;
				hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
				Binarization();
				_IsInit = true;
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

	private void tempImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void tempImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseMove(object sender, MouseEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseEnter(object sender, MouseEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void GetBox_OnClick(object sender, RoutedEventArgs e)
	{
		OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
		IsGetFeatureModel = false;
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			IsGetBox = false;
		}
		else
		{
			if (!_changedredo)
			{
				_changedredo = true;
			}
			BoxCovers.Clear();
			_changedredo = false;
			OCRTrainDataMgr.Instance._boxes = GetBox();
			if (OCRTrainDataMgr.Instance._boxes == null)
			{
				IsGetBox = false;
			}
			else
			{
				IsGetBox = true;
				OCRTrainDataMgr.Instance.IsStartTrain = true;
				CreatBoxCover();
				AddMaskBoxUndo();
			}
		}
		OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
	}

	private void imageView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!_selectbox)
		{
			IEnumerable<RoiParameter> enumerable = from p in BoxCovers.OfType<RoiParameter>()
				where p.IsSelected
				select p;
			foreach (RoiParameter item in enumerable)
			{
				item.IsSelected = false;
			}
			SelectedBox = null;
		}
		_selectbox = false;
		if (BoxCovers.Count > 0)
		{
			OCRTrainDataMgr.Instance.IsStartTrain = true;
		}
		else
		{
			OCRTrainDataMgr.Instance.IsStartTrain = false;
		}
	}

	private void imageView_ROIClick(object sender, MouseEventArgs e)
	{
		bool? isChecked = Arrow.IsChecked;
		bool flag = true;
		if (isChecked == true == flag && isChecked.HasValue && sender is FrameworkElement frameworkElement)
		{
			SelectBox(frameworkElement.DataContext as HikCoverRoiParameter);
			int index = BoxCovers.IndexOf(SelectedBox);
			GetMarkPatParams(index);
		}
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		_IsDrawBox = true;
		string name = (sender as FrameworkElement).Name;
		if (!(name == "hikFrame"))
		{
			if (name == "hikPolygon")
			{
				imageView.PrepareRawRoi(RoiUIType.HikPolygonRoi);
				return;
			}
			imageView.UnPrepareRawRoi();
			imageView.ContextMenuVisible = false;
		}
		else
		{
			imageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		_IsDrawBox = false;
		imageView.UnPrepareRawRoi();
		imageView.ContextMenuVisible = false;
	}

	private void Mask_OnChecked(object sender, RoutedEventArgs e)
	{
		if (imageView.Source == null)
		{
			Arrow.IsChecked = true;
			return;
		}
		_rubber = new Rubber2();
		Rubber2 rubber = _rubber;
		rubber.OnRubberFinish = (Rubber2.RubberFinish)Delegate.Combine(rubber.OnRubberFinish, new Rubber2.RubberFinish(rubber_OnRubberFinish));
		Rubber2 rubber2 = _rubber;
		rubber2.OnWipe = (Rubber2.Wipe)Delegate.Combine(rubber2.OnWipe, new Rubber2.Wipe(rubber_OnWipe));
		_rubber.DataContext = this;
		_rubber.SetBinding(Rubber2.DWidthProperty, "PanRectWidth");
		_rubber.SetBinding(Rubber2.DHeightProperty, "PanRectWidth");
		_rubber.RegisterRoi(imageView);
	}

	private void rubber_OnWipe(int[] pixels, List<Point> bagPoints)
	{
		if (pixels.Length != 0)
		{
			WriteMask(pixels);
		}
	}

	private void rubber_OnRubberFinish(object sender, object data)
	{
		AddMaskBoxUndo();
	}

	private void Mask_OnUnchecked(object sender, RoutedEventArgs e)
	{
		if (_rubber != null)
		{
			_rubber.UnRegisterRoi(imageView);
			imageView.ContextMenuVisible = false;
			BindingOperations.ClearAllBindings(_rubber);
			_rubber.DataContext = null;
			if (_rubber.OnWipe != null)
			{
				Rubber2 rubber = _rubber;
				rubber.OnWipe = (Rubber2.Wipe)Delegate.Remove(rubber.OnWipe, new Rubber2.Wipe(rubber_OnWipe));
			}
			if (_rubber.OnRubberFinish != null)
			{
				Rubber2 rubber2 = _rubber;
				rubber2.OnRubberFinish = (Rubber2.RubberFinish)Delegate.Remove(rubber2.OnRubberFinish, new Rubber2.RubberFinish(rubber_OnRubberFinish));
			}
			_rubber = null;
		}
	}

	private void DeleteBox_OnClick(object sender, RoutedEventArgs e)
	{
		if (SelectedBox != null)
		{
			if (BoxCovers.Contains(SelectedBox))
			{
				BoxCovers.Remove(SelectedBox);
			}
			SelectedBox = null;
		}
	}

	private void imageView_OnRoiCountChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		AddMaskBoxUndo();
		Arrow.IsChecked = true;
	}

	private void imageView_OnRoiParamChange(object data)
	{
		if (!_IsDrawBox)
		{
			AddMaskBoxUndo();
		}
	}

	private void BackStep_OnClick(object sender, RoutedEventArgs e)
	{
		_undoEngine.Undo();
	}

	private void ForwardStep_OnClick(object sender, RoutedEventArgs e)
	{
		_undoEngine.Redo();
	}

	private void Clear_OnClick(object sender, RoutedEventArgs e)
	{
		WriteMask(new int[0], clearall: true);
		AddMaskBoxUndo();
	}

	private unsafe void imageView_OnPixelChange(int X, int Y)
	{
		string content = "--";
		string content2 = "--";
		if (X >= 0)
		{
			content = X.ToString();
		}
		if (Y >= 0)
		{
			content2 = Y.ToString();
		}
		byte b = 0;
		if (CurImage == null)
		{
			return;
		}
		ModuleCommonMethodMgr.ImageTool.Clear();
		ImageSource imageSource = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
		if (!(imageSource is WriteableBitmap { BackBuffer: var backBuffer } writeableBitmap))
		{
			return;
		}
		byte* ptr = (byte*)backBuffer.ToPointer();
		int backBufferStride = writeableBitmap.BackBufferStride;
		int num = 0;
		int num2 = backBufferStride * writeableBitmap.PixelHeight;
		MPixelFormat pixelFormatCode = ImageData.GetPixelFormatCode(writeableBitmap.Format);
		if (pixelFormatCode == MPixelFormat.Gray8)
		{
			num = Y * backBufferStride + X;
			if (num >= 0 && num < num2)
			{
				b = ptr[num];
			}
		}
		if (OCRTrainDataMgr.Instance.Owner != null)
		{
			OCRTrainDataMgr.Instance.Owner.xVal.Content = content;
			OCRTrainDataMgr.Instance.Owner.yVal.Content = content2;
			OCRTrainDataMgr.Instance.Owner.grayVal.Content = b.ToString();
		}
	}

	private void BinaryImageShow_ValueChanged(object sender, EventArgs e)
	{
		if (BinaryImage == null)
		{
			Binarization();
		}
		IsDisplayBinaryImage = (_binaryImageShow.CurValueA ? true : false);
	}

	private void GrayThresh_SetValue(object sender, EventArgs e)
	{
		if (e != null)
		{
			Binarization();
		}
	}

	private void FeatureModel_OnClick(object sender, RoutedEventArgs e)
	{
		OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
		TrainModel();
		OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
	}

	private void zoomOrgBtn_Click(object sender, RoutedEventArgs e)
	{
		imageView.Init();
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		FullScreen val = new FullScreen();
		viewGrid.Children.Remove(imageView);
		val.SetChild((UIElement)imageView);
		((Window)(object)val).ShowDialog();
		val.RemoveChild();
		viewGrid.Children.Add(imageView);
	}

	public void ThumbMouseLeftButtonDown()
	{
		if (_rubber != null)
		{
			_rubber.IsDisplayArea = true;
		}
	}

	public void ThumbMouseLeftButtonUp()
	{
		if (_rubber != null)
		{
			_rubber.IsDisplayArea = false;
		}
	}

	private void DisplayOrHide_Click(object sender, RoutedEventArgs e)
	{
		if (IsDisplayLocationParam)
		{
			IsDisplayLocationParam = false;
		}
		else
		{
			IsDisplayLocationParam = true;
		}
	}

	public void GetTargetImage()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetBoxCopyImage"];
			if (array != null && array.Length != 0)
			{
				IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				CurImage = ModuleCommonMethodMgr.Instance.GetMono8ImageData(intPtr);
			}
		}
		catch
		{
		}
	}

	public void Init()
	{
		imageView.ROITypeUI = ROITypeUIEnum.Transparent;
		GetTargetImage();
		if (CurImage != null)
		{
			imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
			if (BoxCovers == null)
			{
				BoxCovers = new ObservableCollection<object>();
			}
			if (!_changedredo)
			{
				_changedredo = true;
			}
			BoxCovers.Clear();
			_changedredo = false;
			OCRTrainDataMgr.Instance._boxes = GetBox(charExtract: false, bInit: true);
			if (OCRTrainDataMgr.Instance._boxes == null)
			{
				IsGetBox = false;
			}
			else
			{
				CreatBoxCover();
				if (BoxCovers.Count > 0)
				{
					IsGetBox = true;
					DrawPoints(isInit: true);
				}
				else
				{
					IsGetBox = false;
				}
			}
		}
		if (IsGetBox)
		{
			OCRTrainDataMgr.Instance.IsStartTrain = true;
			AddMaskBoxUndo();
			return;
		}
		OCRTrainDataMgr.Instance.IsStartTrain = false;
		_undoEngine.AddUndoUnit(new UndoEngine.UndoUnit
		{
			BoxData = null,
			MaskData = null,
			Type = 1
		});
	}

	public void DeInit()
	{
		BoxCovers.CollectionChanged -= imageView_OnRoiCountChanged;
		if (_undoEngine != null)
		{
			UndoEngine undoEngine = _undoEngine;
			undoEngine.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoEngine.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			UndoEngine undoEngine2 = _undoEngine;
			undoEngine2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoEngine2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			ClearUnredoData();
		}
		if (_grayThreshItem != null)
		{
			_grayThreshItem.SetValueCompleted -= GrayThresh_SetValue;
		}
		if (_binaryImageShow != null)
		{
			_binaryImageShow.CurValue = false;
			_binaryImageShow.OnCurValueChanged -= BinaryImageShow_ValueChanged;
		}
		_maskLayer.MouseLeftButtonDown -= tempImage_PreviewMouseLeftButtonDown;
		_maskLayer.MouseLeftButtonUp -= tempImage_PreviewMouseLeftButtonUp;
		_maskLayer.MouseEnter -= tempImage_MouseEnter;
		_maskLayer.MouseMove -= tempImage_MouseMove;
		_maskLayer.MouseWheel -= tempImage_MouseWheel;
		if (imageView != null)
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Remove(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
			imageView.Dispose();
			imageView.DataContext = null;
		}
		((ItemsControl)(object)paramDivideUI).ItemsSource = null;
		((ItemsControl)(object)paramTrainUI).ItemsSource = null;
		((ItemsControl)(object)paramLocationUI).ItemsSource = null;
		_grayThreshItem = null;
		_binaryImageShow = null;
		DataContext = null;
	}

	public void ClearUnredoData()
	{
		if (_undoEngine != null)
		{
			_undoEngine.ClearUp();
		}
	}

	private void TrainModel()
	{
		if (OCRTrainDataMgr.Instance.Param != null)
		{
			IsGetFeatureModel = false;
			OCRTrainDataMgr.Instance._boxes = Convert2MVBI_BOX_F();
			imageView.ExternPoint_Buffer = new List<byte[]>(0);
			imageView.CreatPointK();
			if (!GetFeatureModel())
			{
				OCRTrainDataMgr.Instance.Owner.btnNextStep.IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
			}
			else
			{
				IsGetFeatureModel = true;
				OCRTrainDataMgr.Instance.Owner.btnNextStep.IsEnabled = true;
			}
		}
	}

	private void CreatBoxCover()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		if (!_changedredo)
		{
			_changedredo = true;
		}
		BoxCovers.Clear();
		if (OCRTrainDataMgr.Instance._boxes != null)
		{
			MVBI_BOX_F[] boxes = OCRTrainDataMgr.Instance._boxes;
			foreach (MVBI_BOX_F val in boxes)
			{
				if (!IsOverstep(val))
				{
					BoxCovers.Add(new HikCoverRoiParameter
					{
						Width = val.width,
						Height = val.height,
						Angle = val.angle,
						CenterX = val.center_x,
						CenterY = val.center_y,
						ForceShowSelected = true
					});
				}
			}
		}
		_changedredo = false;
	}

	private MVBI_BOX_F[] GetBox(bool charExtract = true, bool bInit = false)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return null;
		}
		if (charExtract)
		{
			try
			{
				((IUserStringData)OCRTrainDataMgr.Instance.Param)["CharExtractTrain"] = "";
			}
			catch
			{
				return null;
			}
		}
		try
		{
			byte[] array = null;
			array = ((!bInit) ? ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetExtractBoxArr"] : ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetModelBoxArr"]);
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

	public void Binarization()
	{
		if (OCRTrainDataMgr.Instance.Param != null)
		{
			IntPtr intPtr = BinarizationLocal();
			if (intPtr != IntPtr.Zero)
			{
				BinaryImage = ModuleCommonMethodMgr.Instance.GetMono8ImageData(intPtr);
				Marshal.FreeHGlobal(intPtr);
			}
		}
	}

	private unsafe IntPtr BinarizationLocal()
	{
		if (OCRTrainDataMgr.Instance.Param == null || CurImage == null)
		{
			return IntPtr.Zero;
		}
		ImageSource imageSource = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
		if (!(imageSource is WriteableBitmap writeableBitmap))
		{
			return IntPtr.Zero;
		}
		byte b = 128;
		if (_grayThreshItem != null)
		{
			b = byte.Parse(_grayThreshItem.CurValue.ToString());
		}
		IntPtr result = Marshal.AllocHGlobal(8 + writeableBitmap.PixelWidth * writeableBitmap.PixelHeight);
		byte* ptr = (byte*)result.ToPointer();
		byte* ptr2 = (byte*)writeableBitmap.BackBuffer.ToPointer();
		*(int*)ptr = writeableBitmap.PixelWidth;
		((int*)ptr)[1] = writeableBitmap.PixelHeight;
		int backBufferStride = writeableBitmap.BackBufferStride;
		int pixelHeight = writeableBitmap.PixelHeight;
		int pixelWidth = writeableBitmap.PixelWidth;
		int num = 8;
		for (int i = 0; i < pixelHeight; i++)
		{
			for (int j = 0; j < pixelWidth; j++)
			{
				byte* ptr3 = ptr2 + i * backBufferStride;
				ptr3 += j;
				ptr[num] = (byte)((*ptr3 > b) ? byte.MaxValue : 0);
				num++;
			}
		}
		return result;
	}

	private unsafe void AddMaskBoxUndo()
	{
		if (_changedredo)
		{
			return;
		}
		if (MaskImage == null)
		{
			WriteMask(new int[0], clearall: true);
		}
		if (!(MaskImage is WriteableBitmap))
		{
			return;
		}
		IntPtr intPtr = MaskThreshold(out var len);
		if (intPtr == IntPtr.Zero)
		{
			return;
		}
		void* ptr = intPtr.ToPointer();
		byte[] array = new byte[len];
		fixed (byte* ptr2 = array)
		{
			for (int i = 0; i < len; i++)
			{
				ptr2[i] = ((byte*)ptr)[i];
			}
		}
		UndoEngine.UndoUnit unit = new UndoEngine.UndoUnit
		{
			Type = 0,
			MaskData = array,
			BoxData = GetBoxBuffer()
		};
		_undoEngine.AddUndoUnit(unit);
		Marshal.FreeHGlobal(intPtr);
	}

	private unsafe IntPtr MaskThreshold(out int len)
	{
		len = 0;
		if (MaskImage == null)
		{
			WriteMask(new int[0], clearall: true);
		}
		if (!(MaskImage is WriteableBitmap writeableBitmap))
		{
			return IntPtr.Zero;
		}
		int num = 8;
		len = num + writeableBitmap.PixelWidth * writeableBitmap.PixelHeight;
		IntPtr result = Marshal.AllocHGlobal(len);
		byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
		int backBufferStride = writeableBitmap.BackBufferStride;
		int num2 = writeableBitmap.Format.BitsPerPixel / 8;
		void* ptr2 = result.ToPointer();
		*(int*)ptr2 = writeableBitmap.PixelWidth;
		((int*)ptr2)[1] = writeableBitmap.PixelHeight;
		byte* ptr3 = (byte*)ptr2 + num;
		int pixelHeight = writeableBitmap.PixelHeight;
		int pixelWidth = writeableBitmap.PixelWidth;
		for (int i = 0; i < pixelHeight; i++)
		{
			byte* ptr4 = ptr + i * backBufferStride;
			for (int j = 0; j < pixelWidth; j++)
			{
				*ptr3 = (byte)((ptr4[j * num2] == 0) ? byte.MaxValue : 0);
				ptr3++;
			}
		}
		return result;
	}

	private MVBI_BOX_F[] Convert2MVBI_BOX_F()
	{
		return BoxCovers.OfType<HikCoverRoiParameter>().Select((Func<HikCoverRoiParameter, MVBI_BOX_F>)((HikCoverRoiParameter p) =>
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			return new MVBI_BOX_F
			{
				angle = (float)p.Angle,
				center_x = (float)p.CenterX,
				center_y = (float)p.CenterY,
				height = (float)p.Height,
				width = (float)p.Width
			};
		})).ToArray();
	}

	private unsafe byte[] GetBoxBuffer()
	{
		MVBI_BOX_F[] array = Convert2MVBI_BOX_F();
		byte[] array2 = new byte[ModuleDefine.RECT_ROI_SIZE * array.Length];
		fixed (MVBI_BOX_F* ptr = array)
		{
			byte* ptr2 = (byte*)ptr;
			int num = array2.Length;
			fixed (byte* ptr3 = array2)
			{
				for (int i = 0; i < num; i++)
				{
					ptr3[i] = ptr2[i];
				}
			}
		}
		return array2;
	}

	private unsafe void RedoUndo(object sender, UndoEngine.UndoUnit undoUnit)
	{
		_changedredo = true;
		if (undoUnit.Type == 0)
		{
			if (undoUnit.MaskData != null)
			{
				if (!(MaskImage is WriteableBitmap writeableBitmap))
				{
					return;
				}
				writeableBitmap.Lock();
				byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
				int num = 8;
				int backBufferStride = writeableBitmap.BackBufferStride;
				fixed (byte* maskData = undoUnit.MaskData)
				{
					byte* ptr2 = maskData + num;
					int pixelHeight = writeableBitmap.PixelHeight;
					int pixelWidth = writeableBitmap.PixelWidth;
					for (int i = 0; i < pixelHeight; i++)
					{
						byte* ptr3 = ptr + i * backBufferStride;
						for (int j = 0; j < pixelWidth; j++)
						{
							int num2 = ((*ptr2 == 0) ? 1291786778 : 0);
							*(int*)(ptr3 + j * 4) = num2;
							ptr2++;
						}
					}
				}
				writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
				writeableBitmap.Unlock();
			}
			int num3 = undoUnit.BoxData.Length / ModuleDefine.RECT_ROI_SIZE;
			OCRTrainDataMgr.Instance._boxes = new MVBI_BOX_F[num3];
			fixed (MVBI_BOX_F* boxes = OCRTrainDataMgr.Instance._boxes)
			{
				byte* ptr4 = (byte*)boxes;
				int num4 = undoUnit.BoxData.Length;
				fixed (byte* boxData = undoUnit.BoxData)
				{
					for (int k = 0; k < num4; k++)
					{
						ptr4[k] = boxData[k];
					}
				}
			}
			CreatBoxCover();
		}
		else
		{
			BoxCovers.Clear();
			WriteMask(new int[0], clearall: true);
		}
		_changedredo = false;
	}

	private unsafe void WriteMask(int[] indexs, bool clearall = false)
	{
		if (CurImage == null)
		{
			return;
		}
		ImageSource imageSource = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
		if (!(imageSource is WriteableBitmap writeableBitmap))
		{
			return;
		}
		WriteableBitmap patMaskImage = _patMaskImage;
		if (patMaskImage == null || patMaskImage.Width != (double)writeableBitmap.PixelWidth || patMaskImage.Height != (double)writeableBitmap.PixelHeight)
		{
			_patMaskImage = new WriteableBitmap(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight, 96.0, 96.0, PixelFormats.Bgra32, null);
			clearall = false;
		}
		MaskImage = _patMaskImage;
		if (!(MaskImage is WriteableBitmap writeableBitmap2))
		{
			return;
		}
		Int32Rect dirtyRect = new Int32Rect(0, 0, writeableBitmap2.PixelWidth, writeableBitmap2.PixelHeight);
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			int num6 = num5 % dirtyRect.Width;
			int num7 = num5 / dirtyRect.Width;
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
		writeableBitmap2.Lock();
		byte* ptr = (byte*)writeableBitmap2.BackBuffer.ToPointer();
		uint* ptr2 = (uint*)ptr;
		int num8 = writeableBitmap2.BackBufferStride / 4;
		if (clearall)
		{
			int num9 = dirtyRect.Height + dirtyRect.Y;
			int num10 = dirtyRect.Width + dirtyRect.X;
			for (int j = dirtyRect.Y; j < num9; j++)
			{
				uint* ptr3 = ptr2 + j * num8;
				for (int k = 0; k < num10; k++)
				{
					ptr3[k] = 0u;
				}
			}
		}
		fixed (int* ptr4 = indexs)
		{
			uint num11 = 1291786778u;
			for (int l = num2; l < num4 + 1; l++)
			{
				for (int m = num; m < num3 + 1; m++)
				{
					uint* ptr5 = ptr2 + l * num8 + m;
					*ptr5 = num11;
				}
			}
		}
		writeableBitmap2.AddDirtyRect(dirtyRect);
		writeableBitmap2.Unlock();
	}

	private bool Overstep()
	{
		if (CurImage == null)
		{
			return false;
		}
		Rect rect = new Rect(0.0, 0.0, CurImage.Width - 1, CurImage.Height - 1);
		bool flag = false;
		int num = 0;
		while (num < BoxCovers.Count)
		{
			if (!(BoxCovers[num] is HikCoverRoiParameter hikCoverRoiParameter))
			{
				continue;
			}
			Rect rect2 = new Rect(hikCoverRoiParameter.OriginPoint.X, hikCoverRoiParameter.OriginPoint.Y, hikCoverRoiParameter.Width, hikCoverRoiParameter.Height);
			Matrix matrix = default;
			matrix.RotateAt(hikCoverRoiParameter.Angle, hikCoverRoiParameter.CenterX, hikCoverRoiParameter.CenterY);
			rect2.Transform(matrix);
			Point[] array = rect2.RectToPoints();
			Point[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Point point = array2[i];
				if (Math.Round(point.X) < rect.Left)
				{
					flag = true;
					break;
				}
				if (Math.Round(point.X) > rect.Right)
				{
					flag = true;
					break;
				}
				if (Math.Round(point.Y) < rect.Top)
				{
					flag = true;
					break;
				}
				if (Math.Round(point.Y) > rect.Bottom)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
			num++;
		}
		if (!flag)
		{
			return false;
		}
		SelectBox((HikCoverRoiParameter)BoxCovers[num]);
		return true;
	}

	private bool IsOverstep(MVBI_BOX_F box)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		if (CurImage == null)
		{
			return false;
		}
		Rect rect = new Rect(0.0, 0.0, CurImage.Width - 1, CurImage.Height - 1);
		bool flag = false;
		HikCoverRoiParameter hikCoverRoiParameter = new HikCoverRoiParameter
		{
			Width = box.width,
			Height = box.height,
			Angle = box.angle,
			CenterX = box.center_x,
			CenterY = box.center_y,
			ForceShowSelected = true
		};
		Rect rect2 = new Rect(hikCoverRoiParameter.OriginPoint.X, hikCoverRoiParameter.OriginPoint.Y, hikCoverRoiParameter.Width, hikCoverRoiParameter.Height);
		Matrix matrix = default;
		matrix.RotateAt(hikCoverRoiParameter.Angle, hikCoverRoiParameter.CenterX, hikCoverRoiParameter.CenterY);
		rect2.Transform(matrix);
		Point[] array = rect2.RectToPoints();
		Point[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			Point point = array2[i];
			if (Math.Round(point.X) < rect.Left)
			{
				flag = true;
				break;
			}
			if (Math.Round(point.X) > rect.Right)
			{
				flag = true;
				break;
			}
			if (Math.Round(point.Y) < rect.Top)
			{
				flag = true;
				break;
			}
			if (Math.Round(point.Y) > rect.Bottom)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		return true;
	}

	private void SelectBox(HikCoverRoiParameter box)
	{
		foreach (RoiParameter boxCover in BoxCovers)
		{
			boxCover.IsSelected = false;
			if (!boxCover.ForceShowSelected)
			{
				boxCover.ForceShowSelected = true;
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

	private unsafe void GetMarkPatParams(int index)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		float num = 1f;
		float num2 = 1f;
		byte b = 0;
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)[$"GetModelParams_{index}"];
			if (array != null && array.Length >= 9)
			{
				void* ptr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
				num = *(float*)ptr;
				num2 = ((float*)ptr)[1];
				b = ((byte*)ptr + 4)[4];
			}
		}
		catch
		{
		}
		if (((ItemsControl)(object)paramTrainUI).ItemsSource != null)
		{
			Float obj2 = ((ItemsControl)(object)paramTrainUI).ItemsSource.OfType<Float>().FirstOrDefault((Float p) => p.Name == "GranularityCoarse");
			if (obj2 != null)
			{
				obj2.Value = Convert.ToString(num);
				obj2.CurValue = num;
			}
			Float obj3 = ((ItemsControl)(object)paramTrainUI).ItemsSource.OfType<Float>().FirstOrDefault((Float p) => p.Name == "GranularityFine");
			if (obj3 != null)
			{
				obj3.Value = Convert.ToString(num2);
				obj3.CurValue = num2;
			}
			Integer integer = ((ItemsControl)(object)paramTrainUI).ItemsSource.OfType<Integer>().FirstOrDefault((Integer p) => p.Name == "ModelThreshold");
			if (integer != null)
			{
				integer.Value = Convert.ToString(b);
				integer.CurValue = b;
			}
		}
	}

	private bool GetFeatureModel(bool featumatch = false)
	{
		if (!featumatch)
		{
			if (BoxCovers.Count > 100)
			{
				string strMsg = string.Format(AppLocalizationService.GetLang("Lang_OCV_OVERLEN"), 100);
				HikMessageBox.Show(AppLocalizationService.GetLang("训练模板失败"), strMsg, MessageIcons.Warning);
				return false;
			}
			HikCoverRoiParameter hikCoverRoiParameter = BoxCovers.OfType<HikCoverRoiParameter>().FirstOrDefault((HikCoverRoiParameter p) => p.Width < 5.0 || p.Height < 5.0);
			if (hikCoverRoiParameter != null)
			{
				SelectBox(hikCoverRoiParameter);
				HikMessageBox.Show("训练模板失败", "高亮处字符框宽高小于能力集5，请调整大小后重试！", MessageIcons.Warning, isTranslate: true);
				return false;
			}
			if (Overstep())
			{
				HikMessageBox.Show("训练模板失败", "高亮处字符框超出图像范围，请调整大小后重试！", MessageIcons.Warning, isTranslate: true);
				return false;
			}
			try
			{
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["AdjustMarkBox"] = GetBoxBuffer();
			}
			catch (Exception ex)
			{
				int result = 0;
				string strMsg2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				HikMessageBox.Show(strMsg2, MessageIcons.Error);
				return false;
			}
			IntPtr intPtr = MaskThreshold(out var len);
			if (intPtr == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				byte[] array = new byte[len];
				Marshal.Copy(intPtr, array, 0, len);
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetExactPatMaskImage"] = array;
			}
			catch
			{
				Marshal.FreeHGlobal(intPtr);
				return false;
			}
			Marshal.FreeHGlobal(intPtr);
			try
			{
				OCRTrainDataMgr.Instance.Param.TimeOut = 2147483647u;
				byte[] value = new byte[1];
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["ExactModelTrain"] = value;
				OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
			}
			catch (Exception ex2)
			{
				OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
				DrawPoints();
				if (OCRTrainDataMgr.Instance._boxes != null)
				{
					byte[] array2 = null;
					try
					{
						array2 = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetMarkStatus"];
						if (array2 != null && array2.Length != 0)
						{
							for (int num = 0; num < array2.Length; num++)
							{
								if (array2[num] == 0)
								{
									SelectBox(BoxCovers[num] as HikCoverRoiParameter);
									break;
								}
							}
						}
					}
					catch
					{
					}
				}
				int result2 = 0;
				string text = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
				string strMsg3 = AppLocalizationService.GetLang("当前图像或高亮部分无法提取特征点！") + "\n(" + AppLocalizationService.GetLang("errInfo:") + text + ")";
				HikMessageBox.Show(AppLocalizationService.GetLang("训练模板失败"), strMsg3, MessageIcons.Error);
				return false;
			}
		}
		DrawPoints();
		return true;
	}

	private unsafe void DrawPoints(bool isInit = false)
	{
		byte[] array = null;
		try
		{
			array = ((!isInit) ? ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetFeatureModel"] : ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetModelFeature"]);
			if (array == null || array.Length == 0)
			{
				return;
			}
			int num = array.Length;
			IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
			byte[] array2 = new byte[num];
			byte* ptr = (byte*)intPtr.ToPointer();
			fixed (byte* ptr2 = array2)
			{
				for (int i = 0; i < num; i++)
				{
					ptr2[i] = ptr[i];
				}
			}
			List<byte[]> externPoint_Buffer = new List<byte[]> { array2 };
			imageView.ExternOpacity = 1.0;
			imageView.ExternPoint_Buffer = externPoint_Buffer;
			imageView.CreatPointK();
			if (isInit)
			{
				IsGetFeatureModel = true;
			}
		}
		catch
		{
		}
	}
}
