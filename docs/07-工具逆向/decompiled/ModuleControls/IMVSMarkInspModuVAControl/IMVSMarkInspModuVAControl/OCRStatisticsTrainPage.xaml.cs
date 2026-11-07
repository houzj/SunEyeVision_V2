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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Apps.Data;
using Apps.Data.Common;
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.RenderInterface;

namespace IMVSMarkInspModuVAControl;

public partial class OCRStatisticsTrainPage : System.Windows.Controls.UserControl, INotifyPropertyChanged, IComponentConnector
{
	protected ObservableCollection<ImageEntity> _ImageList = new ObservableCollection<ImageEntity>();

	private int _FreshListCount = 0;

	private int _markNum = 0;

	private int _CurIndex = 0;

	private double _CurHorizontalOffset = 0.0;

	private bool _isUpdateOffset = true;

	private int _PanRectWidth = 10;

	private bool _IsInit = false;

	private int _imageCount = 0;

	private ObservableCollection<DispatcherOperation> _dispatcherOperationList = new ObservableCollection<DispatcherOperation>();

	private int _modelImageCount = 0;

	private string _imagePath = null;

	private bool _isNeedExact = true;

	public static readonly DependencyProperty IsCanTrainProperty = DependencyProperty.Register("IsCanTrain", typeof(bool), typeof(OCRStatisticsTrainPage), new PropertyMetadata(false));

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(OCRStatisticsTrainPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BoxImageProperty = DependencyProperty.Register("BoxImage", typeof(ImageSource), typeof(OCRStatisticsTrainPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty TrainCountProperty = DependencyProperty.Register("TrainCount", typeof(int), typeof(OCRStatisticsTrainPage), new PropertyMetadata(0));

	private ObservableCollection<object> shapeList = new ObservableCollection<object>();

	private static List<string> moreOptions = new List<string> { "当前图像", "缓存图像", "导入本地图像", "导入文件夹" };

	private DelegateCommand freshOptionsCommand = null;

	private DelegateCommand<object> clickCommand = null;

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

	public int MarkNum
	{
		get
		{
			return _markNum;
		}
		set
		{
			if (_markNum != value)
			{
				_markNum = value;
				OnPropertyChanged("MarkNum");
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
				OCRTrainDataMgr.Instance.DeleteImageEvent += DeleteImageEvent;
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

	private void Abort_OnClick(object sender, RoutedEventArgs e)
	{
		ClearStatData();
	}

	private void TrainCurImage_OnClick(object sender, RoutedEventArgs e)
	{
		if (lst.SelectedIndex == 0)
		{
			listScroll.ScrollToLeftEnd();
		}
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val == null)
		{
			return;
		}
		if (!val.IsCalibrated || !val.IsOKCalibrated)
		{
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
				string strMsg = string.Format("{0}{1}({2}{3})", AppLocalizationService.GetLang("Lang_SetCurImageDataError"), Environment.NewLine, AppLocalizationService.GetLang("errInfo:"), text);
				HikMessageBox.Show(strMsg, MessageIcons.Error);
				Marshal.FreeHGlobal(selectedImagePtr);
				return;
			}
			Marshal.FreeHGlobal(selectedImagePtr);
			int num = Train("TrainCurImage");
			if ((long)num != 0)
			{
				string text2 = AppLocalizationService.GetLang("Lang_TrainImageError") + "\r\n(";
				if (num == -1)
				{
					text2 += AppLocalizationService.GetLang("Lang_ParamErr");
				}
				else
				{
					text2 += AppLocalizationService.GetLang("errCode:");
					text2 += $"{num:X}, ";
					text2 += ErrorCodeInfo.GetErrorInfo(num);
				}
				text2 += ")";
				HikMessageBox.Show(text2, MessageIcons.Error);
				return;
			}
			if (!val.IsCalibrated)
			{
				val.IsCalibrated = true;
				MarkNum++;
			}
			val.IsOKCalibrated = true;
		}
		if (lst.SelectedIndex + 1 < lst.Items.Count)
		{
			_isUpdateOffset = false;
			lst.SelectedIndex++;
			if (lst.SelectedIndex > 1)
			{
				ScrollImageList();
			}
		}
	}

	private void IgnoreCurImage_OnClick(object sender, RoutedEventArgs e)
	{
		if (lst.SelectedIndex == 0)
		{
			listScroll.ScrollToLeftEnd();
		}
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val == null)
		{
			return;
		}
		if (val.IsCalibrated)
		{
			if (val.IsOKCalibrated)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoRecordImageWarn"), MessageIcons.Warning);
			}
		}
		else
		{
			Train("IgnoreCurImage", bIgnoreTrain: true);
			val.IsCalibrated = true;
			val.IsOKCalibrated = false;
			MarkNum++;
		}
		if (lst.SelectedIndex + 1 < lst.Items.Count)
		{
			_isUpdateOffset = false;
			lst.SelectedIndex++;
			if (lst.SelectedIndex > 1)
			{
				ScrollImageList();
			}
		}
	}

	public void NextImageProcess()
	{
		imageViewBox.ClearExternShapes();
		ShapeList.Clear();
		int len = 0;
		IntPtr selectedImagePtr = GetSelectedImagePtr(out len);
		if (selectedImagePtr == IntPtr.Zero)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetCurImageDataError"), MessageIcons.Warning);
			return;
		}
		try
		{
			CurImage = ModuleCommonMethodMgr.Instance.GetMono8ImageData(selectedImagePtr);
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
			OCRTrainDataMgr.Instance.Param.TimeOut = 2147483647u;
			byte[] value = new byte[1];
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["ExactMatchTrain"] = value;
			OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
			NextImageDisplay();
		}
		catch (Exception ex2)
		{
			int result2 = 0;
			string text3 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
			string text4 = AppLocalizationService.GetLang("Lang_NextImageExactError") + "\r\n(";
			text4 += AppLocalizationService.GetLang("errInfo:");
			text4 += text3;
			text4 += ")";
			HikMessageBox.Show(text4, MessageIcons.Error);
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

	private void DelAllImage_Click(object sender, RoutedEventArgs e)
	{
		while (_ImageList.Count > 0)
		{
			DeleteImage(null, bIsTrain: false);
		}
		string savePath = GetSavePath();
		if (Directory.Exists(savePath))
		{
			Directory.Delete(savePath, recursive: true);
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
	}

	private void lst_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		_CurIndex = lst.SelectedIndex;
		if (_isNeedExact)
		{
			if (_CurIndex >= 0)
			{
				NextImageProcess();
			}
		}
		else
		{
			_isNeedExact = true;
		}
		if (_isUpdateOffset)
		{
			if (listScroll != null)
			{
				_CurHorizontalOffset = listScroll.HorizontalOffset;
			}
		}
		else
		{
			_isUpdateOffset = true;
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
						IsCanTrain = true;
						if (lst.SelectedIndex < 0)
						{
							lst.SelectedIndex = 0;
						}
						else
						{
							object selectedItem = lst.SelectedItem;
							ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
							if (val != null && val.IsCalibrated)
							{
								lst.SelectedIndex++;
							}
						}
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
						});
						task.Start();
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
		moduleTrainData.ModelImageCount = _modelImageCount;
		moduleTrainData.TrainFlagList.Clear();
		moduleTrainData.TrainImageNameList.Clear();
		foreach (ImageEntity image in ImageList)
		{
			if (image.IsCalibrated)
			{
				moduleTrainData.TrainFlagList.Add(image.IsOKCalibrated);
				moduleTrainData.TrainImageNameList.Add(image.ImageName);
			}
		}
		moduleTrainData.ImagePath = _imagePath;
		OCRTrainDataMgr.Instance.DeleteImageEvent -= DeleteImageEvent;
		if (imageViewBox != null)
		{
			imageViewBox.DataContext = null;
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

	public void ClearStatData()
	{
		if ((long)Train("DeleteMarkInspModel") == 0)
		{
			OCRTrainDataMgr.Instance.OnEmptyModelEvent();
		}
		ResetImageList();
	}

	public void ClearData()
	{
		_isNeedExact = false;
		ClearStatData();
		CurImage = null;
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

	private unsafe IntPtr GetSelectedImagePtr(out int len)
	{
		len = 0;
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null)
		{
			ImageData imageDataFromFile = ImageData.GetImageDataFromFile(val.ImagePath, 0, 0, needGC: true);
			if (imageDataFromFile == null || imageDataFromFile.Buffer == null)
			{
				DeleteImage(null);
				return IntPtr.Zero;
			}
			if (imageDataFromFile.Width > 0)
			{
				val.ImageWidth = imageDataFromFile.Width;
				val.ImageHeight = imageDataFromFile.Height;
				int num = 8;
				len = num + imageDataFromFile.Width * imageDataFromFile.Height;
				IntPtr result = Marshal.AllocHGlobal(len);
				fixed (byte* buffer = imageDataFromFile.Buffer)
				{
					int num2 = imageDataFromFile.Buffer.Length;
					void* ptr = result.ToPointer();
					*(int*)ptr = imageDataFromFile.Width;
					((int*)ptr)[1] = imageDataFromFile.Height;
					byte* ptr2 = (byte*)ptr + num;
					for (int i = 0; i < num2; i++)
					{
						*ptr2 = buffer[i];
						ptr2++;
					}
				}
				return result;
			}
		}
		return IntPtr.Zero;
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
			MyOutlinePoint myOutlinePoint = new MyOutlinePoint();
			myOutlinePoint.OutLineInfo = array2;
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
				ShapeList.Remove(multiPoint2);
				imageViewBox?.ClearExternShapes();
				ShapeList.Add(multiPoint2);
			}
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
		imageViewBox.ClearExternShapes();
		ShapeList.Clear();
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
			array2 = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetExactPatMatchStatus"];
			if (array2 != null && array2.Length != 0)
			{
				IntPtr source = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
				Marshal.Copy(source, array, 0, array.Length);
			}
		}
		catch
		{
		}
		List<object> rects = OCRTrainDataMgr.Instance.GetRects(box);
		if (rects == null)
		{
			return;
		}
		foreach (Apps.Data.ShapeDefine.Rect item in rects)
		{
			item.Width++;
			item.Height++;
			int num = rects.IndexOf(item);
			if (num < array.Length)
			{
				item.ColorA = ((array[num] == 1) ? "#00FF00" : "#FF0000");
			}
			ShapeList.Add(item);
		}
	}

	private void NextImageDisplay()
	{
		if (OCRTrainDataMgr.Instance.Param == null || CurImage == null)
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
				array2 = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetExactPatMatchStatus"];
				if (array2 != null && array2.Length != 0)
				{
					IntPtr source = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
					Marshal.Copy(source, array, 0, array.Length);
				}
			}
			catch
			{
			}
			List<object> rects = OCRTrainDataMgr.Instance.GetRects(box);
			if (rects != null)
			{
				foreach (Apps.Data.ShapeDefine.Rect item in rects)
				{
					int num = rects.IndexOf(item);
					if (num < array.Length)
					{
						item.ColorA = ((array[num] == 1) ? "#00FF00" : "#FF0000");
					}
					ShapeList.Add(item);
				}
			}
		}
		object obj2 = ShapeList.FirstOrDefault((object p) => p is IMultiPoint);
		if (obj2 != null)
		{
			ShapeList.Remove(obj2);
		}
		imageViewBox.ClearExternShapes();
		GetFeatureModel();
	}

