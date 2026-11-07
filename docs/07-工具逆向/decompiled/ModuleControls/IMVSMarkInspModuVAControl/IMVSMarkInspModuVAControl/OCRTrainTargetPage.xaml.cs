using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
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
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Apps.XmlUI;
using HikPcUI.ImageView;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility;
using VMBaseControls;
using VMBaseControls.Base.ImageView;
using VMControls.BaseInterface;
using VMControls.RenderInterface;

namespace IMVSMarkInspModuVAControl;

public partial class OCRTrainTargetPage : UserControl, INotifyPropertyChanged, IComponentConnector
{
	private Integer _grayThreshItem;

	private Apps.XmlParser.ParamTab.Boolean _binaryImageShow;

	private System.Windows.Controls.Image _maskLayer = new System.Windows.Controls.Image();

	private bool _selectbox = false;

	private RubberControl _rubber;

	private UndoEngine _undoDetectTarget = new UndoEngine();

	private UndoEngine _undoFinePositioning = new UndoEngine();

	private UndoEngine _undoAddMask = new UndoEngine();

	private int _PanRectWidth = 10;

	private bool _IsInit = false;

	private bool _IsDrawBox = false;

	private bool _IsDisplayLocationParam = false;

	private MVBI_BOX_F[] _charBoxes = null;

	private byte[] curMaskData = null;

	private byte[] _positioningMaskData = null;

	private byte[] _addMaskData = null;

	private bool _IsGetFeatureModel = false;

	private bool _IsFirstDrawPoints = true;

	private const int ButtonWidthCH = 74;

	private const int ButtonWidthEN = 95;

	private const int PageLeftWidthCH = 598;

	private const int PageLeftWidthEN = 572;

	private const int ParamLeftWidthCH = 105;

	private const int ParamLeftWidthEN = 160;

