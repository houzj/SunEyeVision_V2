using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using HikExternalCall.ImageProcess;
using HikPcUI.ImageView;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;

namespace IMVSMarkInspModuControl;

public partial class OCRStatisticsTrainPage : System.Windows.Controls.UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	protected ObservableCollection<ImageEntity> _ImageList = new ObservableCollection<ImageEntity>();

	private Image _maskLayer = new Image();

	private Rubber2 _rubber;

	private WriteableBitmap _markInspMask;

	private int _FreshListCount = 0;

	private int _SelectedItemsCount;

	private int _SelectListViewIndex = 0;

	private int _CurIndex = 0;

	private double _CurHorizontalOffset = 0.0;

	private int _PanRectWidth = 10;

	private bool _IsDrawRubber = true;

	private bool _IsInit = false;

	private int _imageCount = 0;

	private ObservableCollection<DispatcherOperation> _dispatcherOperationList = new ObservableCollection<DispatcherOperation>();

	private int _modelImageCount = 0;

	private string _imagePath = null;

	public static readonly DependencyProperty IsCanTrainProperty = DependencyProperty.Register("IsCanTrain", typeof(bool), typeof(OCRStatisticsTrainPage), new PropertyMetadata(false));

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(OCRStatisticsTrainPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BoxImageProperty = DependencyProperty.Register("BoxImage", typeof(ImageSource), typeof(OCRStatisticsTrainPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty MaskImageProperty = DependencyProperty.Register("MaskImage", typeof(ImageSource), typeof(OCRStatisticsTrainPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty TrainCountProperty = DependencyProperty.Register("TrainCount", typeof(int), typeof(OCRStatisticsTrainPage), new PropertyMetadata(0));

	private static List<string> moreOptions = new List<string> { "当前图像", "缓存图像", "导入本地图像", "导入文件夹" };

	private DelegateCommand freshOptionsCommand = null;

	private DelegateCommand<object> clickCommand = null;

	internal ResultOverviewPopupButton setBtn;

	public bool IsCanTrain
	{
		get
		{
			return (bool)GetValue(IsCanTrainProperty);
		}
		set
		{
			SetValue(IsCanTrainProperty, value);
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

	public ImageSource BoxImage
	{
		get
		{
			return (ImageSource)GetValue(BoxImageProperty);
		}
		set
		{
			SetValue(BoxImageProperty, value);
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

	public int TrainCount
	{
		get
		{
			return (int)GetValue(TrainCountProperty);
		}
		set
		{
			SetValue(TrainCountProperty, value);
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
				OnPropertyChanged("SelectedItemsCount");
			}
		}
	}

	public int SelectListViewIndex
	{
		get
		{
			return _SelectListViewIndex;
		}
		set
		{
			if (_SelectListViewIndex != value)
			{
				_SelectListViewIndex = value;
				OnPropertyChanged("SelectListViewIndex");
			}
		}
	}

	public ObservableCollection<ImageEntity> ImageList => _ImageList;

	public int ShowAllTextCount => _ImageList.Count;

	public List<string> MoreOptions => moreOptions;

	public ICommand FreshOptionsCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = freshOptionsCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)FreshOptionsCommandExecute);
				DelegateCommand val3 = val2;
				freshOptionsCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand ClickCommand => (ICommand)(clickCommand ?? (clickCommand = new DelegateCommand<object>((Action<object>)ClickCommandExecute)));

	public event PropertyChangedEventHandler PropertyChanged;

	public OCRStatisticsTrainPage()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!_IsInit)
			{
				_maskLayer.MouseLeftButtonDown += tempImage_PreviewMouseLeftButtonDown;
				_maskLayer.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
				_maskLayer.MouseEnter += tempImage_MouseEnter;
				_maskLayer.MouseMove += tempImage_MouseMove;
				_maskLayer.MouseWheel += tempImage_MouseWheel;
				_maskLayer.IsHitTestVisible = false;
				_maskLayer.Focusable = false;
				_maskLayer.IsEnabled = false;
				_maskLayer.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
				System.Windows.Controls.Panel.SetZIndex(_maskLayer, 100);
				imageView.AddImageLayer(_maskLayer);
				_maskLayer.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("MaskImage")
				{
					Source = this
				});
				_maskLayer.SetBinding(UIElement.IsEnabledProperty, new System.Windows.Data.Binding("IsChecked")
				{
					Source = mask
				});
				HikImageView hikImageView = imageView;
				hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
				OCRTrainDataMgr.Instance.DeleteImageEvent += DeleteImageEvent;
				MaskImage = _markInspMask;
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

	private void tempImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void tempImage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void Mask_OnClick(object sender, RoutedEventArgs e)
	{
		if (_IsDrawRubber)
		{
			if (imageView.Source != null)
			{
				_rubber = new Rubber2();
				Rubber2 rubber = _rubber;
				rubber.OnRubberFinish = (Rubber2.RubberFinish)Delegate.Combine(rubber.OnRubberFinish, new Rubber2.RubberFinish(rubber_OnRubberFinish));
				Rubber2 rubber2 = _rubber;
				rubber2.OnWipe = (Rubber2.Wipe)Delegate.Combine(rubber2.OnWipe, new Rubber2.Wipe(rubber_OnWipe));
				_rubber.DataContext = this;
				_rubber.SetBinding(Rubber2.DWidthProperty, "PanRectWidth");
				_rubber.SetBinding(Rubber2.DHeightProperty, "PanRectWidth");
				_rubber.RegisterRoi(imageView);
				_IsDrawRubber = false;
			}
		}
		else if (_rubber != null)
		{
			_rubber.UnRegisterRoi(imageView);
			BindingOperations.ClearAllBindings(_rubber);
			_rubber.DataContext = null;
			if (_rubber.OnWipe != null)
			{
				Rubber2 rubber3 = _rubber;
				rubber3.OnWipe = (Rubber2.Wipe)Delegate.Remove(rubber3.OnWipe, new Rubber2.Wipe(rubber_OnWipe));
			}
			if (_rubber.OnRubberFinish != null)
			{
				Rubber2 rubber4 = _rubber;
				rubber4.OnRubberFinish = (Rubber2.RubberFinish)Delegate.Remove(rubber4.OnRubberFinish, new Rubber2.RubberFinish(rubber_OnRubberFinish));
			}
			_rubber = null;
			mask.IsChecked = false;
			_IsDrawRubber = true;
		}
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
		AddMaskUndo();
	}

	private void ClearMask_OnClick(object sender, RoutedEventArgs e)
	{
		WriteMask(new int[0], clearall: true);
		AddMaskUndo();
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

	private void Abort_OnClick(object sender, RoutedEventArgs e)
	{
		ClearStatData(bIsNeedExact: true);
	}

	private void TrainCurImage_OnClick(object sender, RoutedEventArgs e)
	{
		if (lst.SelectedIndex == 0)
		{
			listScroll.ScrollToLeftEnd();
		}
		IntPtr selectedImagePtr = GetSelectedImagePtr(out var len);
		if (selectedImagePtr == IntPtr.Zero)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetCurImageDataError"), MessageIcons.Warning);
			return;
		}
		try
		{
			byte[] array = new byte[len];
			Marshal.Copy(selectedImagePtr, array, 0, len);
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetTrainImage"] = array;
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("Lang_SetCurImageDataError") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			HikMessageBox.Show(text2, MessageIcons.Error);
			Marshal.FreeHGlobal(selectedImagePtr);
			return;
		}
		Marshal.FreeHGlobal(selectedImagePtr);
		int num = Train("TrainCurImage");
		if ((long)num != 0)
		{
			string text3 = AppLocalizationService.GetLang("Lang_TrainImageError") + "\r\n(";
			if (num == -1)
			{
				text3 += AppLocalizationService.GetLang("Lang_ParamErr");
			}
			else
			{
				text3 += AppLocalizationService.GetLang("errCode:");
				text3 += $"{num:X}, ";
				text3 += ErrorCodeInfo.GetErrorInfo(num);
			}
			text3 += ")";
			HikMessageBox.Show(text3, MessageIcons.Error);
			return;
		}
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		val.IsCalibrated = true;
		val.IsOKCalibrated = true;
		if (lst.SelectedIndex + 1 < lst.Items.Count)
		{
			lst.SelectedIndex++;
			if (lst.SelectedIndex > 1)
			{
				ScrollImageList();
			}
		}
		else
		{
			SelectListViewIndex++;
			IsCanTrain = false;
		}
		_CurIndex = lst.SelectedIndex;
		NextImageProcess();
		if (!_IsDrawRubber && _rubber != null)
		{
			_rubber.UnRegisterRoi(imageView);
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
			mask.IsChecked = false;
			_IsDrawRubber = true;
		}
	}

	private void IgnoreCurImage_OnClick(object sender, RoutedEventArgs e)
	{
		if (lst.SelectedIndex == 0)
		{
			listScroll.ScrollToLeftEnd();
		}
		Train("IgnoreCurImage", bIgnoreTrain: true);
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		val.IsCalibrated = true;
		val.IsOKCalibrated = false;
		if (lst.SelectedIndex + 1 < lst.Items.Count)
		{
			lst.SelectedIndex++;
			if (lst.SelectedIndex > 1)
			{
				ScrollImageList();
			}
		}
		else
		{
			SelectListViewIndex++;
			IsCanTrain = false;
		}
		_CurIndex = lst.SelectedIndex;
		NextImageProcess();
	}

	public void NextImageProcess()
	{
		int len = 0;
		IntPtr selectedImagePtr = GetSelectedImagePtr(out len);
		if (selectedImagePtr == IntPtr.Zero)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetCurImageDataError"), MessageIcons.Warning);
			return;
		}
		try
		{
			byte[] array = new byte[len];
			Marshal.Copy(selectedImagePtr, array, 0, len);
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetTrainImage"] = array;
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("Lang_SetCurImageDataError") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			HikMessageBox.Show(text2, MessageIcons.Error);
			Marshal.FreeHGlobal(selectedImagePtr);
			return;
		}
		Marshal.FreeHGlobal(selectedImagePtr);
		try
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)["ExactMatchTrain"] = "";
			NextImageDisplay();
		}
		catch (VmException ex2)
		{
			string text3 = AppLocalizationService.GetLang("Lang_NextImageExactError") + "\r\n(";
			text3 += AppLocalizationService.GetLang("errInfo:");
			text3 += ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			text3 += ")";
			HikMessageBox.Show(text3, MessageIcons.Error);
		}
	}

	private void ImageBoxes_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ImageBoxes.SelectedIndex < 0)
		{
			BoxImage = null;
		}
		else
		{
			BoxImage = (ImageBoxes.SelectedItem as ModelImage)?.Source;
		}
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

	private void DelAllImage_Click(object sender, RoutedEventArgs e)
	{
		while (_ImageList.Count > 0)
		{
			DeleteImage(null, bIsTrain: false);
		}
		if (OCRTrainDataMgr.Instance.Param != null)
		{
			string savePath = GetSavePath();
			if (Directory.Exists(savePath))
			{
				Directory.Delete(savePath, recursive: true);
			}
			imageView.ClearShapesE();
			CurImage = null;
		}
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = listScroll;
		if (scrollViewer != null)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableWidth, scrollViewer.HorizontalOffset + num) : Math.Max(0.0, scrollViewer.HorizontalOffset - num));
			if (num2 != scrollViewer.HorizontalOffset)
			{
				scrollViewer.ScrollToHorizontalOffset(num2);
				scrollViewer.Focus();
			}
			e.Handled = true;
		}
	}

	private void listScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		FreshImageList();
		if (lst == null)
		{
			return;
		}
		if (lst.SelectedItems != null)
		{
			SelectedItemsCount = lst.SelectedItems.Count;
		}
		if (lst.SelectedIndex < 0)
		{
			SelectListViewIndex = 0;
			return;
		}
		SelectListViewIndex = lst.SelectedIndex;
		if (lst.SelectedIndex + 1 == lst.Items.Count && ImageList[lst.SelectedIndex].IsCalibrated)
		{
			SelectListViewIndex = lst.SelectedIndex + 1;
		}
	}

	private void lst_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (lst == null)
		{
			return;
		}
		if (lst.SelectedItems != null)
		{
			SelectedItemsCount = lst.SelectedItems.Count;
		}
		if (lst.SelectedIndex < 0)
		{
			SelectListViewIndex = 0;
			return;
		}
		SelectListViewIndex = lst.SelectedIndex;
		if (lst.SelectedIndex + 1 == lst.Items.Count && ImageList[lst.SelectedIndex].IsCalibrated)
		{
			SelectListViewIndex = lst.SelectedIndex + 1;
		}
	}

	private void grid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		lst.SelectedIndex = _CurIndex;
	}

	private void grid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		lst.SelectedIndex = _CurIndex;
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

	private void DeleteImageEvent(object obj)
	{
		DeleteImage(obj);
	}

	private void FreshOptionsCommandExecute()
	{
		moreOptions = new List<string>();
		moreOptions.Add(AppLocalizationService.GetLang("Lang_CurImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_CacheImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportLocalImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportFolder"));
		OnPropertyChanged("MoreOptions");
	}

	private void ClickCommandExecute(object obj)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (obj is string text)
			{
				bool flag = false;
				CreateFolder(GetSavePath());
				if (Enumerable.Count(_ImageList) >= 500)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 500, MessageButtons.Normal, MessageIcons.Warning);
				}
				else
				{
					OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
					OCRTrainDataMgr.Instance.IsUpImageLimit = false;
					bool flag2 = false;
					if (text == AppLocalizationService.GetLang("Lang_CurImage"))
					{
						flag = LoadCurrentImage();
					}
					else if (text == AppLocalizationService.GetLang("Lang_CacheImage"))
					{
						flag = LoadCacheImage();
					}
					else if (text == AppLocalizationService.GetLang("Lang_ImportLocalImage"))
					{
						flag = LoadLocalImage();
						flag2 = true;
					}
					else if (text == AppLocalizationService.GetLang("Lang_ImportFolder"))
					{
						flag = LoadFolder();
						flag2 = true;
					}
					if (flag)
					{
						if (lst.SelectedIndex < 0)
						{
							lst.SelectedIndex = 0;
							_CurIndex = 0;
						}
						else if (!IsCanTrain)
						{
							_CurIndex = lst.SelectedIndex + 1;
							lst.SelectedIndex += 1;
						}
						else
						{
							_CurIndex = lst.SelectedIndex;
						}
					}
					if (flag2 & flag)
					{
						IsCanTrain = true;
					}
					else
					{
						IsCanTrain = IsTrain();
					}
					if (flag2)
					{
						Task task = new Task(() =>
						{
							int num = 0;
							while (!IsAllOperationComplete())
							{
								Thread.Sleep(10);
								num++;
								if (num == 30000)
								{
									break;
								}
							}
							OCRTrainDataMgr.Instance.OnCloseProgressWindowEvent();
							_dispatcherOperationList.Clear();
							if (OCRTrainDataMgr.Instance.IsUpImageLimit)
							{
								HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 500, MessageButtons.Normal, MessageIcons.Warning);
							}
							Dispatcher.BeginInvoke((Action)(() =>
							{
								if (IsCanTrain)
								{
									NextImageProcess();
								}
							}));
						});
						task.Start();
					}
					else
					{
						Dispatcher.BeginInvoke((Action)(() =>
						{
							if (IsCanTrain)
							{
								NextImageProcess();
							}
						}));
					}
					OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
				}
			}
		}));
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		_imagePath = moduleTrainData.ImagePath;
		LoadImagesAsync(moduleTrainData);
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		moduleTrainData.CurSelectIndex = _CurIndex;
		moduleTrainData.CurHorizontalOffset = _CurHorizontalOffset;
		moduleTrainData.ModelImageCount = _modelImageCount;
		if (moduleTrainData.TrainFlagList == null)
		{
			moduleTrainData.TrainFlagList = new List<bool>();
		}
		moduleTrainData.TrainFlagList.Clear();
		for (int i = 0; i < _CurIndex; i++)
		{
			moduleTrainData.TrainFlagList.Add(ImageList[i].IsOKCalibrated);
		}
		if (_CurIndex >= 0 && _CurIndex < ImageList.Count && ImageList[_CurIndex].IsCalibrated)
		{
			moduleTrainData.TrainFlagList.Add(ImageList[_CurIndex].IsOKCalibrated);
		}
		moduleTrainData.ImagePath = _imagePath;
		_maskLayer.MouseLeftButtonDown -= tempImage_PreviewMouseLeftButtonDown;
		_maskLayer.MouseLeftButtonUp -= tempImage_PreviewMouseLeftButtonUp;
		_maskLayer.MouseEnter -= tempImage_MouseEnter;
		_maskLayer.MouseMove -= tempImage_MouseMove;
		_maskLayer.MouseWheel -= tempImage_MouseWheel;
		OCRTrainDataMgr.Instance.DeleteImageEvent -= DeleteImageEvent;
		if (imageView != null)
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Remove(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
			imageView.Dispose();
			imageView.DataContext = null;
		}
		if (ImageBoxes.ItemsSource is ObservableCollection<ModelImage>)
		{
			foreach (ModelImage item in ImageBoxes.ItemsSource as ObservableCollection<ModelImage>)
			{
				if (item != null)
				{
					item.Source = null;
				}
			}
		}
		ImageBoxes.ItemsSource = null;
		DataContext = null;
		GC.Collect();
	}

	public void ClearStatData(bool bIsNeedExact = false)
	{
		if ((long)Train("DeleteMarkInspModel") == 0)
		{
			OCRTrainDataMgr.Instance.OnEmptyModelEvent();
		}
		ResetImageList(bIsNeedExact);
	}

	private bool IsAllOperationComplete()
	{
		foreach (DispatcherOperation dispatcherOperation in _dispatcherOperationList)
		{
			if (dispatcherOperation.Status != DispatcherOperationStatus.Completed)
			{
				return false;
			}
		}
		return true;
	}

	private MVBI_BOX_F[] GetBox(bool charExtract = true)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return null;
		}
		if (charExtract)
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)["CharExtract"] = "";
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetMarkBoxArr"];
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

	private void GetTrainCurImage()
	{
		GetCurImage("GetTrainCurImage");
	}

	private void GetCurImage(string key)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)[key];
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

	private unsafe void AddMaskUndo()
	{
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
		fixed (byte* ptr2 = new byte[len])
		{
			for (int i = 0; i < len; i++)
			{
				ptr2[i] = ((byte*)ptr)[i];
			}
		}
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

	private unsafe IntPtr GetSelectedImagePtr(out int len)
	{
		len = 0;
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null)
		{
			BitmapImage bitmapImage = ImageData.CreateThumbnail(val.ImagePath);
			if (bitmapImage == null)
			{
				DeleteImage(null);
				return IntPtr.Zero;
			}
			if (bitmapImage.PixelWidth > 0)
			{
				val.ImageWidth = bitmapImage.PixelWidth;
				val.ImageHeight = bitmapImage.PixelHeight;
				WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapImage);
				if (writeableBitmap == null)
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
						*ptr3 = ptr4[j * num2];
						ptr3++;
					}
				}
				return result;
			}
		}
		return IntPtr.Zero;
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
		WriteableBitmap markInspMask = _markInspMask;
		if (markInspMask == null || markInspMask.Width != (double)writeableBitmap.PixelWidth || markInspMask.Height != (double)writeableBitmap.PixelHeight)
		{
			_markInspMask = new WriteableBitmap(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight, 96.0, 96.0, PixelFormats.Bgra32, null);
			clearall = false;
		}
		MaskImage = _markInspMask;
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
			uint num11 = 0u;
			for (int l = num2; l < num4 + 1; l++)
			{
				for (int m = num; m < num3 + 1; m++)
				{
					num11 = 1291786778u;
					uint* ptr5 = ptr2 + l * num8 + m;
					*ptr5 = num11;
				}
			}
		}
		writeableBitmap2.AddDirtyRect(dirtyRect);
		writeableBitmap2.Unlock();
	}

	private bool GetFeatureModel()
	{
		DrawPoints();
		return true;
	}

	private unsafe void DrawPoints()
	{
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetFeatureMatch"];
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
		}
		catch
		{
		}
	}

	private void ScrollImageList()
	{
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null)
		{
			FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(val);
			if (!IsFullVisibleInParent(child, listScroll))
			{
				_CurHorizontalOffset += GetHorizontalOffset(child, listScroll);
				listScroll.ScrollToHorizontalOffset(_CurHorizontalOffset);
			}
		}
	}

	public void RefreshTrainDisplay()
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		imageView.ClearShapesE();
		ImageBoxes.ItemsSource = null;
		GetTrainCurImage();
		if (CurImage == null)
		{
			return;
		}
		MVBI_BOX_F[] box = GetBox(charExtract: false);
		if (box == null)
		{
			return;
		}
		int[] array = new int[box.Length];
		try
		{
			byte[] array2 = null;
			array2 = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetMarkStatus"];
			if (array2 != null && array2.Length != 0)
			{
				IntPtr source = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
				Marshal.Copy(source, array, 0, array.Length);
			}
		}
		catch
		{
		}
		for (int i = 0; i < box.Length; i++)
		{
			MVBI_BOX_F val = box[i];
			val.height++;
			val.width++;
			imageView.CreatRectangleE(val.center_x, val.center_y, val.width, val.height, val.angle, (array[i] == 1) ? "#00FF00" : "#FF0000", null, 1.0);
		}
	}

	private void NextImageDisplay()
	{
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		imageView.ClearShapesE();
		GetTrainCurImage();
		if (CurImage == null)
		{
			return;
		}
		MVBI_BOX_F[] box = GetBox(charExtract: false);
		if (box != null)
		{
			int[] array = new int[box.Length];
			try
			{
				byte[] array2 = null;
				array2 = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetMarkStatus"];
				if (array2 != null && array2.Length != 0)
				{
					IntPtr source = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
					Marshal.Copy(source, array, 0, array.Length);
				}
			}
			catch
			{
			}
			for (int i = 0; i < box.Length; i++)
			{
				MVBI_BOX_F val = box[i];
				imageView.CreatRectangleE(val.center_x, val.center_y, val.width, val.height, val.angle, (array[i] == 1) ? "#00FF00" : "#FF0000", null, 1.0);
			}
		}
		imageView.ExternPoint_Buffer = new List<byte[]>(0);
		imageView.CreatPointK();
		GetFeatureModel();
	}

	public void ModelProcess(bool bDelete = false)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return;
		}
		if (bDelete)
		{
			imageView.ClearShapesE();
		}
		ImageBoxes.ItemsSource = null;
		try
		{
			string empty = string.Empty;
			empty = ((IUserStringData)OCRTrainDataMgr.Instance.Param)["TrainNum"];
			TrainCount = int.Parse(empty);
		}
		catch
		{
			return;
		}
		if (TrainCount > 0)
		{
			if (bDelete && OCRTrainDataMgr.Instance._boxes != null && OCRTrainDataMgr.Instance._boxes.Length > _modelImageCount)
			{
				ImageBoxes.ItemsSource = GetModelImages();
			}
			else
			{
				GetBoxImages();
			}
		}
	}

	private ObservableCollection<ModelImage> GetModelImages()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return null;
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetModelImage"];
			if (array != null && array.Length != 0)
			{
				int dataLen = array.Length;
				IntPtr dataPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				return ModelImage.GetModuleImages(dataPtr, dataLen);
			}
		}
		catch
		{
		}
		return null;
	}

	private void GetBoxImages()
	{
		ObservableCollection<ModelImage> observableCollection = new ObservableCollection<ModelImage>();
		if (OCRTrainDataMgr.Instance._boxes != null)
		{
			for (int i = 0; i < OCRTrainDataMgr.Instance._boxes.Length; i++)
			{
				observableCollection.Add(new ModelImage
				{
					Name = i.ToString(),
					Source = GetBoxImage(i)
				});
			}
			_modelImageCount = OCRTrainDataMgr.Instance._boxes.Length;
		}
		ImageBoxes.ItemsSource = observableCollection;
	}

	private WriteableBitmap GetBoxImage(int index)
	{
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)[$"GetBoxImage_{index}"];
			if (array != null && array.Length != 0)
			{
				int readLen = 0;
				IntPtr dataPtr = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
				return ModelImage.GetMono8Image(dataPtr, out readLen);
			}
		}
		catch
		{
		}
		return null;
	}

	private int Train(string cmd, bool bIgnoreTrain = false)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return -1;
		}
		if (TrainCount == 0 && !bIgnoreTrain)
		{
			IntPtr intPtr = MaskThreshold(out var len);
			if (intPtr == IntPtr.Zero)
			{
				return -1;
			}
			try
			{
				byte[] array = new byte[len];
				Marshal.Copy(intPtr, array, 0, len);
				((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetMarkInspMask"] = array;
			}
			catch (Exception ex)
			{
				Marshal.FreeHGlobal(intPtr);
				int result = 0;
				return int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? result : (-1);
			}
			Marshal.FreeHGlobal(intPtr);
		}
		try
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)[cmd] = "";
		}
		catch (VmException ex2)
		{
			if (cmd == "TrainCurImage")
			{
				RefreshTrainDisplay();
			}
			return ex2.errorCode;
		}
		if (!bIgnoreTrain)
		{
			bool bDelete = false;
			if (cmd == "DeleteMarkInspModel")
			{
				bDelete = true;
			}
			ModelProcess(bDelete);
		}
		return 0;
	}

	private void DeleteImage(object obj, bool bIsTrain = true)
	{
		try
		{
			int selectedIndex = lst.SelectedIndex;
			if (obj == null)
			{
				if (lst.SelectedItems != null)
				{
					List<ImageEntity> list = lst.SelectedItems.OfType<ImageEntity>().ToList();
					foreach (ImageEntity item in list)
					{
						_ImageList.Remove(item);
						if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
						{
							File.Delete(item.ImagePath);
						}
						OnPropertyChanged("ShowAllTextCount");
					}
				}
			}
			else
			{
				ImageEntity val = (ImageEntity)((obj is ImageEntity) ? obj : null);
				if (val != null)
				{
					_ImageList.Remove(val);
					if (!string.IsNullOrEmpty(val.ImagePath) && File.Exists(val.ImagePath))
					{
						File.Delete(val.ImagePath);
					}
					OnPropertyChanged("ShowAllTextCount");
				}
			}
			if (selectedIndex + 1 > lst.Items.Count)
			{
				lst.SelectedIndex = selectedIndex - 1;
				IsCanTrain = false;
				_CurIndex = lst.SelectedIndex;
			}
			else
			{
				lst.SelectedIndex = selectedIndex;
			}
			if (bIsTrain)
			{
				if (lst.Items.Count == 0)
				{
					imageView.ClearShapesE();
					CurImage = null;
				}
				else
				{
					NextImageProcess();
				}
			}
		}
		catch
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_NoneChoose", MessageIcons.Error, isTranslate: true);
		}
	}

	protected void AddImageByDir(string dir, bool bSyn = false)
	{
		try
		{
			if (Directory.Exists(dir))
			{
				if (bSyn)
				{
					DoAddImages(SortFilesByTime(Directory.GetFileSystemEntries(dir).ToArray()), bSyn);
				}
				else
				{
					DoAddImages(Directory.GetFileSystemEntries(dir).ToArray(), bSyn);
				}
			}
		}
		catch (Exception)
		{
			Dispatcher.Invoke(() =>
			{
				HikMessageBox.Show("Lang_Err", "Lang_FileScanErr", MessageIcons.Error, isTranslate: true);
			});
		}
	}

	private void FreshImageList()
	{
		if (_FreshListCount >= 5)
		{
			return;
		}
		_FreshListCount++;
		Dispatcher.BeginInvoke((Action)(() =>
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Expected Obj, but got Unknown
			foreach (ImageEntity item in (IEnumerable)lst.Items)
			{
				ImageEntity val = item;
				FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(val);
				if (val != null)
				{
					if (IsChildVisibleInParent(child, listScroll))
					{
						if (val.ImageSource == null)
						{
							BitmapImage bitmapImage = ImageData.CreateThumbnail(val.ImagePath, 126.0, 102.0);
							if (bitmapImage != null)
							{
								val.ImageSource = bitmapImage;
							}
						}
					}
					else
					{
						val.ImageSource = null;
					}
				}
			}
			_FreshListCount--;
		}));
	}

	private bool IsChildVisibleInParent(FrameworkElement child, FrameworkElement parent)
	{
		GeneralTransform generalTransform = child.TransformToAncestor(parent);
		if (generalTransform == null)
		{
			return false;
		}
		Rect rect = generalTransform.TransformBounds(new Rect(new Point(0.0, 0.0), child.RenderSize));
		return new Rect(new Point(0.0, 0.0), parent.RenderSize).IntersectsWith(rect);
	}

	private bool IsFullVisibleInParent(FrameworkElement child, FrameworkElement parent)
	{
		GeneralTransform generalTransform = child.TransformToAncestor(parent);
		if (generalTransform == null)
		{
			return true;
		}
		Rect rect = generalTransform.TransformBounds(new Rect(new Point(0.0, 0.0), child.RenderSize));
		return new Rect(new Point(0.0, 0.0), parent.RenderSize).Contains(rect);
	}

	private double GetHorizontalOffset(FrameworkElement child, FrameworkElement parent)
	{
		double result = 0.0;
		GeneralTransform generalTransform = child.TransformToAncestor(parent);
		if (generalTransform != null)
		{
			Rect rect = generalTransform.TransformBounds(new Rect(new Point(0.0, 0.0), child.RenderSize));
			Rect rect2 = new Rect(new Point(0.0, 0.0), parent.RenderSize);
			Rect rect3 = Rect.Intersect(rect2, rect);
			if (rect3 != Rect.Empty)
			{
				result = ((lst.SelectedIndex <= 5) ? (rect.Width - rect3.Width) : rect.Width);
			}
			else
			{
				result = rect.Width;
			}
		}
		return result;
	}

	private bool LoadCurrentImage()
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected Obj, but got Unknown
		string newPath = GetNewImageName();
		if (newPath == null)
		{
			return false;
		}
		ImageData curImageData = OCRTrainDataMgr.Instance.CurImageData;
		if (curImageData != null)
		{
			curImageData.SaveGrayImage(newPath);
			if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == newPath) == null)
			{
				_ImageList.Add(new ImageEntity
				{
					ImagePath = newPath
				});
			}
			OnPropertyChanged("ShowAllTextCount");
			return true;
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoCurrentImage"), MessageIcons.Warning);
		return false;
	}

	private bool LoadCacheImage()
	{
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected Obj, but got Unknown
		bool flag = false;
		List<ImageData> cacheImageList = ModuleCommonMethodMgr.Instance.GetCacheImageList(OCRTrainDataMgr.Instance.Module);
		if (cacheImageList != null && cacheImageList.Count > 0 && Enumerable.Count(_ImageList) < 500)
		{
			foreach (ImageData item in cacheImageList)
			{
				if (Enumerable.Count(_ImageList) >= 500)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 500, MessageButtons.Normal, MessageIcons.Warning);
					break;
				}
				string imageName = GetNewImageName();
				if (imageName != null)
				{
					item.SaveGrayImage(imageName);
					if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == imageName) == null)
					{
						_ImageList.Add(new ImageEntity
						{
							ImagePath = imageName
						});
					}
					flag = true;
				}
			}
		}
		if (flag)
		{
			OnPropertyChanged("ShowAllTextCount");
			return true;
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoCacheImage"), MessageIcons.Warning);
		return false;
	}

	private bool LoadLocalImage()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Multiselect = true;
		string text = ConfigurationHelper.INIGetDefaultOpenPath(OCRTrainDataMgr.Instance.Module?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpg;*.jpeg;*.png";
		if (openFileDialog.ShowDialog() == false)
		{
			return false;
		}
		int num = (_imageCount = ImageList.Count);
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), OCRTrainDataMgr.Instance.Module?.ModuleDir);
		OCRTrainDataMgr.Instance.OnShowProgressWindowEvent();
		DoAddImages(openFileDialog.FileNames);
		if (_imageCount > num)
		{
			return true;
		}
		return false;
	}

	private bool LoadFolder()
	{
		string text = "";
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		string text2 = ConfigurationHelper.INIGetDefaultOpenPath(OCRTrainDataMgr.Instance.Module?.ModuleDir);
		if (text2 != null)
		{
			folderBrowserDialog.SelectedPath = text2;
		}
		System.Windows.Forms.DialogResult dialogResult = folderBrowserDialog.ShowDialog();
		if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
		{
			folderBrowserDialog.Dispose();
			return false;
		}
		try
		{
			text = folderBrowserDialog.SelectedPath.Trim();
		}
		catch (PathTooLongException)
		{
			folderBrowserDialog.Dispose();
			HikMessageBox.Show("Lang_DrAdd", "Lang_FilePathLong", MessageIcons.Error, isTranslate: true);
			return false;
		}
		folderBrowserDialog.Dispose();
		OCRTrainDataMgr.Instance.OnShowProgressWindowEvent();
		int num = (_imageCount = ImageList.Count);
		AddImageByDir(text);
		ConfigurationHelper.INISetDefaultOpenPath(text, OCRTrainDataMgr.Instance.Module?.ModuleDir);
		if (_imageCount > num)
		{
			return true;
		}
		return false;
	}

	private async void LoadImagesAsync(ModuleTrainData moduleTrainData)
	{
		await Task.Run(() =>
		{
			string savePath = GetSavePath();
			OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
			if (Directory.Exists(savePath))
			{
				OCRTrainDataMgr.Instance.OnShowProgressWindowEvent();
			}
			_imageCount = 0;
			OCRTrainDataMgr.Instance.IsUpImageLimit = false;
			AddImageByDir(savePath, bSyn: true);
			_modelImageCount = moduleTrainData.ModelImageCount;
			_CurIndex = 0;
			Dispatcher.BeginInvoke((Action)(() =>
			{
				if (ImageList.Count > 0 && moduleTrainData.CurSelectIndex < ImageList.Count)
				{
					if (moduleTrainData.TrainFlagList != null && moduleTrainData.TrainFlagList.Count > 0)
					{
						int count = moduleTrainData.TrainFlagList.Count;
						for (int i = 0; i < count; i++)
						{
							ImageList[i].IsCalibrated = true;
							ImageList[i].IsOKCalibrated = moduleTrainData.TrainFlagList[i];
						}
						lst.SelectedIndex = moduleTrainData.CurSelectIndex;
						_CurIndex = moduleTrainData.CurSelectIndex;
						_CurHorizontalOffset = moduleTrainData.CurHorizontalOffset;
						listScroll.ScrollToHorizontalOffset(_CurHorizontalOffset);
					}
					else if (moduleTrainData.TrainFlagList == null)
					{
						moduleTrainData.TrainFlagList = new List<bool>();
					}
				}
				if (_CurIndex == 0 && _imageCount > 0)
				{
					lst.SelectedIndex = 0;
				}
				IsCanTrain = IsTrain();
				OCRTrainDataMgr.Instance.OnCloseProgressWindowEvent();
				OCRTrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
			}));
		});
	}

	private string GetNewImageName(string extension = ".bmp")
	{
		string result = null;
		if (OCRTrainDataMgr.Instance.Module != null)
		{
			string text = GetSavePath() + "\\image";
			string text2 = text + extension;
			for (uint num = 0u; num < uint.MaxValue; num++)
			{
				if (File.Exists(text2))
				{
					text2 = text + "_" + num + extension;
					continue;
				}
				result = text2;
				break;
			}
		}
		return result;
	}

	private bool IsTrain()
	{
		if (lst.Items.Count > lst.SelectedIndex + 1)
		{
			return true;
		}
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null && !val.IsCalibrated)
		{
			return true;
		}
		return false;
	}

	private void ResetImageList(bool bIsNeedExact)
	{
		if (_ImageList.Count <= 0)
		{
			return;
		}
		foreach (ImageEntity image in _ImageList)
		{
			image.IsCalibrated = false;
			image.IsOKCalibrated = false;
		}
		listScroll.ScrollToLeftEnd();
		lst.SelectedIndex = 0;
		IsCanTrain = true;
		_CurHorizontalOffset = 0.0;
		_CurIndex = 0;
		SelectListViewIndex = 0;
		if (bIsNeedExact)
		{
			NextImageProcess();
		}
	}

	private string[] SortFilesByTime(string[] fileNames)
	{
		int num = fileNames.Length;
		for (int i = 0; i < num - 1; i++)
		{
			for (int j = i + 1; j < num; j++)
			{
				long num2 = File.GetCreationTime(fileNames[i]).ToFileTime();
				long num3 = File.GetCreationTime(fileNames[j]).ToFileTime();
				if (num2 > num3)
				{
					string text = fileNames[i];
					fileNames[i] = fileNames[j];
					fileNames[j] = text;
				}
			}
		}
		return fileNames;
	}

	private void DoAddImages(string[] fileNames, bool bSyn = false)
	{
		foreach (string name in fileNames)
		{
			if (OCRTrainDataMgr.Instance.IsUpImageLimit)
			{
				break;
			}
			if (Directory.Exists(name))
			{
				AddImageByDir(name);
				continue;
			}
			if (_imageCount >= 500)
			{
				if (!OCRTrainDataMgr.Instance.IsUpImageLimit)
				{
					OCRTrainDataMgr.Instance.IsUpImageLimit = true;
				}
				break;
			}
			string text = Path.GetExtension(name).ToUpper();
			if (text != ".JPG" && text != ".JPEG" && text != ".BMP" && text != ".PNG")
			{
				continue;
			}
			_imageCount++;
			if (bSyn)
			{
				DoAddImage(name);
				continue;
			}
			DispatcherOperation item = Dispatcher.BeginInvoke((Action)(() =>
			{
				DoAddImage(name);
				OnPropertyChanged("ShowAllTextCount");
			}));
			_dispatcherOperationList.Add(item);
		}
		if (bSyn)
		{
			OnPropertyChanged("ShowAllTextCount");
		}
	}

	private string DoAddImage(string name, bool needCopy = false, bool AddToList = true)
	{
		if (name.Length >= 260)
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_FilePathLong", MessageIcons.Error, isTranslate: true);
			return null;
		}
		string text = null;
		if (Path.GetDirectoryName(name) != GetSavePath())
		{
			string extension = Path.GetExtension(name);
			text = GetNewImageName(extension);
		}
		AddImageToList(name, text, AddToList);
		return text;
	}

	private void AddImageToList(string strImagePath, string newPath = null, bool AddToList = true)
	{
		if (strImagePath.Length > 0 && strImagePath[0] == '\\')
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_LocalImageNeed", MessageIcons.Error, isTranslate: true);
		}
		else
		{
			if (!File.Exists(strImagePath))
			{
				return;
			}
			try
			{
				bool bHasImageData = false;
				if (newPath != null)
				{
					Task task = new Task(() =>
					{
						ImageData imageData = new ImageData
						{
							PixelFormat = PixelFormats.Gray8
						};
						ImageProcessManager.GetMono8ImageData(strImagePath, ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer);
						if (imageData.ImageBuffer != null)
						{
							imageData.SaveGrayImage(newPath);
							bHasImageData = true;
						}
						strImagePath = newPath;
					});
					task.Start();
					Task.WaitAll(task);
				}
				else
				{
					bHasImageData = true;
				}
				if (!AddToList)
				{
					return;
				}
				if (!bHasImageData)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetImageDataError"), MessageIcons.Error);
				}
				else if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == strImagePath) == null)
				{
					Dispatcher.Invoke(() =>
					{
						//IL_000c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0011: Unknown result type (might be due to invalid IL or missing references)
						//IL_0023: Expected Obj, but got Unknown
						_ImageList.Add(new ImageEntity
						{
							ImagePath = strImagePath
						});
					});
				}
			}
			catch
			{
				HikMessageBox.Show("Lang_ImageAdd", "Lang_ImagePathError", MessageIcons.Error, isTranslate: true);
			}
		}
	}

	private bool CreateFolder(string strPath)
	{
		if (!Directory.Exists(strPath))
		{
			Directory.CreateDirectory(strPath);
			if (!Directory.Exists(strPath))
			{
				return false;
			}
		}
		return true;
	}

	private string GetSavePath()
	{
		string fullPath = Path.GetFullPath("./TempImage");
		if (string.IsNullOrEmpty(_imagePath))
		{
			_imagePath = ModuleCommonMethodMgr.Instance.GenerateUniqueText(8);
		}
		return fullPath + "\\" + _imagePath;
	}
}
