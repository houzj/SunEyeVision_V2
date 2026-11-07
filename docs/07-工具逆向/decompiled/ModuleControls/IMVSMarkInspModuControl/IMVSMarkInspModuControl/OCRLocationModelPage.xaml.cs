using System;
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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.XmlUI;
using Hik.Xml;
using HikExternalCall.ImageProcess;
using HikExternalCall.PatCreate;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility;
using VMControls.BaseInterface;

namespace IMVSMarkInspModuControl;

public partial class OCRLocationModelPage : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public bool NeedBuild;

	public byte[] ModelData;

	public List<object> needSkipData = new List<object>();

	private ImageData _Image;

	private System.Windows.Controls.Image tempImage = new System.Windows.Controls.Image();

	private HikCross Cross;

	private bool IsSetCrossByManual = false;

	private bool ClickRoiFlag = true;

	private List<string> history = new List<string>();

	private int historyIndex = -1;

	private Rubber2 rubber;

	private Byte2ImageSource MaskSourceConvertor = new Byte2ImageSource();

	private object lockobj = new object();

	private int _PanRectWidth = 10;

	private FeaturepatMatchDialogData featurepatMatchDialogData;

	private bool _IsInit = false;

	private bool _IsDisplaySearchParam = false;

	private bool _IsEnterROIMode = false;

	private bool _IsHasCurImage = false;

	private bool _selectbox = false;

	public static readonly DependencyProperty IsDrawRectProperty = DependencyProperty.Register("IsDrawRect", typeof(bool), typeof(OCRLocationModelPage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectEnableProperty = DependencyProperty.Register("RectEnable", typeof(bool), typeof(OCRLocationModelPage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectRoisProperty = DependencyProperty.Register("RectRois", typeof(ObservableCollection<object>), typeof(OCRLocationModelPage), new PropertyMetadata(null));

	public static readonly DependencyProperty SelectedBoxProperty = DependencyProperty.Register("SelectedBox", typeof(HikCoverRoiParameter), typeof(OCRLocationModelPage), new PropertyMetadata((object)null));

	internal ResultOverviewPopupButton setBtn;

	internal ParamListView paramSearchUI;

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
				ShowImage();
				historyIndex = -1;
				RecordHistory();
				NotifyPropertyChanged("BackEnable");
				NotifyPropertyChanged("ForwardEnable");
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
				if (CurImage != null && (value > CurImage.Width || value > CurImage.Height))
				{
					_PanRectWidth = Convert.ToInt32((CurImage.Width > CurImage.Height) ? CurImage.Height : CurImage.Width);
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
			if (_IsEnterROIMode != value)
			{
				_IsEnterROIMode = value;
				NotifyPropertyChanged("IsEnterROIMode");
			}
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

	public double TextFontSize => OCRTrainDataMgr.Instance.IsEnglish ? 12 : 14;

	public double ParamLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 118 : 90;

	public double PageLeftWidth => OCRTrainDataMgr.Instance.IsEnglish ? 580 : 598;

	public double ROITextWidth => OCRTrainDataMgr.Instance.IsEnglish ? 125 : 100;

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
				NeedBuild = true;
				HikImageView hikImageView = imageView;
				hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
				HikImageView hikImageView2 = imageView;
				hikImageView2.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView2.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
				HikImageView hikImageView3 = imageView;
				hikImageView3.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Combine(hikImageView3.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
				HikImageView hikImageView4 = imageView;
				hikImageView4.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView4.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
				imageView.MouseLeftButtonDown += imageView_MouseLeftButtonDown;
				HikImageView hikImageView5 = imageView;
				hikImageView5.OnRoi_Click = (HikImageView.Roi_Click)Delegate.Combine(hikImageView5.OnRoi_Click, new HikImageView.Roi_Click(imageView_OnRoiClick));
				HikImageView hikImageView6 = imageView;
				hikImageView6.OnFreshExtendContent = (HikImageView.FreshExtendContent)Delegate.Combine(hikImageView6.OnFreshExtendContent, new HikImageView.FreshExtendContent(imageView_OnFreshExtendContent));
				HikImageView hikImageView7 = roiImageView;
				hikImageView7.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView7.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
				HikImageView hikImageView8 = roiImageView;
				hikImageView8.OnDrawRoi_MouseUpChange = (HikImageView.DrawRoi_MouseUpChange)Delegate.Combine(hikImageView8.OnDrawRoi_MouseUpChange, new HikImageView.DrawRoi_MouseUpChange(imageView_DrawRoi_MouseUpChange));
				tempImage.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
				tempImage.PreviewKeyUp += tempImage_PreviewKeyUp;
				tempImage.MouseEnter += tempImage_MouseEnter;
				tempImage.MouseMove += tempImage_MouseMove;
				tempImage.MouseWheel += tempImage_MouseWheel;
				tempImage.IsHitTestVisible = false;
				tempImage.Focusable = true;
				tempImage.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
				imageView.AddAboveChild(tempImage);
				Panel.SetZIndex(tempImage, 100);
				if (Cross == null)
				{
					Cross = new HikCross();
					Binding binding = new Binding
					{
						Mode = BindingMode.TwoWay,
						Path = new PropertyPath("FixPointX")
					};
					Cross.SetBinding(HikCross.XProperty, binding);
					binding = new Binding
					{
						Mode = BindingMode.TwoWay,
						Path = new PropertyPath("FixPointY")
					};
					Cross.SetBinding(HikCross.YProperty, binding);
				}
				Cross.RegisterRoi(imageView);
				Cross.UnRegisterRoi();
				_IsInit = true;
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Remove(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
			HikImageView hikImageView2 = imageView;
			hikImageView2.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Remove(hikImageView2.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
			HikImageView hikImageView3 = imageView;
			hikImageView3.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Remove(hikImageView3.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
			HikImageView hikImageView4 = imageView;
			hikImageView4.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView4.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
			imageView.MouseLeftButtonDown -= imageView_MouseLeftButtonDown;
			HikImageView hikImageView5 = imageView;
			hikImageView5.OnRoi_Click = (HikImageView.Roi_Click)Delegate.Remove(hikImageView5.OnRoi_Click, new HikImageView.Roi_Click(imageView_OnRoiClick));
			HikImageView hikImageView6 = imageView;
			hikImageView6.OnFreshExtendContent = (HikImageView.FreshExtendContent)Delegate.Remove(hikImageView6.OnFreshExtendContent, new HikImageView.FreshExtendContent(imageView_OnFreshExtendContent));
			HikImageView hikImageView7 = roiImageView;
			hikImageView7.OnPixelChange = (HikImageView.PixelChange)Delegate.Remove(hikImageView7.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
			HikImageView hikImageView8 = roiImageView;
			hikImageView8.OnDrawRoi_MouseUpChange = (HikImageView.DrawRoi_MouseUpChange)Delegate.Remove(hikImageView8.OnDrawRoi_MouseUpChange, new HikImageView.DrawRoi_MouseUpChange(imageView_DrawRoi_MouseUpChange));
			tempImage.MouseLeftButtonUp -= tempImage_PreviewMouseLeftButtonUp;
			tempImage.PreviewKeyUp -= tempImage_PreviewKeyUp;
			tempImage.MouseEnter -= tempImage_MouseEnter;
			tempImage.MouseMove -= tempImage_MouseMove;
			tempImage.MouseWheel -= tempImage_MouseWheel;
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
		if (ChangeImageWarning())
		{
			InitData();
			DoClear();
			CurImage = OCRTrainDataMgr.Instance.CurImageData;
			needSkipData.Clear();
			OCRTrainDataMgr.Instance.SynDataProcess(SynDataOccasion.SetCurrentImage);
			if (RectRois.Count > 0)
			{
				RectRois.Clear();
				RectEnable = false;
				SelectedBox = null;
			}
		}
	}

	private void SelectOtherImage_OnClick(object sender, RoutedEventArgs e)
	{
		string path = ConfigurationHelper.INIGetDefaultOpenPath(OCRTrainDataMgr.Instance.Module?.ModuleDir);
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			ImageProcessManager.GetMono8ImageDataByFileDialog(ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer, ref path);
			if (imageData.ImageBuffer == null)
			{
				return;
			}
			if (ChangeImageWarning())
			{
				InitData();
				DoClear();
				CurImage = imageData;
				needSkipData.Clear();
				OCRTrainDataMgr.Instance.SynDataProcess(SynDataOccasion.SetOtherImage, imageData);
				if (RectRois.Count > 0)
				{
					RectRois.Clear();
					RectEnable = false;
					SelectedBox = null;
				}
			}
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), OCRTrainDataMgr.Instance.Module?.ModuleDir);
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
			imageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
			break;
		case "hikPolygon":
			imageView.PrepareRawRoi(RoiUIType.HikPolygonRoi);
			break;
		case "hikCoverCircle":
			imageView.PrepareRawRoi(RoiUIType.HikCoverCircleRoi);
			break;
		case "hikCross":
			imageView.PrepareRawRoi(RoiUIType.HikTool);
			Cross.RegisterRoi(imageView);
			break;
		default:
			imageView.UnPrepareRawRoi();
			break;
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement).Name == "hikCross" && Cross != null)
		{
			Cross.UnRegisterRoi();
		}
		imageView.UnPrepareRawRoi();
	}

	public void GenerateModel_OnClick(object sender, RoutedEventArgs e)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		try
		{
			OCRTrainDataMgr.Instance.Owner.btnNextStep.IsEnabled = false;
			Arrow.IsChecked = true;
			ReFreshMask(((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask);
			FeaturePatCreateByEraser();
			SetRoiData();
			try
			{
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["RoughModelTrain"] = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ModelData;
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				string text2 = AppLocalizationService.GetLang("Lang_SetCoarsePositioningFailure") + "\r\n(";
				text2 += AppLocalizationService.GetLang("errInfo:");
				text2 += text;
				text2 += ")";
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), text2, MessageIcons.Error);
				throw new Exception();
			}
			OCRTrainDataMgr.Instance.Owner.btnNextStep.IsEnabled = true;
		}
		catch
		{
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbTrainRegion).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbTrainTarget).IsEnabled = false;
			((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
		}
	}

	private void Rubber_Checked(object sender, RoutedEventArgs e)
	{
		this.rubber = new Rubber2();
		Rubber2 rubber = this.rubber;
		rubber.OnRubberFinish = (Rubber2.RubberFinish)Delegate.Combine(rubber.OnRubberFinish, new Rubber2.RubberFinish(rubber_OnRubberFinish));
		Rubber2 rubber2 = this.rubber;
		rubber2.OnWipe = (Rubber2.Wipe)Delegate.Combine(rubber2.OnWipe, new Rubber2.Wipe(rubber_OnWipe));
		this.rubber.DataContext = this;
		this.rubber.SetBinding(Rubber2.DWidthProperty, "PanRectWidth");
		this.rubber.SetBinding(Rubber2.DHeightProperty, "PanRectWidth");
		this.rubber.RegisterRoi(imageView);
		tempImage.IsHitTestVisible = true;
	}

	private void Rubber_Unchecked(object sender, RoutedEventArgs e)
	{
		if (rubber != null)
		{
			rubber.UnRegisterRoi(imageView);
		}
		tempImage.IsHitTestVisible = false;
	}

	private void rubber_OnRubberFinish(object sender, object Data)
	{
		RecordHistory();
	}

	private void rubber_OnWipe(int[] indexs, List<System.Windows.Point> bagPoints)
	{
		if (featurepatMatchDialogData == null || CurImage == null)
		{
			return;
		}
		byte[] bytes = BitConverter.GetBytes(1291786778);
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask == null)
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask = new byte[CurImage.Width * CurImage.Height];
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask[num5] = 1;
			int num6 = num5 % CurImage.Width;
			int num7 = num5 / CurImage.Width;
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
		imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
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
		if (IsEnterROIMode)
		{
			roiImageView.Init();
		}
		else
		{
			imageView.Init();
		}
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected Obj, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected Obj, but got Unknown
		if (IsEnterROIMode)
		{
			FullScreen val = new FullScreen();
			roiViewGrid.Children.Remove(roiImageView);
			val.SetChild((UIElement)roiImageView);
			((Window)(object)val).ShowDialog();
			val.RemoveChild();
			roiViewGrid.Children.Add(roiImageView);
		}
		else
		{
			FullScreen val2 = new FullScreen();
			viewGrid.Children.Remove(imageView);
			val2.SetChild((UIElement)imageView);
			((Window)(object)val2).ShowDialog();
			val2.RemoveChild();
			viewGrid.Children.Add(imageView);
		}
	}

	private void DeleteRoi_OnClick(object sender, RoutedEventArgs e)
	{
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Contains((sender as FrameworkElement).Tag))
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Remove((sender as FrameworkElement).Tag);
		}
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
		if (!(imageView.Source is WriteableBitmap { BackBuffer: var backBuffer } writeableBitmap))
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

	private void ItemsData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		AutoSetFixPoint();
	}

	private void imageView_OnRoiCountChanged(object Roi, EventArgs e = null)
	{
		if (hikCross.IsChecked == true)
		{
			IsSetCrossByManual = true;
		}
		Arrow.IsChecked = true;
	}

	private void imageView_OnRoiDeCountChange(object Roi)
	{
		RecordHistory();
	}

	private void imageView_OnRoiParamChange(object Date)
	{
		AutoSetFixPoint();
		if (needSkipData.Contains(Date))
		{
			needSkipData.Remove(Date);
		}
		else
		{
			RecordHistory();
		}
	}

	private void imageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (Arrow.IsChecked != true)
		{
			return;
		}
		double radio = imageView.Radio;
		System.Windows.Point position = e.GetPosition(imageView.SourceImage);
		position.X = Math.Floor(position.X / radio);
		position.Y = Math.Floor(position.Y / radio);
		if (CurImage == null || position.X < 0.0 || position.X >= (double)CurImage.Width || position.Y < 0.0 || position.Y >= (double)CurImage.Height)
		{
			return;
		}
		int num = (int)(position.Y * (double)CurImage.Width + position.X);
		if (ClickRoiFlag)
		{
			foreach (RoiParameter itemsDatum in ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData)
			{
				itemsDatum.IsSelected = false;
			}
		}
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
				if (!ClickRoiFlag)
				{
					foreach (RoiParameter itemsDatum2 in ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData)
					{
						itemsDatum2.IsSelected = false;
					}
				}
			}
			else if (b == 0)
			{
				ReFreshMask(otherMask);
			}
		}
		ClickRoiFlag = true;
	}

	private void imageView_OnRoiClick(object sender, MouseEventArgs e)
	{
		ClickRoiFlag = false;
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		DrawMask();
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
		imageView.SourceImage.RaiseEvent(e);
	}

	private void FullScreen_Click(object sender, RoutedEventArgs e)
	{
		roiImageView.UnPrepareRawRoi();
		roiImageView.ContextMenuVisible = false;
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
		roiImageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
	}

	private void imageView_DrawRoi_MouseUpChange(object sender, MouseEventArgs e)
	{
		roiImageView.UnPrepareRawRoi();
		roiImageView.ContextMenuVisible = false;
		IsDrawRect = false;
		RectEnable = true;
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

	private void imageView_ROIClick(object sender, MouseEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement)
		{
			SelectBox(frameworkElement.DataContext as HikCoverRoiParameter);
		}
	}

	private void DisplayOrHide_Click(object sender, RoutedEventArgs e)
	{
		if (IsDisplaySearchParam)
		{
			IsDisplaySearchParam = false;
			IsEnterROIMode = false;
			return;
		}
		IsDisplaySearchParam = true;
		roiImageView.ImageDataSource = imageView.ImageDataSource;
		roiImageView.PointsData = imageView.PointsData;
		IsEnterROIMode = true;
	}

	private void SwitchMode_Click(object sender, RoutedEventArgs e)
	{
		if (IsEnterROIMode)
		{
			IsEnterROIMode = false;
			return;
		}
		roiImageView.ImageDataSource = imageView.ImageDataSource;
		roiImageView.PointsData = imageView.PointsData;
		IsEnterROIMode = true;
	}

	private void InitData()
	{
		NeedBuild = true;
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
		if (CurImage != null)
		{
			imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
			DrawMask(new System.Drawing.Point(0, 0), (imageView.Source as WriteableBitmap).PixelWidth, (imageView.Source as WriteableBitmap).PixelHeight);
		}
	}

	private unsafe void DrawMask(System.Drawing.Point point, int sWidth, int sHeight, bool isNormal = true)
	{
		if (imageView.Source == null || ((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask == null)
		{
			return;
		}
		System.Windows.Point imageOffset = imageView.ImageOffset;
		double onePixelWidth = imageView.OnePixelWidth;
		int num = 0;
		int num2 = 0;
		int num3 = (int)imageView.ViewWidth;
		int num4 = (int)imageView.ViewHeight;
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
			int pixelWidth = (imageView.Source as WriteableBitmap).PixelWidth;
			int pixelHeight = (imageView.Source as WriteableBitmap).PixelHeight;
			int num8 = 0;
			int num9 = Math.Max((int)((double)point.X * onePixelWidth + imageOffset.X), 0);
			int num10 = Math.Max((int)((double)point.Y * onePixelWidth + imageOffset.Y), 0);
			int num11 = Math.Min((int)((double)(point.X + sWidth) * onePixelWidth + imageOffset.X) + 1, num3);
			int num12 = Math.Min((int)((double)(point.Y + sHeight) * onePixelWidth + imageOffset.Y) + 1, num4);
			for (int k = num10; k < num12; k++)
			{
				for (int l = num9; l < num11; l++)
				{
					num = (int)(((double)l - imageOffset.X) / onePixelWidth);
					if (num < 0 || num >= pixelWidth)
					{
						continue;
					}
					num2 = (int)(((double)k - imageOffset.Y) / onePixelWidth);
					if (num2 >= 0 && num2 < pixelHeight)
					{
						num8 = num2 * pixelWidth + num;
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
			if (CurImage == null)
			{
				HikMessageBox.Show("Lang_OCRDefect_TrainModel", "Lang_NoImageErr", MessageIcons.Error, isTranslate: true);
				throw new Exception();
			}
			if ((((FeaturepatMatchDataBase)featurepatMatchData).ItemsData == null || ((FeaturepatMatchDataBase)featurepatMatchData).ItemsData.Count == 0) && ((FeaturepatMatchDataBase)featurepatMatchData).otherMask == null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), AppLocalizationService.GetLang("Lang_NoROIRegion"), MessageIcons.Error);
				throw new Exception();
			}
			CREATE_PAT_INPUT_INFO inputInfo = default;
			int maskStartX = 0;
			int maskStartY = 0;
			GetModelParam(featurepatMatchData, ref inputInfo, CurImage, ref maskStartX, ref maskStartY);
			CREATE_PAT_OUTPUT_INFO outputInfo = default;
			uint num2 = CreatePat(ref inputInfo, ref outputInfo);
			if (num2 != 0)
			{
				if (needPopTip)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), AppLocalizationService.GetLang("Lang_CreateModelError") + ":" + ErrorCodeInfo.GetErrorInfo((int)num2), MessageIcons.Error);
				}
				PatCreateManager.GetInstance().FreePatE(outputInfo.pModelData);
				throw new Exception();
			}
			featurepatMatchData.RoughPyramidscale = inputInfo.fParamCoarseGranularity;
			featurepatMatchData.FinePyramidscale = (int)inputInfo.nParamFineGranularity;
			featurepatMatchData.HighThreshold = (int)inputInfo.nParamHighThreshold;
			featurepatMatchData.LowThreshold = (int)inputInfo.nParamLowThreshold;
			((FeaturepatMatchDataBase)featurepatMatchData).PatVersion = PatCreateManager.GetInstance().GetPatAlgorithmVersionE();
			if (OCRTrainDataMgr.Instance.Param == null)
			{
				HikMessageBox.Show("Lang_OCRDefect_TrainModel", "Lang_CurFetLost", MessageIcons.Error, isTranslate: true);
				PatCreateManager.GetInstance().FreePatE(outputInfo.pModelData);
				throw new Exception();
			}
			ModelData = new byte[outputInfo.nModelSize];
			Marshal.Copy(outputInfo.pModelData, ModelData, 0, (int)outputInfo.nModelSize);
			((FeaturepatMatchDataBase)featurepatMatchData).ModelData = ModelData;
			ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> observableCollection = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
			for (int num3 = 0; num3 < outputInfo.stEdgePointList.nPointNum; num3++)
			{
				observableCollection.Add(new MVBPATMATCH_EDGEPOINT_OUT
				{
					x = outputInfo.stEdgePointList.stEdgePointList[num3].fPointX + (float)maskStartX,
					y = outputInfo.stEdgePointList.stEdgePointList[num3].fPointY + (float)maskStartY,
					score_flag = outputInfo.stEdgePointList.stEdgePointList[num3].nScore,
					weight_flag = outputInfo.stEdgePointList.stEdgePointList[num3].nWeight
				});
			}
			((FeaturepatMatchDataBase)featurepatMatchData).PointsData = observableCollection;
			((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData = CurImage;
			RefreshModelImageData(featurepatMatchData);
			PatCreateManager.GetInstance().FreePatE(outputInfo.pModelData);
			NeedBuild = false;
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

	private static uint CreatePat(ref CREATE_PAT_INPUT_INFO inputInfo, ref CREATE_PAT_OUTPUT_INFO outputInfo)
	{
		uint num = 0u;
		if (inputInfo.nParamFineGranularity <= 10)
		{
			inputInfo.nParamFineGranularity = 10u;
		}
		IntPtr zero = IntPtr.Zero;
		int cb = Marshal.SizeOf(typeof(CREATE_PAT_OUTPUT_INFO));
		zero = Marshal.AllocHGlobal(cb);
		Marshal.StructureToPtr(outputInfo, zero, fDeleteOld: true);
		num = (uint)PatCreateManager.GetInstance().CreateDipolePatE(ref inputInfo, zero);
		outputInfo = (CREATE_PAT_OUTPUT_INFO)Marshal.PtrToStructure(zero, typeof(CREATE_PAT_OUTPUT_INFO));
		Marshal.FreeHGlobal(zero);
		return num;
	}

	private static void RefreshModelImageData(FeaturepatMatchDialogData model)
	{
		if (model == null || ((FeaturepatMatchDataBase)model).CurImageData == null)
		{
			return;
		}
		double minX = ((FeaturepatMatchDataBase)model).CurImageData.Width;
		double minY = ((FeaturepatMatchDataBase)model).CurImageData.Height;
		double maxX = 0.0;
		double maxY = 0.0;
		foreach (object itemsDatum in ((FeaturepatMatchDataBase)model).ItemsData)
		{
			if (itemsDatum is HikCoverRoiParameter)
			{
				HikCoverRoiParameter hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
				Rect miniumBoundsRect = GetMiniumBoundsRect(hikCoverRoiParameter);
				if (hikCoverRoiParameter.Sign.HasValue && hikCoverRoiParameter.Sign.Value)
				{
					GetWiderPoint(miniumBoundsRect.X, miniumBoundsRect.Y, ref minX, ref minY, ref maxX, ref maxY);
					GetWiderPoint(miniumBoundsRect.X + miniumBoundsRect.Width, miniumBoundsRect.Y + miniumBoundsRect.Height, ref minX, ref minY, ref maxX, ref maxY);
				}
			}
			else if (itemsDatum is HikCoverCircleRoiParameter)
			{
				HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
				if (hikCoverCircleRoiParameter.Sign.HasValue && hikCoverCircleRoiParameter.Sign.Value)
				{
					GetWiderPoint(hikCoverCircleRoiParameter.CenterX - hikCoverCircleRoiParameter.Radius, hikCoverCircleRoiParameter.CenterY - hikCoverCircleRoiParameter.Radius, ref minX, ref minY, ref maxX, ref maxY);
					GetWiderPoint(hikCoverCircleRoiParameter.CenterX + hikCoverCircleRoiParameter.Radius, hikCoverCircleRoiParameter.CenterY + hikCoverCircleRoiParameter.Radius, ref minX, ref minY, ref maxX, ref maxY);
				}
			}
			else
			{
				if (!(itemsDatum is HikPolygonRoiParameter))
				{
					continue;
				}
				HikPolygonRoiParameter hikPolygonRoiParameter = itemsDatum as HikPolygonRoiParameter;
				if (!hikPolygonRoiParameter.Sign.HasValue || !hikPolygonRoiParameter.Sign.Value)
				{
					continue;
				}
				foreach (PolygonPoint polygonPoint in hikPolygonRoiParameter.PolygonPoints)
				{
					GetWiderPoint((int)polygonPoint.x, (int)polygonPoint.y, ref minX, ref minY, ref maxX, ref maxY);
				}
			}
		}
		minX = Math.Max(minX, 0.0);
		minY = Math.Max(minY, 0.0);
		maxX = Math.Min(maxX, ((FeaturepatMatchDataBase)model).CurImageData.Width);
		maxY = Math.Min(maxY, ((FeaturepatMatchDataBase)model).CurImageData.Height);
		int num = (int)(maxX - minX);
		int num2 = (int)(maxY - minY);
		int num3 = 0;
		int num4 = 0;
		double num5 = (double)num2 / (double)num;
		if (num5 <= 1.0)
		{
			num3 = 98;
			num4 = (int)((double)num3 * num5) + 1;
		}
		else
		{
			num4 = 98;
			num3 = (int)((double)num4 / num5) + 1;
		}
		if (((FeaturepatMatchDataBase)model).ModelImageData == null)
		{
			((FeaturepatMatchDataBase)model).ModelImageData = new ImageData();
		}
		((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer = new byte[num3 * num4 * 3];
		((FeaturepatMatchDataBase)model).ModelImageData.Height = num4;
		((FeaturepatMatchDataBase)model).ModelImageData.Width = num3;
		((FeaturepatMatchDataBase)model).ModelImageData.PixelFormat = PixelFormats.Rgb24;
		for (int i = 0; i < num4; i++)
		{
			for (int j = 0; j < num3; j++)
			{
				int num6 = (int)(minX + (double)j / (double)num3 * (double)num);
				int num7 = (int)(minY + (double)i / (double)num4 * (double)num2);
				((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer[3 * (i * num3 + j)] = ((FeaturepatMatchDataBase)model).CurImageData.ImageBuffer[num7 * ((FeaturepatMatchDataBase)model).CurImageData.Width + num6];
				((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer[3 * (i * num3 + j) + 1] = ((FeaturepatMatchDataBase)model).CurImageData.ImageBuffer[num7 * ((FeaturepatMatchDataBase)model).CurImageData.Width + num6];
				((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer[3 * (i * num3 + j) + 2] = ((FeaturepatMatchDataBase)model).CurImageData.ImageBuffer[num7 * ((FeaturepatMatchDataBase)model).CurImageData.Width + num6];
			}
		}
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in ((FeaturepatMatchDataBase)model).PointsData)
		{
			int num8 = (int)(((double)pointsDatum.x - minX) / (double)num * (double)num3 + 0.5);
			int num9 = (int)(((double)pointsDatum.y - minY) / (double)num2 * (double)num4 + 0.5);
			try
			{
				int index = 3 * (num9 * num3 + num8);
				SetImagePointGreen(((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer, index);
				index = 3 * ((num9 + 1) * num3 + num8);
				SetImagePointGreen(((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer, index);
				if (num8 < num3 - 1)
				{
					index = 3 * (num9 * num3 + num8 + 1);
					SetImagePointGreen(((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer, index);
					index = 3 * ((num9 + 1) * num3 + num8 + 1);
					SetImagePointGreen(((FeaturepatMatchDataBase)model).ModelImageData.ImageBuffer, index);
				}
			}
			catch
			{
			}
		}
	}

	private static Rect GetMiniumBoundsRect(HikCoverRoiParameter cover)
	{
		Rect rect = new Rect
		{
			X = cover.CenterX - cover.Width / 2.0,
			Y = cover.CenterY - cover.Height / 2.0,
			Height = cover.Height,
			Width = cover.Width
		};
		RotateTransform rotateTransform = new RotateTransform(cover.Angle);
		rotateTransform.CenterX = cover.CenterX;
		rotateTransform.CenterY = cover.CenterY;
		return rotateTransform.TransformBounds(rect);
	}

	private static void GetWiderPoint(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
	{
		if (x < minX)
		{
			minX = x;
		}
		if (y < minY)
		{
			minY = y;
		}
		if (x > maxX)
		{
			maxX = x;
		}
		if (y > maxY)
		{
			maxY = y;
		}
	}

	private static void SetImagePointGreen(byte[] imageBuffer, int index)
	{
		if (index + 2 < imageBuffer.Length)
		{
			imageBuffer[index] = 0;
			imageBuffer[index + 1] = byte.MaxValue;
			imageBuffer[index + 2] = 0;
		}
	}

	private static void GetModelParam(FeaturepatMatchDialogData featurepatMatchData, ref CREATE_PAT_INPUT_INFO inputInfo, ImageData curImage, ref int maskStartX, ref int maskStartY)
	{
		inputInfo.pMaskData = GetMaxIntPtrByImage(curImage, featurepatMatchData, ref maskStartX, ref maskStartY);
		inputInfo.pImageData = GetIntPtrByImage(curImage);
		inputInfo.nWidth = (uint)curImage.Width;
		inputInfo.nHeight = (uint)curImage.Height;
		inputInfo.fFixPointX = (float)((FeaturepatMatchDataBase)featurepatMatchData).FixPointX - 0.5f;
		inputInfo.fFixPointY = (float)((FeaturepatMatchDataBase)featurepatMatchData).FixPointY - 0.5f;
		inputInfo.nParamAngleStep = 1u;
		inputInfo.nParamNumLevel = 5u;
		inputInfo.nParamGranularityMode = (uint)featurepatMatchData._PyramidScaleMode;
		inputInfo.fParamCoarseGranularity = featurepatMatchData.RoughPyramidscale * 10f;
		inputInfo.nParamFineGranularity = (uint)(featurepatMatchData.FinePyramidscale * 10);
		inputInfo.nParamGranularity = 0u;
		if (featurepatMatchData.ThresholdEnable)
		{
			inputInfo.nParamHighThreshold = (uint)featurepatMatchData.HighThreshold;
			inputInfo.nParamLowThreshold = (uint)featurepatMatchData.LowThreshold;
		}
		else
		{
			inputInfo.nParamHighThreshold = 0u;
			inputInfo.nParamLowThreshold = 0u;
		}
	}

	private static IntPtr GetMaxIntPtrByImage(ImageData imageData, FeaturepatMatchDialogData featurepatMatchData, ref int oriPosX, ref int oriPosY)
	{
		byte[] array = new byte[imageData.Width * imageData.Height];
		foreach (object itemsDatum in ((FeaturepatMatchDataBase)featurepatMatchData).ItemsData)
		{
			if (itemsDatum is HikCoverRoiParameter)
			{
				double x = (itemsDatum as HikCoverRoiParameter).OriginPoint.X;
				double y = (itemsDatum as HikCoverRoiParameter).OriginPoint.Y;
				double width = (itemsDatum as HikCoverRoiParameter).Width;
				double height = (itemsDatum as HikCoverRoiParameter).Height;
				RotateTransform rotateTransform = new RotateTransform();
				rotateTransform.CenterX = x + width / 2.0;
				rotateTransform.CenterY = y + height / 2.0;
				rotateTransform.Angle = (itemsDatum as HikCoverRoiParameter).Angle;
				System.Windows.Point point = new System.Windows.Point(x, y);
				point = rotateTransform.Transform(point);
				System.Windows.Point point2 = new System.Windows.Point(x + width, y);
				point2 = rotateTransform.Transform(point2);
				System.Windows.Point point3 = new System.Windows.Point(x + width, y + height);
				point3 = rotateTransform.Transform(point3);
				System.Windows.Point point4 = new System.Windows.Point(x, y + height);
				point4 = rotateTransform.Transform(point4);
				PolygonPoint polygonPoint = new PolygonPoint();
				PolygonPoint polygonPoint2 = new PolygonPoint();
				PolygonPoint polygonPoint3 = new PolygonPoint();
				PolygonPoint polygonPoint4 = new PolygonPoint();
				polygonPoint.x = (float)point.X;
				polygonPoint.y = (float)point.Y;
				polygonPoint2.x = (float)point2.X;
				polygonPoint2.y = (float)point2.Y;
				polygonPoint3.x = (float)point3.X;
				polygonPoint3.y = (float)point3.Y;
				polygonPoint4.x = (float)point4.X;
				polygonPoint4.y = (float)point4.Y;
				ObservableCollection<PolygonPoint> observableCollection = new ObservableCollection<PolygonPoint>();
				observableCollection.Add(polygonPoint);
				observableCollection.Add(polygonPoint2);
				observableCollection.Add(polygonPoint3);
				observableCollection.Add(polygonPoint4);
				HikPolygonRoiParameter hikPolygonRoiParameter = new HikPolygonRoiParameter();
				hikPolygonRoiParameter.Sign = (itemsDatum as HikCoverRoiParameter).Sign;
				hikPolygonRoiParameter.PolygonPoints = observableCollection;
				SetMaskDataByPolygen(imageData, hikPolygonRoiParameter, array);
				ImageData imageData2 = new ImageData();
				imageData2.Width = imageData.Width;
				imageData2.Height = imageData.Height;
				imageData2.ImageBuffer = array;
				imageData2.PixelFormat = PixelFormats.Gray8;
				imageData2.SaveGrayImage("graymask.bmp");
			}
			else if (itemsDatum is HikPolygonRoiParameter)
			{
				HikPolygonRoiParameter polyObj = itemsDatum as HikPolygonRoiParameter;
				SetMaskDataByPolygen(imageData, polyObj, array);
			}
			else
			{
				if (!(itemsDatum is HikCoverCircleRoiParameter))
				{
					continue;
				}
				HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
				int width2 = imageData.Width;
				int height2 = imageData.Height;
				double radius = hikCoverCircleRoiParameter._Radius;
				double minRadius = hikCoverCircleRoiParameter._MinRadius;
				byte b = (byte)((!hikCoverCircleRoiParameter.Sign.HasValue) ? 128u : (hikCoverCircleRoiParameter.Sign.Value ? 1u : 0u));
				double num = hikCoverCircleRoiParameter.EndAngle / 180.0 * Math.PI;
				double num2 = hikCoverCircleRoiParameter.StartAngle / 180.0 * Math.PI;
				if (num < 0.0)
				{
					num = 360.0 + num;
				}
				if (num2 < 0.0)
				{
					num2 = 360.0 + num2;
				}
				int num3 = (int)(hikCoverCircleRoiParameter._CenterPoint.Y - radius);
				int num4 = (int)(hikCoverCircleRoiParameter._CenterPoint.X - radius);
				double num5 = minRadius * minRadius;
				double num6 = radius * radius;
				for (int i = Math.Max(0, num3); (double)i < Math.Min((double)num3 + 2.0 * radius, imageData.Height); i++)
				{
					for (int j = Math.Max(0, num4); (double)j < Math.Min((double)num4 + 2.0 * radius, imageData.Width); j++)
					{
						double num7 = (double)i - hikCoverCircleRoiParameter._CenterPoint.Y;
						double num8 = (double)j - hikCoverCircleRoiParameter._CenterPoint.X;
						double num9 = num8 * num8 + num7 * num7;
						if (!(num9 <= num6) || !(num9 >= num5))
						{
							continue;
						}
						double num10 = Math.Atan(Math.Abs(num7) / Math.Abs(num8));
						if (num8 <= 0.0 && num7 <= 0.0)
						{
							num10 = Math.PI + num10;
						}
						if (num8 <= 0.0 && num7 >= 0.0)
						{
							num10 = Math.PI - num10;
						}
						if (num8 >= 0.0 && num7 <= 0.0)
						{
							num10 = Math.PI * 2.0 - num10;
						}
						if ((num10 >= num2 && num10 <= num) || (num2 >= num && (num10 >= num2 || num10 <= num)))
						{
							int num11 = i * width2 + j;
							if (num11 < array.Length && num11 >= 0)
							{
								array[num11] = b;
							}
						}
					}
				}
			}
		}
		if (((FeaturepatMatchDataBase)featurepatMatchData).otherMask != null && ((FeaturepatMatchDataBase)featurepatMatchData).otherMask.Length == array.Length)
		{
			for (int k = 0; k < ((FeaturepatMatchDataBase)featurepatMatchData).otherMask.Length; k++)
			{
				if (((FeaturepatMatchDataBase)featurepatMatchData).otherMask[k] == 1)
				{
					array[k] = 0;
				}
			}
		}
		GetMaskStartPos(imageData, ref oriPosX, ref oriPosY, array);
		GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
		IntPtr result = gCHandle.AddrOfPinnedObject();
		if (gCHandle.IsAllocated)
		{
			gCHandle.Free();
		}
		return result;
	}

	private static IntPtr GetIntPtrByImage(ImageData imageData)
	{
		GCHandle gCHandle = GCHandle.Alloc(imageData.ImageBuffer, GCHandleType.Pinned);
		IntPtr result = gCHandle.AddrOfPinnedObject();
		if (gCHandle.IsAllocated)
		{
			gCHandle.Free();
		}
		return result;
	}

	private static void SetMaskDataByPolygen(ImageData imageData, HikPolygonRoiParameter polyObj, byte[] maskData)
	{
		int width = imageData.Width;
		int height = imageData.Height;
		float num = Math.Max(polyObj.PolygonPoints.Min((PolygonPoint r) => r.x), 0f);
		float num2 = Math.Min(polyObj.PolygonPoints.Max((PolygonPoint r) => r.x), width);
		float num3 = Math.Max(polyObj.PolygonPoints.Min((PolygonPoint r) => r.y), 0f);
		float num4 = Math.Min(polyObj.PolygonPoints.Max((PolygonPoint r) => r.y), height);
		byte b = (byte)((!polyObj.Sign.HasValue) ? 128u : (polyObj.Sign.Value ? 1u : 0u));
		for (int num5 = (int)num3; (float)num5 < num4; num5++)
		{
			int num6 = 0;
			int index = polyObj.PolygonPoints.Count - 1;
			float[] array = new float[polyObj.PolygonPoints.Count];
			int num7;
			for (num7 = 0; num7 < polyObj.PolygonPoints.Count; num7++)
			{
				if ((polyObj.PolygonPoints[num7].y < (float)num5 && polyObj.PolygonPoints[index].y >= (float)num5) || (polyObj.PolygonPoints[index].y < (float)num5 && polyObj.PolygonPoints[num7].y >= (float)num5))
				{
					array[num6++] = polyObj.PolygonPoints[num7].x + ((float)num5 - polyObj.PolygonPoints[num7].y) / (polyObj.PolygonPoints[index].y - polyObj.PolygonPoints[num7].y) * (polyObj.PolygonPoints[index].x - polyObj.PolygonPoints[num7].x);
				}
				index = num7;
			}
			float num8 = 0f;
			num7 = 0;
			while (num7 < num6 - 1)
			{
				if (array[num7] > array[num7 + 1])
				{
					num8 = array[num7];
					array[num7] = array[num7 + 1];
					array[num7 + 1] = num8;
					if (num7 > 0)
					{
						num7--;
					}
				}
				else
				{
					num7++;
				}
			}
			for (num7 = 0; num7 < num6 && !(array[num7] >= num2); num7 += 2)
			{
				if (!(array[num7 + 1] > num))
				{
					continue;
				}
				if (array[num7] < num)
				{
					array[num7] = num;
				}
				if (array[num7 + 1] > num2)
				{
					array[num7 + 1] = num2;
				}
				for (index = (int)array[num7]; (float)index < array[num7 + 1]; index++)
				{
					int num9 = num5 * width + index;
					if (num9 < maskData.Length && num9 >= 0)
					{
						maskData[num9] = b;
					}
				}
			}
		}
	}

	private static void GetMaskStartPos(ImageData imageData, ref int oriPosX, ref int oriPosY, byte[] maskData)
	{
		if (maskData == null || imageData == null)
		{
			return;
		}
		oriPosX = imageData.Width;
		oriPosY = imageData.Height;
		bool flag = false;
		for (int i = 0; i < imageData.Width; i++)
		{
			for (int j = 0; j < imageData.Height; j++)
			{
				if (maskData[j * imageData.Width + i] == 1)
				{
					oriPosX = i;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		flag = false;
		for (int k = 0; k < imageData.Height; k++)
		{
			for (int l = 0; l < imageData.Width; l++)
			{
				if (maskData[k * imageData.Width + l] == 1)
				{
					oriPosY = k;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
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
			string text = Path.GetFullPath("./Temporary") + "\\FeaturepatMatch_" + (historyIndex + 1) + ".xml";
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
			foreach (RoiParameter itemsDatum in ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData)
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
					HikPolygonRoiParameter hikPolygonRoiParameter = itemsDatum as HikPolygonRoiParameter;
					double num = hikPolygonRoiParameter.PolygonPoints.Min((PolygonPoint r) => r.x);
					double num2 = hikPolygonRoiParameter.PolygonPoints.Min((PolygonPoint r) => r.y);
					double num3 = hikPolygonRoiParameter.PolygonPoints.Max((PolygonPoint r) => r.x);
					double num4 = hikPolygonRoiParameter.PolygonPoints.Max((PolygonPoint r) => r.y);
					value.X = (num + num3) / 2.0;
					value.Y = (num2 + num4) / 2.0;
				}
				if (itemsDatum is HikCoverCircleRoiParameter)
				{
					HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
					value.X = hikCoverCircleRoiParameter.CenterPoint.X;
					value.Y = hikCoverCircleRoiParameter.CenterPoint.Y;
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
			int num3 = num2 / CurImage.Width;
			int num4 = num2 % CurImage.Width;
			int num5 = num2 / CurImage.Width * CurImage.Width;
			int num6 = num5 + CurImage.Width - 1;
			if (num3 - 1 >= 0)
			{
				int k = (num3 - 1) * CurImage.Width + num4;
				SetNewBordPoint(otherMask, masknum, k, color, ref BorderArray, ref num);
			}
			if (num3 - 1 >= 0 && num4 + 1 < CurImage.Width)
			{
				int k2 = num4 + 1 + (num3 - 1) * CurImage.Width;
				SetNewBordPoint(otherMask, masknum, k2, color, ref BorderArray, ref num);
			}
			if (num4 + 1 < CurImage.Width)
			{
				int k3 = num5 + num4 + 1;
				SetNewBordPoint(otherMask, masknum, k3, color, ref BorderArray, ref num);
			}
			if (num4 + 1 < CurImage.Width && num3 + 1 < CurImage.Height)
			{
				int k4 = num4 + 1 + (num3 + 1) * CurImage.Width;
				SetNewBordPoint(otherMask, masknum, k4, color, ref BorderArray, ref num);
			}
			if (num3 + 1 < CurImage.Height)
			{
				int k5 = (num3 + 1) * CurImage.Width + num4;
				SetNewBordPoint(otherMask, masknum, k5, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0 && num3 + 1 < CurImage.Height)
			{
				int k6 = (num3 + 1) * CurImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k6, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0)
			{
				int k7 = num3 * CurImage.Width + num4 - 1;
				SetNewBordPoint(otherMask, masknum, k7, color, ref BorderArray, ref num);
			}
			if (num4 - 1 >= 0 && num3 - 1 >= 0)
			{
				int k8 = (num3 - 1) * CurImage.Width + num4 - 1;
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

	private void ShowImage()
	{
		if (CurImage != null)
		{
			try
			{
				imageView.ImageDataSource = CurImage;
			}
			catch
			{
			}
		}
	}

	private bool ChangeImageWarning()
	{
		if (!OCRTrainDataMgr.Instance.Owner.btnNextStep.IsEnabled)
		{
			return true;
		}
		DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ChangeImageTip", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == DialogResult.Yes || dialogResult == DialogResult.SDKModel)
		{
			OCRTrainDataMgr.Instance.OnClearAllModelEvent();
			if (OCRTrainDataMgr.Instance.Owner != null)
			{
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbTrainRegion).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbTrainTarget).IsEnabled = false;
				((UIElement)(object)OCRTrainDataMgr.Instance.Owner.gbStatisticsTrain).IsEnabled = false;
				OCRTrainDataMgr.Instance.Owner.btnNextStep.IsEnabled = false;
			}
			OCRTrainDataMgr.Instance.Owner?.pageTrainRegion.RectRois.Clear();
			OCRTrainDataMgr.Instance.Owner.pageTrainRegion.RectEnable = false;
			OCRTrainDataMgr.Instance.Owner.pageTrainRegion.SelectedBox = null;
			OCRTrainDataMgr.Instance.Owner?.ClearDataByRoi();
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
		imageView.Dispose();
		roiImageView.Dispose();
	}

	private bool SetRoiData()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return false;
		}
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
			HikMessageBox.Show(text2, MessageIcons.Error);
			return false;
		}
		return true;
	}

	private byte[] GetRoiBuffer()
	{
		HikCoverRoiParameter hikCoverRoiParameter = null;
		if (RectRois.Count > 0)
		{
			hikCoverRoiParameter = RectRois[0] as HikCoverRoiParameter;
		}
		if (imageView.ImageDataSource != null)
		{
			return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(hikCoverRoiParameter, (float)imageView.ImageDataSource.Width, (float)imageView.ImageDataSource.Height);
		}
		return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(hikCoverRoiParameter, 0f, 0f);
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected Obj, but got Unknown
		IsHasCurImage = OCRTrainDataMgr.Instance.CurImageData != null;
		RectRois = new ObservableCollection<object>();
		featurepatMatchDialogData = new FeaturepatMatchDialogData();
		featurepatMatchDialogData = moduleTrainData.FeatureMatchData.CopyModel(false);
		CollectionChangedEventManager.AddHandler(((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData, ItemsData_CollectionChanged);
		DataContext = featurepatMatchDialogData;
		imageView.DataContext = featurepatMatchDialogData;
		((FeaturepatMatchDataBase)featurepatMatchDialogData).version = FeaturepatMatchDataBase.curVersion;
		((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData = OCRTrainDataMgr.Instance.GetRoughImage();
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData != null)
		{
			CurImage = ((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData;
			OCRTrainDataMgr.Instance.SynDataProcess(SynDataOccasion.SetOtherImage, ((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData);
		}
		else
		{
			CurImage = OCRTrainDataMgr.Instance.CurImageData;
			if (CurImage != null)
			{
				OCRTrainDataMgr.Instance.SynDataProcess(SynDataOccasion.SetCurrentImage);
			}
		}
		roiImageView.ROITypeUI = ROITypeUIEnum.Transparent;
		roiImageView.ImageDataSource = imageView.ImageDataSource;
		HikCoverRoiParameter roughRoiParam = moduleTrainData.RoughRoiParam;
		if (roughRoiParam != null)
		{
			RectRois.Add(new HikCoverRoiParameter
			{
				Width = roughRoiParam.Width,
				Height = roughRoiParam.Height,
				Angle = roughRoiParam.Angle,
				CenterX = roughRoiParam.CenterX,
				CenterY = roughRoiParam.CenterY,
				ForceShowSelected = true
			});
			RectEnable = true;
		}
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		if (RectRois.Count > 0)
		{
			moduleTrainData.RoughRoiParam = RectRois[0] as HikCoverRoiParameter;
		}
		else
		{
			moduleTrainData.RoughRoiParam = null;
		}
		moduleTrainData.FeatureMatchData = featurepatMatchDialogData;
		Dispose();
		DataContext = null;
		imageView.DataContext = null;
		roiImageView.DataContext = null;
		ModelData = null;
		((ItemsControl)(object)paramSearchUI).ItemsSource = null;
		CurImage = null;
	}
}