	public static readonly DependencyProperty IsDisplayBinaryImageProperty = DependencyProperty.Register("IsDisplayBinaryImage", typeof(bool), typeof(OCRTrainTargetPage), new PropertyMetadata(false));

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BinaryImageProperty = DependencyProperty.Register("BinaryImage", typeof(ImageData), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty DetectRoiListProperty = DependencyProperty.Register("DetectRoiList", typeof(ObservableCollection<object>), typeof(OCRTrainTargetPage), new PropertyMetadata(new ObservableCollection<object>()));

	public static readonly DependencyProperty CharRoiListProperty = DependencyProperty.Register("CharRoiList", typeof(ObservableCollection<object>), typeof(OCRTrainTargetPage), new PropertyMetadata(new ObservableCollection<object>()));

	public static readonly DependencyProperty SelectedBoxProperty = DependencyProperty.Register("SelectedBox", typeof(HikCoverRoiParameter), typeof(OCRTrainTargetPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty IsStateForDrawROIProperty = DependencyProperty.Register("IsStateForDrawROI", typeof(bool), typeof(OCRTrainTargetPage), new PropertyMetadata(false));

	public static readonly DependencyProperty DrawROITypeProperty = DependencyProperty.Register("DrawROIType", typeof(Type), typeof(OCRTrainTargetPage), new PropertyMetadata(null));

	private ObservableCollection<IList> _ROILists;

	private ObservableCollection<object> shapeList = new ObservableCollection<object>();

	private ICommand _AddROICommand;

	private ICommand _RoiSelectedCommand = null;

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

	public bool IsGetFeatureModel
	{
		get
		{
			return _IsGetFeatureModel;
		}
		set
		{
			if (_IsGetFeatureModel != value)
			{
				_IsGetFeatureModel = value;
				OnPropertyChanged("IsGetFeatureModel");
				OCRTrainDataMgr.Instance.Owner?.UpdateNextStep();
			}
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

	public ObservableCollection<object> DetectRoiList
	{
		get
		{
			return (ObservableCollection<object>)GetValue(DetectRoiListProperty);
		}
		set
		{
			SetValue(DetectRoiListProperty, value);
		}
	}

	public ObservableCollection<object> CharRoiList
	{
		get
		{
			return (ObservableCollection<object>)GetValue(CharRoiListProperty);
		}
		set
		{
			SetValue(CharRoiListProperty, value);
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

	public ObservableCollection<IList> ROILists
	{
		get
		{
			if (_ROILists == null)
			{
				_ROILists = new ObservableCollection<IList>();
			}
			return _ROILists;
		}
	}

	public ObservableCollection<object> ShapeList => shapeList;

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

	public double PageLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 572 : 598;

	public double ParamLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 160 : 105;

	public ICommand AddROICommand => _AddROICommand ?? (_AddROICommand = (ICommand)new DelegateCommand<object>((Action<object>)AddROICommandExecute));

	public ICommand RoiSelectedCommand => _RoiSelectedCommand ?? (_RoiSelectedCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiSelectedCommandExecute));

	public event PropertyChangedEventHandler PropertyChanged;

	public OCRTrainTargetPage()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!_IsInit)
			{
				UndoEngine undoDetectTarget = _undoDetectTarget;
				undoDetectTarget.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoDetectTarget.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				UndoEngine undoDetectTarget2 = _undoDetectTarget;
				undoDetectTarget2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoDetectTarget2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				UndoEngine undoFinePositioning = _undoFinePositioning;
				undoFinePositioning.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoFinePositioning.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				UndoEngine undoFinePositioning2 = _undoFinePositioning;
				undoFinePositioning2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoFinePositioning2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				UndoEngine undoAddMask = _undoAddMask;
				undoAddMask.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoAddMask.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
				UndoEngine undoAddMask2 = _undoAddMask;
				undoAddMask2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Combine(undoAddMask2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
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
				ImageViewBox imageViewBox = this.imageViewBox;
				imageViewBox.OnFreshExtendContent = (ImageViewBox.FreshExtendContent)Delegate.Combine(imageViewBox.OnFreshExtendContent, new ImageViewBox.FreshExtendContent(imageView_OnFreshExtendContent));
				_maskLayer.MouseLeftButtonDown += tempImage_PreviewMouseLeftButtonDown;
				_maskLayer.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
				_maskLayer.MouseEnter += tempImage_MouseEnter;
				_maskLayer.MouseMove += tempImage_MouseMove;
				_maskLayer.MouseWheel += tempImage_MouseWheel;
				_maskLayer.IsHitTestVisible = false;
				_maskLayer.Focusable = true;
				_maskLayer.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
				Panel.SetZIndex(_maskLayer, 100);
				if (this.imageViewBox.ExtenAbove is Canvas canvas && !canvas.Children.Contains(_maskLayer))
				{
					canvas.Children.Add(_maskLayer);
				}
				_IsInit = true;
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			UndoEngine undoDetectTarget = _undoDetectTarget;
			undoDetectTarget.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoDetectTarget.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			UndoEngine undoDetectTarget2 = _undoDetectTarget;
			undoDetectTarget2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoDetectTarget2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			UndoEngine undoFinePositioning = _undoFinePositioning;
			undoFinePositioning.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoFinePositioning.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			UndoEngine undoFinePositioning2 = _undoFinePositioning;
			undoFinePositioning2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoFinePositioning2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			UndoEngine undoAddMask = _undoAddMask;
			undoAddMask.ReDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoAddMask.ReDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			UndoEngine undoAddMask2 = _undoAddMask;
			undoAddMask2.UnDo = (EventHandler<UndoEngine.UndoUnit>)Delegate.Remove(undoAddMask2.UnDo, new EventHandler<UndoEngine.UndoUnit>(RedoUndo));
			ClearUnredoData();
			_undoDetectTarget = null;
			_undoFinePositioning = null;
			_undoAddMask = null;
			if (_grayThreshItem != null)
			{
				_grayThreshItem.SetValueCompleted -= GrayThresh_SetValue;
			}
			if (_binaryImageShow != null)
			{
				_binaryImageShow.CurValue = false;
				_binaryImageShow.OnCurValueChanged -= BinaryImageShow_ValueChanged;
			}
			((ItemsControl)(object)paramDivideUI).ItemsSource = null;
			((ItemsControl)(object)paramTrainUI).ItemsSource = null;
			((ItemsControl)(object)paramLocationUI).ItemsSource = null;
			_grayThreshItem = null;
			_binaryImageShow = null;
			_maskLayer.MouseLeftButtonUp -= tempImage_PreviewMouseLeftButtonUp;
			_maskLayer.MouseEnter -= tempImage_MouseEnter;
			_maskLayer.MouseMove -= tempImage_MouseMove;
			_maskLayer.MouseWheel -= tempImage_MouseWheel;
			if (this.imageViewBox.ExtenAbove != null)
			{
				(this.imageViewBox.ExtenAbove as Canvas).Children.Remove(_maskLayer);
			}
			ImageViewBox imageViewBox = this.imageViewBox;
			imageViewBox.OnFreshExtendContent = (ImageViewBox.FreshExtendContent)Delegate.Remove(imageViewBox.OnFreshExtendContent, new ImageViewBox.FreshExtendContent(imageView_OnFreshExtendContent));
			this.imageViewBox.DataContext = null;
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
		imageViewBox.SourceImage.RaiseEvent(e);
	}

	private void tempImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		imageViewBox.SourceImage.RaiseEvent(e);
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

	private void GetBox_OnClick(object sender, RoutedEventArgs e)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
		imageViewBox.ClearExternShapes();
		ShapeList.Clear();
		_charBoxes = GetBox();
		Binarization();
		List<object> rects = OCRTrainDataMgr.Instance.GetRects(_charBoxes);
		if (rects != null)
		{
			foreach (object item in rects)
			{
				ShapeList.Add(item);
			}
		}
		AddToCharRoiList(_charBoxes);
		OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
	}

	private void imageView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Invalid comparison between Unknown and I4
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Invalid comparison between Unknown and I4
		if (!_selectbox)
		{
			if (OCRTrainDataMgr.Instance.Owner == null)
			{
				return;
			}
			ObservableCollection<object> source = new ObservableCollection<object>();
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
			{
				source = DetectRoiList;
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
			{
				source = CharRoiList;
			}
			IEnumerable<RoiParameter> enumerable = from p in source.OfType<RoiParameter>()
				where p.IsSelected
				select p;
			foreach (RoiParameter item in enumerable)
			{
				item.IsSelected = false;
			}
			if (SelectedBox != null)
			{
				SelectedBox = null;
			}
		}
		_selectbox = false;
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		DrawMask();
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		_IsDrawBox = true;
		string name = (sender as FrameworkElement).Name;
		if (!(name == "hikFrame"))
		{
			if (name == "hikPolygon")
			{
				DrawROIType = typeof(HikPolygonRoiParameter);
				IsStateForDrawROI = true;
			}
			else
			{
				IsStateForDrawROI = false;
				Arrow.IsChecked = true;
			}
		}
		else
		{
			DrawROIType = typeof(HikCoverRoiParameter);
			IsStateForDrawROI = true;
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		IsStateForDrawROI = false;
	}

	private void Mask_OnChecked(object sender, RoutedEventArgs e)
	{
		if (imageViewBox.Source == null)
		{
			Arrow.IsChecked = true;
			return;
		}
		_rubber = new RubberControl();
		RubberControl rubber = _rubber;
		rubber.OnRubberFinish = (RubberControl.RubberFinish)Delegate.Combine(rubber.OnRubberFinish, new RubberControl.RubberFinish(rubber_OnRubberFinish));
		RubberControl rubber2 = _rubber;
		rubber2.OnWipe = (RubberControl.Wipe)Delegate.Combine(rubber2.OnWipe, new RubberControl.Wipe(rubber_OnWipe));
		_rubber.DataContext = this;
		_rubber.SetBinding(RubberControl.DWidthProperty, "PanRectWidth");
		_rubber.SetBinding(RubberControl.DHeightProperty, "PanRectWidth");
		DrawROIType = typeof(ToolROIModel);
		IsStateForDrawROI = true;
		_rubber.RegisterRoi(imageViewBox);
		_maskLayer.IsHitTestVisible = true;
	}

	private void rubber_OnWipe(int[] pixels, List<System.Windows.Point> bagPoints)
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
			_rubber.UnRegisterRoi(imageViewBox);
			IsStateForDrawROI = false;
			BindingOperations.ClearAllBindings(_rubber);
			_rubber.DataContext = null;
			if (_rubber.OnWipe != null)
			{
				RubberControl rubber = _rubber;
				rubber.OnWipe = (RubberControl.Wipe)Delegate.Remove(rubber.OnWipe, new RubberControl.Wipe(rubber_OnWipe));
			}
			if (_rubber.OnRubberFinish != null)
			{
				RubberControl rubber2 = _rubber;
				rubber2.OnRubberFinish = (RubberControl.RubberFinish)Delegate.Remove(rubber2.OnRubberFinish, new RubberControl.RubberFinish(rubber_OnRubberFinish));
			}
			_rubber = null;
			_maskLayer.IsHitTestVisible = false;
		}
	}

	private void DeleteBox_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Invalid comparison between Unknown and I4
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Invalid comparison between Unknown and I4
		if (SelectedBox == null || OCRTrainDataMgr.Instance.Owner == null)
		{
			return;
		}
		bool flag = false;
		if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
		{
			if (DetectRoiList.Contains(SelectedBox))
			{
				DetectRoiList.Remove(SelectedBox);
				flag = true;
			}
		}
		else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2 && CharRoiList.Contains(SelectedBox))
		{
			CharRoiList.Remove(SelectedBox);
			flag = true;
		}
		if (flag)
		{
			SelectedBox = null;
			AddMaskBoxUndo();
		}
	}

	protected virtual void AddROICommandExecute(object Roi)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Invalid comparison between Unknown and I4
		IsStateForDrawROI = false;
		if (Roi is RoiParameter roiParameter)
		{
			roiParameter.ForceShowSelected = true;
		}
		if (OCRTrainDataMgr.Instance.Owner == null)
		{
			return;
		}
		if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1 && DetectRoiList.Count > 16)
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				if (DetectRoiList.Contains(Roi))
				{
					DetectRoiList.Remove(Roi);
				}
				string strMsg = string.Format(AppLocalizationService.GetLang("Lang_DetectROILimitWarn"), 16);
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), strMsg, MessageIcons.Warning);
			}));
			Arrow.IsChecked = true;
		}
		else
		{
			AddRoiDelegate(Roi as IROI);
			AddMaskBoxUndo();
			Arrow.IsChecked = true;
		}
	}

	private void RoiSelectedCommandExecute(RoiWithTagEventArgs args)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Invalid comparison between Unknown and I4
		bool? isChecked = Arrow.IsChecked;
		bool flag = true;
		if (isChecked == true == flag && isChecked.HasValue && OCRTrainDataMgr.Instance.Owner != null && args.Roi != null)
		{
			SelectBox(args.Roi as HikCoverRoiParameter);
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
			{
				int index = CharRoiList.IndexOf(SelectedBox);
				GetMarkPatParams(index);
			}
		}
	}

	protected void AddRoiDelegate(IROI roi)
	{
		roi.DataChangedCommand = (ICommand)new DelegateCommand<IROI>((Action<IROI>)RoiParamChange);
		(roi as RoiBaseParameter)?.NotifyPropertyChanged("DataChangedCommand");
		(roi as RoiParameter)?.NotifyPropertyChanged("DataChangedCommand");
	}

	private void RoiParamChange(object data)
	{
		if (!_IsDrawBox)
		{
			AddMaskBoxUndo();
		}
		else
		{
			_IsDrawBox = false;
		}
	}

	private void BackStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Invalid comparison between Unknown and I4
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Invalid comparison between Unknown and I4
		if (OCRTrainDataMgr.Instance.Owner != null)
		{
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
			{
				_undoDetectTarget.Undo();
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
			{
				_undoFinePositioning.Undo();
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 3)
			{
				_undoAddMask.Undo();
			}
		}
	}

	private void ForwardStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Invalid comparison between Unknown and I4
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Invalid comparison between Unknown and I4
		if (OCRTrainDataMgr.Instance.Owner != null)
		{
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
			{
				_undoDetectTarget.Redo();
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
			{
				_undoFinePositioning.Redo();
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 3)
			{
				_undoAddMask.Redo();
			}
		}
	}

	private void Clear_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		if (OCRTrainDataMgr.Instance.Owner == null)
		{
			return;
		}
		if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
		{
			if (DetectRoiList.Count == 0)
			{
				return;
			}
			DetectRoiList.Clear();
			if (SelectedBox != null)
			{
				SelectedBox = null;
			}
		}
		else
		{
			WriteMask(new int[0], clearall: true);
		}
		AddMaskBoxUndo();
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
		imageViewBox.Init();
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		FullScreen val = new FullScreen();
		viewGrid.Children.Remove(imageViewBox);
		val.SetChild((UIElement)imageViewBox);
		((Window)(object)val).ShowDialog();
		val.RemoveChild();
		viewGrid.Children.Add(imageViewBox);
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

	private void SetMask_OnClick(object sender, RoutedEventArgs e)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		int len = 0;
		bool bHasMask = false;
		try
		{
			IntPtr intPtr = MaskThreshold(out len, out bHasMask);
			if (!(intPtr == IntPtr.Zero))
			{
				byte[] array = null;
				if (bHasMask)
				{
					array = new byte[len];
					Marshal.Copy(intPtr, array, 0, len);
				}
				else
				{
					array = new byte[8];
				}
				Marshal.FreeHGlobal(intPtr);
				OCRTrainDataMgr.Instance.Param.TimeOut = 2147483647u;
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetMarkInspMask"] = array;
				OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
				HikMessageBox.Show("Lang_SetSuc", MessageIcons.Ok, isTranslate: true);
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		RecoveryRect(moduleTrainData.DetectRoiData, DetectRoiList);
		RecoveryRect(moduleTrainData.CharRoiData, CharRoiList);
		_charBoxes = MVBI_BOX_F.GetBoxesWithoutNum(moduleTrainData.CharBoxesData);
		_positioningMaskData = moduleTrainData.PositioningMaskData;
		_addMaskData = moduleTrainData.AddMaskData;
		UndoEngine.UndoUnit unit = new UndoEngine.UndoUnit
		{
			Type = 0,
			MaskData = null,
			BoxData = moduleTrainData.DetectRoiData
		};
		_undoDetectTarget.AddUndoUnit(unit);
		UndoEngine.UndoUnit unit2 = new UndoEngine.UndoUnit
		{
			Type = 0,
			MaskData = moduleTrainData.PositioningMaskData,
			BoxData = moduleTrainData.CharRoiData
		};
		_undoFinePositioning.AddUndoUnit(unit2);
		UndoEngine.UndoUnit unit3 = new UndoEngine.UndoUnit
		{
			Type = 0,
			MaskData = moduleTrainData.AddMaskData,
			BoxData = null
		};
		_undoAddMask.AddUndoUnit(unit3);
		OnPropertyChanged("ROILists");
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		moduleTrainData.DetectRoiData = GetRoiBuffer(DetectRoiList);
		moduleTrainData.CharBoxesData = GetBoxBuffer(_charBoxes);
		moduleTrainData.CharRoiData = GetRoiBuffer(CharRoiList);
		SaveInfo();
		moduleTrainData.PositioningMaskData = _positioningMaskData;
		moduleTrainData.AddMaskData = _addMaskData;
		DetectRoiList.Clear();
		CharRoiList.Clear();
		_charBoxes = null;
		_positioningMaskData = null;
		_addMaskData = null;
		CurImage = null;
		BinaryImage = null;
		curMaskData = null;
		SelectedBox = null;
		DataContext = null;
	}

	public void ClearData()
	{
		DetectRoiList.Clear();
		CharRoiList.Clear();
		imageViewBox.ClearExternShapes();
		ShapeList.Clear();
		_charBoxes = null;
		_positioningMaskData = null;
		_addMaskData = null;
		CurImage = null;
		BinaryImage = null;
		curMaskData = null;
		SelectedBox = null;
		IsGetFeatureModel = false;
		ClearUnredoData();
		if (_binaryImageShow != null && _binaryImageShow.CurValueA)
		{
			_binaryImageShow.CurValueA = false;
			IsDisplayBinaryImage = false;
		}
	}

	public void ClearUnredoData()
	{
		if (_undoDetectTarget != null)
		{
			_undoDetectTarget.ClearUp();
		}
		if (_undoFinePositioning != null)
		{
			_undoFinePositioning.ClearUp();
		}
		if (_undoAddMask != null)
		{
			_undoAddMask.ClearUp();
		}
	}

	public void UpdateInfo()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Invalid comparison between Unknown and I4
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Invalid comparison between Unknown and I4
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Invalid comparison between Unknown and I4
		if (OCRTrainDataMgr.Instance.Owner == null)
		{
			return;
		}
		ROILists.Clear();
		if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
		{
			Button backStep = BackStep;
			object dataContext = (ForwardStep.DataContext = _undoDetectTarget);
			backStep.DataContext = dataContext;
			imageViewBox.ClearExternShapes();
			ShapeList.Clear();
			WriteMask(new int[0], clearall: true);
			List<object> rects = OCRTrainDataMgr.Instance.GetRects(_charBoxes);
			if (rects != null)
			{
				foreach (object item in rects)
				{
					ShapeList.Add(item);
				}
			}
			ROILists.Add(DetectRoiList);
		}
		else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
		{
			Button backStep2 = BackStep;
			object dataContext = (ForwardStep.DataContext = _undoFinePositioning);
			backStep2.DataContext = dataContext;
			imageViewBox.ClearExternShapes();
			ShapeList.Clear();
			RecoveryMaskData(_positioningMaskData);
			DrawPoints(_IsFirstDrawPoints);
			Arrow.IsChecked = true;
			ROILists.Add(CharRoiList);
		}
		else
		{
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep != 3)
			{
				return;
			}
			Button backStep3 = BackStep;
			object dataContext = (ForwardStep.DataContext = _undoAddMask);
			backStep3.DataContext = dataContext;
			imageViewBox.ClearExternShapes();
			ShapeList.Clear();
			RecoveryMaskData(_addMaskData);
			MVBI_BOX_F[] array = CharRoiList.OfType<HikCoverRoiParameter>().Select((Func<HikCoverRoiParameter, MVBI_BOX_F>)((HikCoverRoiParameter p) =>
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
			if (array != null && array.Length != 0)
			{
				List<object> rects2 = OCRTrainDataMgr.Instance.GetRects(array);
				if (rects2 != null)
				{
					foreach (object item2 in rects2)
					{
						ShapeList.Add(item2);
					}
				}
			}
			Arrow.IsChecked = true;
		}
	}

	public void SaveInfo()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Invalid comparison between Unknown and I4
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Invalid comparison between Unknown and I4
		if (OCRTrainDataMgr.Instance.Owner == null)
		{
			return;
		}
		ObservableCollection<object> source = new ObservableCollection<object>();
		if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
		{
			source = DetectRoiList;
		}
		else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
		{
			_positioningMaskData = GetMaskBuffer();
			source = CharRoiList;
		}
		else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 3)
		{
			_addMaskData = GetMaskBuffer();
		}
		IEnumerable<RoiParameter> enumerable = from p in source.OfType<RoiParameter>()
			where p.IsSelected
			select p;
		foreach (RoiParameter item in enumerable)
		{
			item.IsSelected = false;
		}
		if (SelectedBox != null)
		{
			SelectedBox = null;
		}
	}

	private void TrainModel()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		IsGetFeatureModel = false;
		object obj = ShapeList.FirstOrDefault((object p) => p is IMultiPoint);
		if (obj != null)
		{
			ShapeList.Remove(obj);
		}
		imageViewBox.ClearExternShapes();
		if (!GetFeatureModel())
		{
			if (OCRTrainDataMgr.Instance.Owner != null)
			{
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
			}
		}
		else
		{
			IsGetFeatureModel = true;
		}
	}

	private void RecoveryMaskData(byte[] data)
	{
		if (CurImage == null)
		{
			return;
		}
		if (data == null)
		{
			WriteMask(new int[0], clearall: true);
			return;
		}
		if (curMaskData == null)
		{
			curMaskData = new byte[CurImage.Width * CurImage.Height];
		}
		int num = 0;
		for (int i = 8; i < data.Length; i++)
		{
			if (num < curMaskData.Length)
			{
				if (data[i] == byte.MaxValue)
				{
					curMaskData[num] = 0;
				}
				else
				{
					curMaskData[num] = 1;
				}
				num++;
			}
		}
		DrawMask();
	}

	private void RecoveryRect(byte[] data, ObservableCollection<object> roiList)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		OCRTrainDataMgr.Instance.IsNoRecord = true;
		if (roiList == null)
		{
			roiList = new ObservableCollection<object>();
		}
		else
		{
			roiList.Clear();
		}
		MVBI_BOX_F[] boxesWithoutNum = MVBI_BOX_F.GetBoxesWithoutNum(data);
		if (boxesWithoutNum != null)
		{
			MVBI_BOX_F[] array = boxesWithoutNum;
			foreach (MVBI_BOX_F val in array)
			{
				roiList.Add(new HikCoverRoiParameter
				{
					Width = val.width,
					Height = val.height,
					CenterX = val.center_x,
					CenterY = val.center_y,
					Angle = val.angle,
					ForceShowSelected = true
				});
			}
		}
		OCRTrainDataMgr.Instance.IsNoRecord = false;
	}

	private MVBI_BOX_F[] GetBox(bool bInit = false)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return null;
		}
		if (!bInit)
		{
			try
			{
				byte[] array = null;
				if (DetectRoiList == null || DetectRoiList.Count == 0)
				{
					if (CurImage != null)
					{
						array = ModuleCommonMethodMgr.Instance.GetOneRoiBuffer((HikCoverRoiParameter)null, (float)CurImage.Width, (float)CurImage.Height);
					}
				}
				else
				{
					array = GetRoiBuffer(DetectRoiList);
				}
				if (array != null)
				{
					((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetMarkSegmBox"] = array;
				}
			}
			catch (Exception ex)
			{
				int result = 0;
				string strMsg = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				HikMessageBox.Show(strMsg, MessageIcons.Error);
				return null;
			}
			try
			{
				OCRTrainDataMgr.Instance.Param.TimeOut = 2147483647u;
				byte[] value = new byte[1];
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["CharExtractTrain"] = value;
				OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
			}
			catch
			{
				return null;
			}
		}
		try
		{
			byte[] array2 = null;
			array2 = ((!bInit) ? ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetExtractBoxArr"] : ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetModelBoxArr"]);
			if (array2 != null && array2.Length != 0)
			{
				return MVBI_BOX_F.GetBoxesWithoutNum(array2);
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
			BinaryImage = GetBinaryImage();
		}
	}

	private unsafe ImageData GetBinaryImage()
	{
		ImageData imageData = null;
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetMarkSegmBinaImage"];
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
			LogHelper.moduleLog.Error("GetBinaryImage error: " + text);
		}
		return imageData;
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

	private void AddMaskBoxUndo()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Invalid comparison between Unknown and I4
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Invalid comparison between Unknown and I4
		if (!OCRTrainDataMgr.Instance.IsNoRecord && OCRTrainDataMgr.Instance.Owner != null)
		{
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
			{
				UndoEngine.UndoUnit unit = new UndoEngine.UndoUnit
				{
					Type = 0,
					MaskData = null,
					BoxData = GetRoiBuffer(DetectRoiList)
				};
				_undoDetectTarget.AddUndoUnit(unit);
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
			{
				UndoEngine.UndoUnit unit2 = new UndoEngine.UndoUnit
				{
					Type = 0,
					MaskData = GetMaskBuffer(),
					BoxData = GetRoiBuffer(CharRoiList)
				};
				_undoFinePositioning.AddUndoUnit(unit2);
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 3)
			{
				UndoEngine.UndoUnit unit3 = new UndoEngine.UndoUnit
				{
					Type = 0,
					MaskData = GetMaskBuffer(),
					BoxData = null
				};
				_undoAddMask.AddUndoUnit(unit3);
			}
		}
	}

	private unsafe IntPtr MaskThreshold(out int len, out bool bHasMask)
	{
		len = 0;
		bHasMask = false;
		if (CurImage == null)
		{
			return IntPtr.Zero;
		}
		if (curMaskData == null)
		{
			curMaskData = new byte[CurImage.Width * CurImage.Height];
		}
		int num = CurImage.Width * CurImage.Height;
		int num2 = 8;
		len = num2 + num;
		IntPtr result = Marshal.AllocHGlobal(len);
		fixed (byte* ptr = curMaskData)
		{
			void* ptr2 = result.ToPointer();
			*(int*)ptr2 = CurImage.Width;
			((int*)ptr2)[1] = CurImage.Height;
			byte* ptr3 = (byte*)ptr2 + num2;
			for (int i = 0; i < num; i++)
			{
				if (ptr[i] == 0)
				{
					*ptr3 = byte.MaxValue;
				}
				else
				{
					bHasMask = true;
					*ptr3 = 0;
				}
				ptr3++;
			}
		}
		return result;
	}

	private unsafe byte[] GetMaskBuffer()
	{
		int len = 0;
		bool bHasMask = false;
		IntPtr intPtr = MaskThreshold(out len, out bHasMask);
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		byte* ptr = (byte*)intPtr.ToPointer();
		byte[] array = new byte[len];
		fixed (byte* ptr2 = array)
		{
			for (int i = 0; i < len; i++)
			{
				ptr2[i] = ptr[i];
			}
		}
		Marshal.FreeHGlobal(intPtr);
		return array;
	}

	private byte[] GetRoiBuffer(ObservableCollection<object> rectList)
	{
		if (rectList == null || rectList.Count == 0)
		{
			return null;
		}
		MVBI_BOX_F[] boxes = rectList.OfType<HikCoverRoiParameter>().Select((Func<HikCoverRoiParameter, MVBI_BOX_F>)((HikCoverRoiParameter p) =>
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
		return GetBoxBuffer(boxes);
	}

	private unsafe byte[] GetBoxBuffer(MVBI_BOX_F[] boxes)
	{
		if (boxes == null || boxes.Length == 0)
		{
			return null;
		}
		byte[] array = new byte[ModuleDefine.RECT_ROI_SIZE * boxes.Length];
		fixed (MVBI_BOX_F* ptr = boxes)
		{
			byte* ptr2 = (byte*)ptr;
			int num = array.Length;
			fixed (byte* ptr3 = array)
			{
				for (int i = 0; i < num; i++)
				{
					ptr3[i] = ptr2[i];
				}
			}
		}
		return array;
	}

	private void RedoUndo(object sender, UndoEngine.UndoUnit undoUnit)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Invalid comparison between Unknown and I4
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Invalid comparison between Unknown and I4
		OCRTrainDataMgr.Instance.IsNoRecord = true;
		if (undoUnit.Type == 0)
		{
			RecoveryMaskData(undoUnit.MaskData);
			if (OCRTrainDataMgr.Instance.Owner != null)
			{
				if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
				{
					RecoveryRect(undoUnit.BoxData, DetectRoiList);
				}
				else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
				{
					RecoveryRect(undoUnit.BoxData, CharRoiList);
				}
			}
		}
		else
		{
			ClearMaskBox();
		}
		OCRTrainDataMgr.Instance.IsNoRecord = false;
	}

	private void ClearMaskBox()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Invalid comparison between Unknown and I4
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Invalid comparison between Unknown and I4
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Invalid comparison between Unknown and I4
		if (OCRTrainDataMgr.Instance.Owner != null)
		{
			if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 1)
			{
				DetectRoiList.Clear();
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 2)
			{
				CharRoiList.Clear();
				WriteMask(new int[0], clearall: true);
			}
			else if ((int)OCRTrainDataMgr.Instance.Owner.CurTrainStep == 3)
			{
				WriteMask(new int[0], clearall: true);
			}
		}
	}

	private void WriteMask(int[] indexs, bool clearall = false)
	{
		if (CurImage == null)
		{
			return;
		}
		if (indexs.Length == 0)
		{
			_maskLayer.Source = null;
			curMaskData = null;
			return;
		}
		if (curMaskData == null)
		{
			curMaskData = new byte[CurImage.Width * CurImage.Height];
		}
		Int32Rect int32Rect = new Int32Rect(0, 0, CurImage.Width, CurImage.Height);
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			curMaskData[num5] = 1;
			int num6 = num5 % int32Rect.Width;
			int num7 = num5 / int32Rect.Width;
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
		DrawMask(new System.Drawing.Point(num, num2), num3 - num + 1, num4 - num2 + 1, clearall);
	}

	private void DrawMask()
	{
		if (CurImage != null)
		{
			DrawMask(new System.Drawing.Point(0, 0), CurImage.Width, CurImage.Height);
		}
	}

	private unsafe void DrawMask(System.Drawing.Point point, int sWidth, int sHeight, bool isNormal = true)
	{
		if (imageViewBox == null || imageViewBox.Source == null || curMaskData == null)
		{
			return;
		}
		if (imageViewBox.ExtenAbove is Canvas canvas && !canvas.Children.Contains(_maskLayer))
		{
			canvas.Children.Add(_maskLayer);
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
		WriteableBitmap writeableBitmap = ModuleCommonMethodMgr.ImageTool.Init(num3, num4, PixelFormats.Bgra32);
		if (writeableBitmap == null)
		{
			return;
		}
		_maskLayer.Source = writeableBitmap;
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
		fixed (byte* ptr4 = curMaskData)
		{
			uint num6 = 0u;
			int num7 = (int)Math.Ceiling(onePixelWidth);
			int width = CurImage.Width;
			int height = CurImage.Height;
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
						if (ptr4[num8] != 0)
						{
							num6 = ((ptr4[num8] <= 10) ? 1291786778u : 2583632410u);
							uint* ptr5 = ptr2 + k * num5 + l;
							*ptr5 = num6;
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

	private bool Overstep()
	{
		if (CurImage == null)
		{
			return false;
		}
		System.Windows.Rect rect = new System.Windows.Rect(0.0, 0.0, CurImage.Width - 1, CurImage.Height - 1);
		bool flag = false;
		int num = 0;
		while (num < CharRoiList.Count)
		{
			if (!(CharRoiList[num] is HikCoverRoiParameter hikCoverRoiParameter))
			{
				continue;
			}
			System.Windows.Rect rect2 = new System.Windows.Rect(hikCoverRoiParameter.OriginPoint.X, hikCoverRoiParameter.OriginPoint.Y, hikCoverRoiParameter.Width, hikCoverRoiParameter.Height);
			Matrix matrix = default;
			matrix.RotateAt(hikCoverRoiParameter.Angle, hikCoverRoiParameter.CenterX, hikCoverRoiParameter.CenterY);
			rect2.Transform(matrix);
			System.Windows.Point[] array = rect2.RectToPoints();
			System.Windows.Point[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				System.Windows.Point point = array2[i];
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
		SelectBox((HikCoverRoiParameter)CharRoiList[num]);
		return true;
	}

	private void SelectBox(HikCoverRoiParameter box)
	{
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
		byte b = 1;
		int num3 = 1;
		int num4 = 1;
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
			string value = ((IUserStringData)OCRTrainDataMgr.Instance.Param)["PyramidScaleFlag"];
			num3 = Convert.ToInt32(value);
			value = ((IUserStringData)OCRTrainDataMgr.Instance.Param)["EdgeThresholdFlag"];
			num4 = Convert.ToInt32(value);
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			LogHelper.moduleLog.Error("GetMarkPatParams error: " + text);
		}
		if (((ItemsControl)(object)paramTrainUI).ItemsSource == null)
		{
			return;
		}
		if (num3 == 1)
		{
			Float obj = ((ItemsControl)(object)paramTrainUI).ItemsSource.OfType<Float>().FirstOrDefault((Float p) => p.Name == "PyramidScaleRough");
			if (obj != null)
			{
				obj.Value = Convert.ToString(num);
				obj.CurValue = num;
			}
			Float obj2 = ((ItemsControl)(object)paramTrainUI).ItemsSource.OfType<Float>().FirstOrDefault((Float p) => p.Name == "PyramidScaleFine");
			if (obj2 != null)
			{
				obj2.Value = Convert.ToString(num2);
				obj2.CurValue = num2;
			}
		}
		if (num4 == 1)
		{
			Integer integer = ((ItemsControl)(object)paramTrainUI).ItemsSource.OfType<Integer>().FirstOrDefault((Integer p) => p.Name == "EdgeThreshold");
			if (integer != null)
			{
				integer.Value = Convert.ToString(b);
				integer.CurValue = b;
			}
		}
	}

	private bool GetFeatureModel()
	{
		if (CharRoiList.Count > 100)
		{
			string text = string.Format(AppLocalizationService.GetLang("Lang_OCV_OVERLEN"), 100);
			LogHelper.moduleLog.Warn(text);
			HikMessageBox.Show(AppLocalizationService.GetLang("训练模板失败"), text, MessageIcons.Warning);
			return false;
		}
		HikCoverRoiParameter hikCoverRoiParameter = CharRoiList.OfType<HikCoverRoiParameter>().FirstOrDefault((HikCoverRoiParameter p) => p.Width < 5.0 || p.Height < 5.0);
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
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["AdjustMarkBox"] = GetRoiBuffer(CharRoiList);
			int len = 0;
			bool bHasMask = false;
			intPtr = MaskThreshold(out len, out bHasMask);
			if (intPtr == IntPtr.Zero)
			{
				return false;
			}
			byte[] array = null;
			if (bHasMask)
			{
				array = new byte[len];
				Marshal.Copy(intPtr, array, 0, len);
			}
			else
			{
				array = new byte[8];
			}
			Marshal.FreeHGlobal(intPtr);
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetExactPatMaskImage"] = array;
		}
		catch (Exception ex)
		{
			int result = 0;
			string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			LogHelper.moduleLog.Error(text2);
			HikMessageBox.Show(AppLocalizationService.GetLang("训练模板失败"), text2, MessageIcons.Error);
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
			return false;
		}
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
			int result2 = 0;
			string text3 = "";
			byte[] array2 = null;
			try
			{
				array2 = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetExactPatCreateStatus"];
				if (array2 != null && array2.Length != 0)
				{
					int[] array3 = new int[CharRoiList.Count];
					IntPtr source = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
					Marshal.Copy(source, array3, 0, array3.Length);
					for (int num = 0; num < array3.Length; num++)
					{
						if (array3[num] == 0)
						{
							SelectBox(CharRoiList[num] as HikCoverRoiParameter);
							break;
						}
					}
				}
			}
			catch (Exception ex3)
			{
				text3 = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex3.Message);
				LogHelper.moduleLog.Error("GetExactPatCreateStatus error: " + text3);
			}
			text3 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
			string text4 = AppLocalizationService.GetLang("当前图像或高亮部分无法提取特征点！") + "\n(" + AppLocalizationService.GetLang("errInfo:") + text3 + ")";
			HikMessageBox.Show(AppLocalizationService.GetLang("训练模板失败"), text4, MessageIcons.Error);
			LogHelper.moduleLog.Error("ExactModelTrain error: " + text4);
			return false;
		}
		DrawPoints();
		return true;
	}

	private void DrawPoints(bool isInit = false)
	{
		byte[] array = null;
		try
		{
			if (isInit)
			{
				array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetModelFeature"];
			}
			else
			{
				_IsFirstDrawPoints = false;
				array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetFeatureModel"];
			}
			if (array != null && array.Length != 0)
			{
				MyOutlinePoint myOutlinePoint = new MyOutlinePoint();
				myOutlinePoint.OutLineInfo = array;
				myOutlinePoint.ColorA = "#FF00FF00";
				myOutlinePoint.NgColorA = "#FFFF0000";
				myOutlinePoint.OpacityA = 1.0;
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
					multiPoint2.OutlinePoints.Clear();
					multiPoint2.OutlinePoints.Add(myOutlinePoint);
					ShapeList.Clear();
					imageViewBox?.ClearExternShapes();
					ShapeList.Add(multiPoint2);
				}
				if (isInit)
				{
					IsGetFeatureModel = true;
				}
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			LogHelper.moduleLog.Error("DrawPoints error: " + text);
			if (isInit)
			{
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbAddMask).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
			}
		}
	}

	private void AddToCharRoiList(MVBI_BOX_F[] boxes)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		CharRoiList.Clear();
		if (boxes != null)
		{
			foreach (MVBI_BOX_F val in boxes)
			{
				CharRoiList.Add(new HikCoverRoiParameter
				{
					CenterX = val.center_x,
					CenterY = val.center_y,
					Width = val.width,
					Height = val.height,
					Angle = val.angle,
					ForceShowSelected = true
				});
			}
		}
		UndoEngine.UndoUnit unit = new UndoEngine.UndoUnit
		{
			Type = 0,
			MaskData = _positioningMaskData,
			BoxData = GetRoiBuffer(CharRoiList)
		};
		_undoFinePositioning.AddUndoUnit(unit);
	}
}