	public void ModelProcess(bool bDelete = false)
	{
		if (OCRTrainDataMgr.Instance.Param != null)
		{
			if (bDelete)
			{
				imageViewBox.ClearExternShapes();
				ShapeList.Clear();
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
				ImageBoxes.ItemsSource = GetModelImages();
			}
		}
	}

	private ObservableCollection<ModelImage> GetModelImages()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return null;
		}
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)OCRTrainDataMgr.Instance.Param)["GetModelImage"];
			if (array != null && array.Length != 0)
			{
				int num = array.Length;
				intPtr = Marshal.AllocHGlobal(num);
				if (intPtr == IntPtr.Zero)
				{
					return null;
				}
				Marshal.Copy(array, 0, intPtr, num);
				ObservableCollection<ModelImage> moduleImages = ModelImage.GetModuleImages(intPtr, num);
				Marshal.FreeHGlobal(intPtr);
				return moduleImages;
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			LogHelper.moduleLog.Error("GetModelImages error: " + text);
		}
		if (intPtr != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(intPtr);
		}
		return null;
	}

	private int Train(string cmd, bool bIgnoreTrain = false)
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return -1;
		}
		try
		{
			OCRTrainDataMgr.Instance.Param.TimeOut = 2147483647u;
			byte[] value = new byte[1];
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)[cmd] = value;
			OCRTrainDataMgr.Instance.Param.TimeOut = 3000u;
		}
		catch (Exception ex)
		{
			if (cmd == "TrainCurImage")
			{
				RefreshTrainDisplay();
			}
			int result = 0;
			return int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? result : (-1);
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
			_isNeedExact = bIsTrain;
			if (selectedIndex + 1 > lst.Items.Count)
			{
				lst.SelectedIndex = selectedIndex - 1;
			}
			else
			{
				lst.SelectedIndex = selectedIndex;
			}
			if (lst.Items.Count == 0)
			{
				imageViewBox.ClearExternShapes();
				ShapeList.Clear();
				CurImage = null;
				IsCanTrain = false;
				_isNeedExact = true;
				_CurHorizontalOffset = 0.0;
				listScroll.ScrollToLeftEnd();
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
		System.Windows.Rect rect = generalTransform.TransformBounds(new System.Windows.Rect(new System.Windows.Point(0.0, 0.0), child.RenderSize));
		return new System.Windows.Rect(new System.Windows.Point(0.0, 0.0), parent.RenderSize).IntersectsWith(rect);
	}

	private bool IsFullVisibleInParent(FrameworkElement child, FrameworkElement parent)
	{
		GeneralTransform generalTransform = child.TransformToAncestor(parent);
		if (generalTransform == null)
		{
			return true;
		}
		System.Windows.Rect rect = generalTransform.TransformBounds(new System.Windows.Rect(new System.Windows.Point(0.0, 0.0), child.RenderSize));
		return new System.Windows.Rect(new System.Windows.Point(0.0, 0.0), parent.RenderSize).Contains(rect);
	}

	private double GetHorizontalOffset(FrameworkElement child, FrameworkElement parent)
	{
		double result = 0.0;
		GeneralTransform generalTransform = child.TransformToAncestor(parent);
		if (generalTransform != null)
		{
			System.Windows.Rect rect = generalTransform.TransformBounds(new System.Windows.Rect(new System.Windows.Point(0.0, 0.0), child.RenderSize));
			System.Windows.Rect rect2 = new System.Windows.Rect(new System.Windows.Point(0.0, 0.0), parent.RenderSize);
			System.Windows.Rect rect3 = System.Windows.Rect.Intersect(rect2, rect);
			if (rect3 != System.Windows.Rect.Empty)
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
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected Obj, but got Unknown
		string newPath = GetNewImageName();
		if (newPath == null)
		{
			return false;
		}
		ImageData imageData = OCRTrainDataMgr.Instance.CurImageData;
		if (imageData != null)
		{
			try
			{
				if (imageData.PixelFormat != PixelFormats.Gray8)
				{
					ImageSaveOpenCvHelper.SaveRawImageSyn(newPath, imageData);
					imageData = ImageData.GetMono8ImageDataFromFile(newPath);
				}
				ImageSaveOpenCvHelper.SaveRawImageSyn(newPath, imageData);
				if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == newPath) == null)
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = newPath
					});
				}
			}
			catch (Exception ex)
			{
				HikMessageBox.Show("Lang_ImageAdd", ex.Message, MessageIcons.Error, isTranslate: true);
				return false;
			}
			OnPropertyChanged("ShowAllTextCount");
			return true;
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoCurrentImage"), MessageIcons.Warning);
		return false;
	}

	private bool LoadCacheImage()
	{
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected Obj, but got Unknown
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
				if (imageName == null)
				{
					continue;
				}
				try
				{
					ImageData imageData = item;
					if (imageData.PixelFormat != PixelFormats.Gray8)
					{
						ImageSaveOpenCvHelper.SaveRawImageSyn(imageName, item);
						imageData = ImageData.GetMono8ImageDataFromFile(imageName);
					}
					ImageSaveOpenCvHelper.SaveRawImageSyn(imageName, imageData);
					if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == imageName) == null)
					{
						_ImageList.Add(new ImageEntity
						{
							ImagePath = imageName
						});
					}
				}
				catch (Exception ex)
				{
					HikMessageBox.Show("Lang_ImageAdd", ex.Message, MessageIcons.Error, isTranslate: true);
					return false;
				}
				flag = true;
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
		ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(openFileDialog.FileName), OCRTrainDataMgr.Instance.Module?.ModuleDir);
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
			Dispatcher.BeginInvoke((Action)(() =>
			{
				if (moduleTrainData.TrainFlagList.Count == moduleTrainData.TrainImageNameList.Count)
				{
					int i;
					for (i = 0; i < moduleTrainData.TrainImageNameList.Count; i++)
					{
						ImageEntity val = ImageList.FirstOrDefault((ImageEntity x) => x.ImageName == moduleTrainData.TrainImageNameList[i]);
						if (val != null)
						{
							val.IsCalibrated = true;
							val.IsOKCalibrated = moduleTrainData.TrainFlagList[i];
							int markNum = MarkNum;
							MarkNum = markNum + 1;
						}
					}
				}
				if (_imageCount > 0)
				{
					lst.SelectedIndex = 0;
					IsCanTrain = true;
				}
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

	private void ResetImageList()
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
		_CurHorizontalOffset = 0.0;
		_CurIndex = 0;
		MarkNum = 0;
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
			string text = System.IO.Path.GetExtension(name).ToUpper();
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
		if (System.IO.Path.GetDirectoryName(name) != GetSavePath())
		{
			string extension = System.IO.Path.GetExtension(name);
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
						try
						{
							ImageData mono8ImageDataFromFile = ImageData.GetMono8ImageDataFromFile(strImagePath);
							if (mono8ImageDataFromFile.ImageBuffer != null && !string.IsNullOrEmpty(newPath))
							{
								ImageSaveOpenCvHelper.SaveRawImageSyn(newPath, mono8ImageDataFromFile);
								bHasImageData = true;
							}
							strImagePath = newPath;
						}
						catch (Exception ex)
						{
							LogHelper.Error(ex.Message);
						}
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
		string fullPath = System.IO.Path.GetFullPath("./TempImage");
		if (string.IsNullOrEmpty(_imagePath))
		{
			_imagePath = ModuleCommonMethodMgr.Instance.GenerateUniqueText(8);
		}
		return fullPath + "\\" + _imagePath;
	}
}
