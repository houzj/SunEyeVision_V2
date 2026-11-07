using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
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
using Hik.Util.Model;
using HikExternalCall.ImageProcess;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMBaseControls.Base.ImageView.Shape;
using VMControls.BaseInterface;

namespace IMVSInspectModuControl;

public partial class BatchTrainPage : System.Windows.Controls.UserControl, INotifyPropertyChanged, IComponentConnector
{
	private ObservableCollection<ImageEntity> _ImageList = new ObservableCollection<ImageEntity>();

	private int _FreshListCount = 0;

	private int _SelectedItemsCount;

	private int _SelectListViewIndex = 0;

	private int _CurIndex = -1;

	private double _CurHorizontalOffset = 0.0;

	private bool _IsInit = false;

	private int _markOkNum = 0;

	private int _markNgNum = 0;

	private ImageListType imageListType = ImageListType.All;

	private int _displayImageCount = 0;

	private double _okStandard = 50.0;

	private int _scoreOkNum = 0;

	private int _scoreNgNum = 0;

	private bool _isEnable = false;

	private string _imagePath = null;

	private bool _IsDisplayVerifyParam = false;

	private bool _isVerifing = false;

	private bool _isTraining = false;

	private bool _isUpdateOffset = true;

	private bool _isUpdateScrolOffset = false;

	private ImageData _modelImage = null;

	private ROISelecter _selectedROI = null;

	private bool _isExportToModel = false;

	private ObservableCollection<SampleImageData> _sampleImages;

	private Enumeration _bgNoiseType;

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(BatchTrainPage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty IsTrainFinishProperty = DependencyProperty.Register("IsTrainFinish", typeof(bool), typeof(BatchTrainPage), new PropertyMetadata(false));

	private ObservableCollection<object> shapeList = new ObservableCollection<object>();

	private static List<string> moreOptions = new List<string> { "当前图像", "缓存图像", "导入本地图像", "导入文件夹", "批量导入OK图像", "批量导入NG图像" };

	private List<string> comboOptions = new List<string>
	{
		AppLocalizationService.GetLang("All"),
		AppLocalizationService.GetLang("Lang_RegisterAsOK"),
		AppLocalizationService.GetLang("Lang_RegisterAsNG"),
		AppLocalizationService.GetLang("Lang_Unregistered"),
		AppLocalizationService.GetLang("Lang_ModelVerificationResult")
	};

	private DelegateCommand freshOptionsCommand = null;

	private DelegateCommand<object> clickCommand = null;

	internal ParamListView paramTrainUI;

	internal ParamListView paramVerifyUI;

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

	public double OKStandard
	{
		get
		{
			return _okStandard;
		}
		set
		{
			if (_okStandard == value)
			{
				return;
			}
			_okStandard = value;
			foreach (ImageEntity image in ImageList)
			{
				if (!image.IsVerify)
				{
					continue;
				}
				if ((double)image.ImageScore >= _okStandard)
				{
					if (!image.IsOKVerify)
					{
						image.IsOKVerify = true;
						ScoreOkNum++;
						ScoreNgNum--;
					}
				}
				else if (image.IsOKVerify)
				{
					image.IsOKVerify = false;
					ScoreOkNum--;
					ScoreNgNum++;
				}
			}
			UpdateDisplayInfo();
			OnPropertyChanged("OKStandard");
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

	public ObservableCollection<ImageEntity> DisplayImageList
	{
		get
		{
			if (imageListType == ImageListType.All)
			{
				DisplayImageCount = _ImageList.Count;
				return _ImageList;
			}
			ObservableCollection<ImageEntity> observableCollection = new ObservableCollection<ImageEntity>();
			switch (imageListType)
			{
			case ImageListType.OKImage:
				foreach (ImageEntity image in ImageList)
				{
					if (image.IsCalibrated && image.IsOKCalibrated)
					{
						observableCollection.Add(image);
					}
				}
				break;
			case ImageListType.NGImage:
				foreach (ImageEntity image2 in ImageList)
				{
					if (image2.IsCalibrated && !image2.IsOKCalibrated)
					{
						observableCollection.Add(image2);
					}
				}
				break;
			case ImageListType.UnregisterImage:
				foreach (ImageEntity image3 in ImageList)
				{
					if (!image3.IsCalibrated)
					{
						observableCollection.Add(image3);
					}
				}
				break;
			case ImageListType.VerifyImage:
				foreach (ImageEntity image4 in ImageList)
				{
					if (image4.IsVerify)
					{
						observableCollection.Add(image4);
					}
				}
				break;
			}
			DisplayImageCount = observableCollection.Count;
			return observableCollection;
		}
	}

	public int CurIndex => _CurIndex + 1;

	public int AllImageCount
	{
		get
		{
			int count = _ImageList.Count;
			if (count > 0)
			{
				IsEnable = true;
			}
			else
			{
				IsEnable = false;
			}
			return count;
		}
	}

	public bool IsEnable
	{
		get
		{
			return _isEnable;
		}
		set
		{
			if (_isEnable != value)
			{
				_isEnable = value;
				OnPropertyChanged("IsEnable");
			}
		}
	}

	public bool IsTrainFinish
	{
		get
		{
			return (bool)GetValue(IsTrainFinishProperty);
		}
		set
		{
			SetValue(IsTrainFinishProperty, value);
		}
	}

	public int DisplayImageCount
	{
		get
		{
			return _displayImageCount;
		}
		set
		{
			if (_displayImageCount != value)
			{
				_displayImageCount = value;
				OnPropertyChanged("DisplayImageCount");
			}
		}
	}

	public int MarkOkNum
	{
		get
		{
			return _markOkNum;
		}
		set
		{
			if (_markOkNum != value)
			{
				_markOkNum = value;
				OnPropertyChanged("MarkOkNum");
				OnPropertyChanged("MarkNum");
			}
		}
	}

	public int MarkNgNum
	{
		get
		{
			return _markNgNum;
		}
		set
		{
			if (_markNgNum != value)
			{
				_markNgNum = value;
				OnPropertyChanged("MarkNgNum");
				OnPropertyChanged("MarkNum");
			}
		}
	}

	public int MarkNum => MarkOkNum + MarkNgNum;

	public int ScoreOkNum
	{
		get
		{
			return _scoreOkNum;
		}
		set
		{
			if (_scoreOkNum != value)
			{
				_scoreOkNum = value;
				OnPropertyChanged("ScoreOkNum");
			}
		}
	}

	public int ScoreNgNum
	{
		get
		{
			return _scoreNgNum;
		}
		set
		{
			if (_scoreNgNum != value)
			{
				_scoreNgNum = value;
				OnPropertyChanged("ScoreNgNum");
			}
		}
	}

	public bool IsDisplayVerifyParam
	{
		get
		{
			return _IsDisplayVerifyParam;
		}
		set
		{
			if (_IsDisplayVerifyParam != value)
			{
				_IsDisplayVerifyParam = value;
				OnPropertyChanged("IsDisplayVerifyParam");
			}
		}
	}

	public int TextFontSize => TrainDataMgr.Instance.IsEnglish ? 12 : 14;

	public ObservableCollection<object> ShapeList => shapeList;

	public ROISelecter SelectedROI
	{
		get
		{
			return _selectedROI;
		}
		set
		{
			if (value != _selectedROI)
			{
				_selectedROI = value;
				OnPropertyChanged("SelectedROI");
			}
		}
	}

	public bool IsExportToModel
	{
		get
		{
			return _isExportToModel;
		}
		set
		{
			if (value != _isExportToModel)
			{
				_isExportToModel = value;
				OnPropertyChanged("IsExportToModel");
			}
		}
	}

	public ObservableCollection<SampleImageData> SampleImages
	{
		get
		{
			if (_sampleImages == null)
			{
				_sampleImages = new ObservableCollection<SampleImageData>();
			}
			return _sampleImages;
		}
		set
		{
			if (_sampleImages != value)
			{
				_sampleImages = value;
			}
		}
	}

	public List<string> MoreOptions => moreOptions;

	public List<string> ComboOptions => comboOptions;

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

	public BatchTrainPage()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!_IsInit)
			{
				_IsInit = true;
				TrainDataMgr.Instance.DeleteImageEvent += DeleteImageEvent;
				((ItemsControl)(object)paramTrainUI).ItemsSource = ModuleCommonMethodMgr.Instance.GetItemSource("Tab_Defect Model", "Tab_Train Params");
				((ItemsControl)(object)paramVerifyUI).ItemsSource = ModuleCommonMethodMgr.Instance.GetItemSource("Tab_Defect Model", "Tab_Verify Params");
				_bgNoiseType = ModuleCommonMethodMgr.Instance.GetParamItem(((ItemsControl)(object)paramTrainUI).ItemsSource, "BGNoiseRestrainType") as Enumeration;
				SelectedROI = ModuleCommonMethodMgr.Instance.GetParamItem(((ItemsControl)(object)paramTrainUI).ItemsSource, "BGNoiseRestrainRegion") as ROISelecter;
				if (_bgNoiseType != null)
				{
					if (SelectedROI != null)
					{
						SelectedROI.CustomVisibleA = _bgNoiseType.CurValueA == 1;
					}
					_bgNoiseType.OnCurValueChanged += BgNoiseType_ValueChanged;
				}
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

	private void MarkOKImage_OnClick(object sender, RoutedEventArgs e)
	{
		MarkImage(isOk: true);
	}

	private void MarkNGImage_OnClick(object sender, RoutedEventArgs e)
	{
		MarkImage(isOk: false);
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
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ClearImageWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult != VM.Controls.DialogResult.Yes && dialogResult != VM.Controls.DialogResult.SDKModel)
		{
			return;
		}
		ClearTrainData();
		foreach (ImageEntity image in ImageList)
		{
			if (!string.IsNullOrEmpty(image.ImagePath) && File.Exists(image.ImagePath))
			{
				File.Delete(image.ImagePath);
			}
		}
		ImageList.Clear();
		_CurIndex = -1;
		OnPropertyChanged("AllImageCount");
		OnPropertyChanged("DisplayImageList");
		OnPropertyChanged("CurIndex");
		string savePath = GetSavePath();
		if (Directory.Exists(savePath))
		{
			Directory.Delete(savePath, recursive: true);
		}
		ShapeList.Clear();
		CurImage = null;
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
		if (lst != null)
		{
			SelectedItemsCount = (lst.SelectedItems?.Count).Value;
			if (lst.SelectedIndex < 0)
			{
				SelectListViewIndex = 0;
			}
			else
			{
				SelectListViewIndex = lst.SelectedIndex;
				if (lst.SelectedIndex + 1 == lst.Items.Count && ImageList[lst.SelectedIndex].IsCalibrated)
				{
					SelectListViewIndex = lst.SelectedIndex + 1;
				}
			}
		}
		if (_isUpdateScrolOffset)
		{
			_CurHorizontalOffset = listScroll.HorizontalOffset;
			_isUpdateScrolOffset = false;
		}
	}

	private void lst_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (lst != null)
		{
			System.Windows.Controls.ListView listView = lst;
			if (listView != null && listView.SelectedIndex >= 0)
			{
				_CurIndex = lst.SelectedIndex;
				if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Create && TrainDataMgr.Instance.IsHasModel)
				{
					ModelMatch();
				}
				else if (TrainDataMgr.Instance.Owner.region.IsChecked == true)
				{
					GetBoxes();
				}
				UpdateDisplayInfo();
				OnPropertyChanged("CurIndex");
			}
		}
		if (listScroll != null && _isUpdateOffset)
		{
			_CurHorizontalOffset = listScroll.HorizontalOffset;
			if (!IsFullVisible())
			{
				_isUpdateScrolOffset = true;
			}
		}
	}

	private void Train_OnClick(object sender, RoutedEventArgs e)
	{
		if (TrainDataMgr.Instance.Param == null)
		{
			return;
		}
		if (!IsHasOkMark())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_TrainModelWarning"), MessageIcons.Warning);
		}
		else if (!_isTraining)
		{
			_isTraining = true;
			IsTrainFinish = false;
			try
			{
				((IUserStringData)TrainDataMgr.Instance.Param)["ClearInspModelData"] = "";
				_modelImage = null;
				TrainAsync();
			}
			catch (VmException ex)
			{
				string lang = AppLocalizationService.GetLang("errCode:");
				lang += $"{ex.errorCode:X}";
				lang += "\r\n";
				lang += AppLocalizationService.GetLang("errInfo:");
				lang += ex.errorMessage;
				HikMessageBox.Show(lang, MessageIcons.Error);
				_isTraining = false;
			}
		}
	}

	private void ModelVerify_OnClick(object sender, RoutedEventArgs e)
	{
		if (TrainDataMgr.Instance.Param == null)
		{
			return;
		}
		if (!IsTrainFinish)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ModelVerifyWarning"), MessageIcons.Warning);
		}
		else if (MarkNum == AllImageCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoVerifyImageWarning"), MessageIcons.Warning);
		}
		else
		{
			if (_isVerifing)
			{
				return;
			}
			_isVerifing = true;
			ScoreOkNum = 0;
			ScoreNgNum = 0;
			foreach (ImageEntity image in ImageList)
			{
				if (image.IsVerify)
				{
					image.IsVerify = false;
					image.IsOKVerify = false;
					image.ImageScore = 0;
				}
			}
			VerifyAsync();
		}
	}

	private void ImageListType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!(sender is System.Windows.Controls.ComboBox comboBox))
		{
			return;
		}
		if (comboBox.SelectedIndex == 0)
		{
			imageListType = ImageListType.All;
		}
		else if (comboBox.SelectedIndex == 1)
		{
			imageListType = ImageListType.OKImage;
		}
		else if (comboBox.SelectedIndex == 2)
		{
			imageListType = ImageListType.NGImage;
		}
		else if (comboBox.SelectedIndex == 3)
		{
			imageListType = ImageListType.UnregisterImage;
		}
		else if (comboBox.SelectedIndex == 4)
		{
			imageListType = ImageListType.VerifyImage;
		}
		OnPropertyChanged("DisplayImageList");
		SetSelectedIndex();
		OnPropertyChanged("CurIndex");
		if (listScroll != null)
		{
			if (_CurHorizontalOffset > 1.0)
			{
				listScroll.ScrollToHorizontalOffset(_CurHorizontalOffset - 1.0);
			}
			else
			{
				listScroll.ScrollToHorizontalOffset(_CurHorizontalOffset + 1.0);
			}
		}
	}

	private void front_Click(object sender, RoutedEventArgs e)
	{
		int selectedIndex = lst.SelectedIndex;
		if (selectedIndex > 0)
		{
			lst.SelectedIndex = selectedIndex - 1;
		}
	}

	private void next_Click(object sender, RoutedEventArgs e)
	{
		int selectedIndex = lst.SelectedIndex;
		if (selectedIndex < lst.Items.Count - 1)
		{
			lst.SelectedIndex = selectedIndex + 1;
		}
	}

	private void ClearTrainData_Click(object sender, RoutedEventArgs e)
	{
		if (!TrainDataMgr.Instance.IsStartTrain && !IsTrainFinish)
		{
			return;
		}
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ClearTrainDataWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult != VM.Controls.DialogResult.Yes && dialogResult != VM.Controls.DialogResult.SDKModel)
		{
			return;
		}
		int selectedIndex = lst.SelectedIndex;
		ClearTrainData();
		if (selectedIndex == 0)
		{
			if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Create && TrainDataMgr.Instance.IsHasModel)
			{
				ModelMatch();
			}
			else
			{
				GetBoxes();
			}
			UpdateDisplayInfo();
		}
	}

	private void DeleteImageEvent(object obj)
	{
		DeleteImage(obj);
	}

	private void DisplayOrHide_Click(object sender, RoutedEventArgs e)
	{
		if (IsDisplayVerifyParam)
		{
			IsDisplayVerifyParam = false;
		}
		else
		{
			IsDisplayVerifyParam = true;
		}
	}

	private void BgNoiseType_ValueChanged(object sender, EventArgs e)
	{
		if (SelectedROI != null)
		{
			SelectedROI.CustomVisibleA = _bgNoiseType.CurValueA == 1;
		}
	}

	private void FreshOptionsCommandExecute()
	{
		moreOptions = new List<string>();
		moreOptions.Add(AppLocalizationService.GetLang("Lang_CurImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_CacheImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportLocalImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportFolder"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_BatchImportOKImages"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_BatchImportNGImages"));
		OnPropertyChanged("MoreOptions");
	}

	private void ClickCommandExecute(object obj)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (obj is string text)
			{
				if (Enumerable.Count(_ImageList) >= 500)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 500, MessageButtons.Normal, MessageIcons.Warning);
				}
				else
				{
					bool flag = false;
					CreateFolder(GetSavePath());
					TrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
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
					}
					else if (text == AppLocalizationService.GetLang("Lang_ImportFolder"))
					{
						flag = LoadFolder();
					}
					else if (text == AppLocalizationService.GetLang("Lang_BatchImportOKImages"))
					{
						flag = LoadFolder(isCalibrate: true, isOk: true);
					}
					else if (text == AppLocalizationService.GetLang("Lang_BatchImportNGImages"))
					{
						flag = LoadFolder(isCalibrate: true);
					}
					if (flag && lst.SelectedIndex < 0)
					{
						lst.SelectedIndex = 0;
					}
					TrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
				}
			}
		}));
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		_imagePath = moduleTrainData.ImagePath;
		IsTrainFinish = moduleTrainData.IsTrainFinish;
		if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Inherit)
		{
			_modelImage = ((FeaturepatMatchDataBase)(moduleTrainData.FeatureMatchData?)).CurImageData;
		}
		IsExportToModel = moduleTrainData.IsExportToModel;
		SampleImages = (ObservableCollection<SampleImageData>)SerializationUnit.DeserializeObject(moduleTrainData._imagesData);
		if (SampleImages.Count > 0)
		{
			LoadSampleImagesAsync();
		}
		else
		{
			LoadImages();
		}
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Expected Obj, but got Unknown
		TrainDataMgr.Instance.DeleteImageEvent -= DeleteImageEvent;
		if (imageView != null)
		{
			imageView.DataContext = null;
		}
		if (_bgNoiseType != null)
		{
			_bgNoiseType.OnCurValueChanged -= BgNoiseType_ValueChanged;
			_bgNoiseType = null;
		}
		if (SelectedROI != null)
		{
			SelectedROI.CustomVisibleA = false;
			SelectedROI = null;
		}
		moduleTrainData.IsTrainFinish = IsTrainFinish;
		moduleTrainData.ImagePath = _imagePath;
		if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Inherit)
		{
			if (moduleTrainData.FeatureMatchData == null)
			{
				moduleTrainData.FeatureMatchData = new FeaturepatMatchDialogData();
			}
			((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData = _modelImage;
			try
			{
				byte[] array = null;
				array = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetDetectROIData"];
				moduleTrainData._matchBoxData = array;
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				string text2 = AppLocalizationService.GetLang("模板匹配错误") + "\r\n(";
				text2 += AppLocalizationService.GetLang("errInfo:");
				text2 += text;
				text2 += ")";
				LogHelper.moduleLog.Error($"GetDetectROIData error: {text2}");
			}
		}
		moduleTrainData.IsExportToModel = IsExportToModel;
		if (IsExportToModel)
		{
			SampleImages.Clear();
			foreach (ImageEntity image in ImageList)
			{
				SampleImageData sampleImageData = new SampleImageData();
				sampleImageData.ImageBuffer = File.ReadAllBytes(image.ImagePath);
				sampleImageData.ImageName = image.ImageName;
				sampleImageData.IsCalibrated = image.IsCalibrated;
				sampleImageData.IsOKCalibrated = image.IsOKCalibrated;
				SampleImages.Add(sampleImageData);
			}
			moduleTrainData._imagesData = SerializationUnit.SerializeObject(SampleImages);
		}
		((ItemsControl)(object)paramTrainUI).ItemsSource = null;
		((ItemsControl)(object)paramVerifyUI).ItemsSource = null;
		DataContext = null;
	}

	public void NeedUpdateDisplay()
	{
		if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Create && TrainDataMgr.Instance.IsHasModel)
		{
			ModelMatch();
		}
		else
		{
			GetBoxes();
		}
		UpdateDisplayInfo(bInit: true);
	}

	private bool IsFullVisible()
	{
		bool result = true;
		if (lst != null)
		{
			object selectedItem = lst.SelectedItem;
			ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
			if (val != null)
			{
				FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(val);
				result = IsFullVisibleInParent(child, listScroll);
			}
		}
		return result;
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

	private void DeleteImage(object obj)
	{
		try
		{
			int selectedIndex = lst.SelectedIndex;
			ImageEntity val = (ImageEntity)((obj is ImageEntity) ? obj : null);
			if (val != null)
			{
				_ImageList.Remove(val);
				if (!string.IsNullOrEmpty(val.ImagePath) && File.Exists(val.ImagePath))
				{
					File.Delete(val.ImagePath);
				}
				if (val.IsCalibrated)
				{
					if (val.IsOKCalibrated)
					{
						MarkOkNum--;
					}
					else
					{
						MarkNgNum--;
					}
				}
				if (val.IsVerify)
				{
					if (val.IsOKVerify)
					{
						ScoreOkNum--;
					}
					else
					{
						ScoreNgNum--;
					}
				}
				OnPropertyChanged("AllImageCount");
				OnPropertyChanged("DisplayImageList");
			}
			if (selectedIndex + 1 > lst.Items.Count)
			{
				_CurIndex = selectedIndex - 1;
			}
			else
			{
				_CurIndex = selectedIndex;
			}
			lst.SelectedIndex = _CurIndex;
			OnPropertyChanged("CurIndex");
			if (ImageList.Count == 0)
			{
				ShapeList.Clear();
				CurImage = null;
			}
		}
		catch
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_NoneChoose", MessageIcons.Error, isTranslate: true);
		}
	}

	private void AddImageByDir(string dir, bool isCalibrate = false, bool isOk = false)
	{
		try
		{
			if (Directory.Exists(dir))
			{
				AddImagesAsync(Directory.GetFileSystemEntries(dir).ToArray(), isCalibrate, isOk);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), ex.Message, MessageIcons.Error);
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
		if (child == null || parent == null)
		{
			return false;
		}
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
		if (child == null || parent == null)
		{
			return true;
		}
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
			result = ((!(rect3 != System.Windows.Rect.Empty)) ? rect.Width : (rect.Width - rect3.Width));
		}
		return result;
	}

	private bool LoadCurrentImage()
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected Obj, but got Unknown
		string newPath = GetNewImageName();
		if (newPath == null)
		{
			return false;
		}
		ImageData curImageData = TrainDataMgr.Instance.CurImageData;
		if (curImageData != null)
		{
			try
			{
				curImageData.SaveGrayImage(newPath);
				if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == newPath) == null)
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = newPath
					});
				}
				OnPropertyChanged("AllImageCount");
				OnPropertyChanged("DisplayImageList");
				return true;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
			}
		}
		else
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoCurrentImage"), MessageIcons.Warning);
		}
		return false;
	}

	private bool LoadCacheImage()
	{
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Expected Obj, but got Unknown
		bool flag = false;
		List<ImageData> cacheImageList = ModuleCommonMethodMgr.Instance.GetCacheImageList(TrainDataMgr.Instance.Module);
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
				catch
				{
				}
			}
		}
		if (flag)
		{
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
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
		string text = ConfigurationHelper.INIGetDefaultOpenPath(TrainDataMgr.Instance.Module?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpg;*.jpeg;*.png";
		if (openFileDialog.ShowDialog() == false)
		{
			return false;
		}
		ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(openFileDialog.FileName), TrainDataMgr.Instance.Module?.ModuleDir);
		AddImagesAsync(openFileDialog.FileNames);
		return true;
	}

	private bool LoadFolder(bool isCalibrate = false, bool isOk = false)
	{
		string text = "";
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		string text2 = ConfigurationHelper.INIGetDefaultOpenPath(TrainDataMgr.Instance.Module?.ModuleDir);
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
		AddImageByDir(text, isCalibrate, isOk);
		ConfigurationHelper.INISetDefaultOpenPath(text, TrainDataMgr.Instance.Module?.ModuleDir);
		return true;
	}

	private void LoadImages()
	{
		try
		{
			string savePath = GetSavePath();
			if (Directory.Exists(savePath))
			{
				AddImageByDir(savePath);
				if (lst.SelectedIndex < 0)
				{
					lst.SelectedIndex = 0;
				}
			}
		}
		catch
		{
		}
	}

	private async void LoadSampleImagesAsync()
	{
		await Task.Run(() =>
		{
			TrainDataMgr.Instance.Owner.ShowProgressDialog("正在加载图片");
			int count = SampleImages.Count;
			int num = 0;
			int num2 = 0;
			try
			{
				string savePath = GetSavePath();
				string text = "";
				CreateFolder(savePath);
				foreach (SampleImageData sampleImage in SampleImages)
				{
					text = $"{savePath}\\{sampleImage.ImageName}";
					if (!File.Exists(text))
					{
						File.WriteAllBytes(text, sampleImage.ImageBuffer);
					}
					AddImageToList(text, null, sampleImage.IsCalibrated, sampleImage.IsOKCalibrated);
					num++;
					num2 = 10 + (int)((float)num / (float)count * 90f);
					TrainDataMgr.Instance.Owner.SetProgressValue(num2);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				TrainDataMgr.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex3.Message, MessageIcons.Error);
					LogHelper.moduleLog.Error($"LoadSampleImagesAsync error: {ex3.Message}");
				});
			}
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
			TrainDataMgr.Instance.Owner.CloseProgressDialog();
			Dispatcher.Invoke(() =>
			{
				if (lst.SelectedIndex < 0)
				{
					lst.SelectedIndex = 0;
				}
			});
		});
	}

	private string GetNewImageName(string extension = ".bmp")
	{
		string result = null;
		if (TrainDataMgr.Instance.Module != null)
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

	private async void TrainAsync()
	{
		await Task.Run(() =>
		{
			TrainDataMgr.Instance.Owner.ShowProgressDialog("Lang_TrainRunning");
			try
			{
				string text = "";
				int markOkNum = MarkOkNum;
				List<byte> list = new List<byte>();
				List<byte> list2 = new List<byte>();
				byte[] bytes = BitConverter.GetBytes(markOkNum);
				list.AddRange(bytes);
				list2.AddRange(bytes);
				foreach (ImageEntity image in ImageList)
				{
					if (image.IsCalibrated && image.IsOKCalibrated)
					{
						int num = 0;
						IntPtr imagePtr = ModuleCommonMethodMgr.Instance.GetImagePtr(image.ImagePath, ref num);
						if (imagePtr == IntPtr.Zero)
						{
							throw new Exception(AppLocalizationService.GetLang("Lang_GetImageDataError"));
						}
						if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Inherit && _modelImage == null)
						{
							_modelImage = ModuleCommonMethodMgr.Instance.GetMono8ImageData(imagePtr);
						}
						byte[] array = new byte[num];
						Marshal.Copy(imagePtr, array, 0, num);
						list.AddRange(array);
						list2.AddRange(BitConverter.GetBytes(0));
						Marshal.FreeHGlobal(imagePtr);
					}
				}
				TrainDataMgr.Instance.Owner.SetProgressValue(30);
				byte[] value = list.ToArray();
				byte[] value2 = list2.ToArray();
				try
				{
					TrainDataMgr.Instance.Param.TimeOut = 2147483647u;
					((IUserBytesData)TrainDataMgr.Instance.Param)["SetImage"] = value;
					((IUserBytesData)TrainDataMgr.Instance.Param)["SetTrainLabel"] = value2;
					byte[] value3 = new byte[1];
					((IUserBytesData)TrainDataMgr.Instance.Param)["InspectTrain"] = value3;
					TrainDataMgr.Instance.Param.TimeOut = 3000u;
				}
				catch (Exception ex)
				{
					int result = 0;
					string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
					text = AppLocalizationService.GetLang("TrainFailed") + "\n(" + AppLocalizationService.GetLang("errInfo:") + text2 + ")";
					throw new Exception(text);
				}
				TrainDataMgr.Instance.Owner.SetProgressValue(50);
				list.Clear();
				list2.Clear();
				markOkNum = MarkNgNum;
				if (markOkNum > 0)
				{
					bytes = BitConverter.GetBytes(markOkNum);
					list.AddRange(bytes);
					list2.AddRange(bytes);
					foreach (ImageEntity image2 in ImageList)
					{
						if (image2.IsCalibrated && !image2.IsOKCalibrated)
						{
							int num2 = 0;
							IntPtr imagePtr2 = ModuleCommonMethodMgr.Instance.GetImagePtr(image2.ImagePath, ref num2);
							if (imagePtr2 == IntPtr.Zero)
							{
								throw new Exception(AppLocalizationService.GetLang("Lang_GetImageDataError"));
							}
							byte[] array2 = new byte[num2];
							Marshal.Copy(imagePtr2, array2, 0, num2);
							list.AddRange(array2);
							list2.AddRange(BitConverter.GetBytes(1));
							Marshal.FreeHGlobal(imagePtr2);
						}
					}
					TrainDataMgr.Instance.Owner.SetProgressValue(70);
					value = list.ToArray();
					value2 = list2.ToArray();
					try
					{
						TrainDataMgr.Instance.Param.TimeOut = 2147483647u;
						((IUserBytesData)TrainDataMgr.Instance.Param)["SetImage"] = value;
						((IUserBytesData)TrainDataMgr.Instance.Param)["SetTrainLabel"] = value2;
						byte[] value4 = new byte[1];
						((IUserBytesData)TrainDataMgr.Instance.Param)["InspectTrain"] = value4;
						TrainDataMgr.Instance.Param.TimeOut = 3000u;
					}
					catch (Exception ex2)
					{
						int result2 = 0;
						string text3 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
						text = AppLocalizationService.GetLang("TrainFailed") + "\n(" + AppLocalizationService.GetLang("errInfo:") + text3 + ")";
						throw new Exception(text);
					}
					TrainDataMgr.Instance.Owner.SetProgressValue(90);
				}
				Dispatcher.Invoke(() =>
				{
					IsTrainFinish = true;
				});
			}
			catch (Exception ex3)
			{
				Exception ex4 = ex3;
				Exception ex5 = ex4;
				TrainDataMgr.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex5.Message, MessageIcons.Error);
				});
				_isTraining = false;
				return;
			}
			TrainDataMgr.Instance.Owner.CloseProgressDialog();
			Dispatcher.Invoke(() =>
			{
				if (IsTrainFinish)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("训练完成"), MessageIcons.Ok);
				}
			});
			_isTraining = false;
		});
	}

	private bool IsHasOkMark()
	{
		foreach (ImageEntity image in ImageList)
		{
			if (image.IsCalibrated && image.IsOKCalibrated)
			{
				return true;
			}
		}
		return false;
	}

	private async void VerifyAsync()
	{
		await Task.Run(() =>
		{
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02be: Unknown result type (might be due to invalid IL or missing references)
			TrainDataMgr.Instance.Owner.ShowProgressDialog("Lang_ModelVerifing");
			try
			{
				string strErrorMsg = "";
				int num = ImageList.Count - MarkNum;
				int num2 = 0;
				int num3 = 0;
				foreach (ImageEntity image in ImageList)
				{
					if (!image.IsCalibrated)
					{
						int num4 = 0;
						IntPtr imagePtr = ModuleCommonMethodMgr.Instance.GetImagePtr(image.ImagePath, ref num4);
						if (imagePtr == IntPtr.Zero)
						{
							throw new Exception(AppLocalizationService.GetLang("Lang_GetImageDataError"));
						}
						List<byte> list = new List<byte>();
						byte[] bytes = BitConverter.GetBytes(1);
						list.AddRange(bytes);
						byte[] array = new byte[num4];
						Marshal.Copy(imagePtr, array, 0, num4);
						list.AddRange(array);
						Marshal.FreeHGlobal(imagePtr);
						byte[] value = list.ToArray();
						try
						{
							TrainDataMgr.Instance.Param.TimeOut = 2147483647u;
							((IUserBytesData)TrainDataMgr.Instance.Param)["SetImage"] = value;
							byte[] value2 = new byte[1];
							((IUserBytesData)TrainDataMgr.Instance.Param)["VerifyModel"] = value2;
							TrainDataMgr.Instance.Param.TimeOut = 3000u;
							byte[] array2 = null;
							array2 = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetSocreList"];
							if (array2 != null && array2.Length != 0)
							{
								float[] scores = GetScores(array2);
								image.ImageScore = (int)(scores[0] * 100f);
								if (image.ImageScore >= (int)OKStandard)
								{
									image.IsOKVerify = true;
									ScoreOkNum++;
								}
								else
								{
									image.IsOKVerify = false;
									ScoreNgNum++;
								}
								image.IsVerify = true;
							}
							array2 = null;
							array2 = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetDetectROIData"];
							if (array2 != null && array2.Length != 0)
							{
								image.Boxes = MVBI_BOX_F.GetBoxesWithNum(array2);
							}
							if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Create && TrainDataMgr.Instance.IsHasModel)
							{
								array2 = null;
								array2 = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetMatchBoxData"];
								if (array2 != null && array2.Length != 0)
								{
									IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
									image.MatchBox = default;
									image.MatchBox = (MVBI_BOX_F)Marshal.PtrToStructure(ptr, typeof(MVBI_BOX_F));
								}
							}
						}
						catch (Exception ex)
						{
							int result = 0;
							string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
							strErrorMsg = strErrorMsg + "\n" + image.ImageName + " : (" + AppLocalizationService.GetLang("errInfo:") + text + ")";
						}
						num2++;
						num3 = 10 + (int)((float)num2 / (float)num * 90f);
						TrainDataMgr.Instance.Owner.SetProgressValue(num3);
					}
				}
				if (!string.IsNullOrEmpty(strErrorMsg))
				{
					TrainDataMgr.Instance.Owner.CloseProgressDialog();
					Dispatcher.Invoke(() =>
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_VerifyFailImageList") + strErrorMsg, MessageIcons.Error);
					});
				}
			}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				Exception ex4 = ex3;
				TrainDataMgr.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex4.Message, MessageIcons.Error);
				});
			}
			Dispatcher.Invoke(() =>
			{
				if (m_ImageType.SelectedIndex == 4)
				{
					OnPropertyChanged("DisplayImageList");
					SetSelectedIndex();
					OnPropertyChanged("CurIndex");
					UpdateDisplayInfo();
				}
				else if (ScoreOkNum + ScoreNgNum > 0)
				{
					m_ImageType.SelectedIndex = 4;
					UpdateDisplayInfo();
				}
			});
			TrainDataMgr.Instance.Owner.CloseProgressDialog();
			_isVerifing = false;
		});
	}

	private unsafe float[] GetScores(byte[] libData)
	{
		if (libData != null && libData.Length != 0)
		{
			byte* ptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(libData, 0).ToPointer();
			int num = 0;
			int num2 = *(int*)(ptr + num);
			num += 4;
			int num3 = num2 * Marshal.SizeOf(typeof(float));
			float[] array = new float[num2];
			fixed (float* ptr2 = array)
			{
				byte* ptr3 = (byte*)ptr2;
				for (int i = 0; i < num3; i++)
				{
					ptr3[i] = libData[num + i];
				}
			}
			return array;
		}
		return null;
	}

	public void ClearTrainData()
	{
		try
		{
			byte[] value = new byte[1];
			((IUserBytesData)TrainDataMgr.Instance.Param)["ClearInspModelData"] = value;
			foreach (ImageEntity image in ImageList)
			{
				image.IsCalibrated = false;
				image.IsOKCalibrated = false;
				image.Boxes = null;
				image.IsVerify = false;
				image.IsOKVerify = false;
				image.ImageScore = 0;
			}
			MarkOkNum = 0;
			MarkNgNum = 0;
			ScoreOkNum = 0;
			ScoreNgNum = 0;
			OKStandard = 50.0;
			m_ImageType.SelectedIndex = 0;
			listScroll.ScrollToLeftEnd();
			lst.SelectedIndex = 0;
			_CurHorizontalOffset = 0.0;
			TrainDataMgr.Instance.IsStartTrain = false;
			IsTrainFinish = false;
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("Lang_ClearTrainDataError") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			HikMessageBox.Show(text2, MessageIcons.Error);
		}
	}

	private void SetSelectedIndex()
	{
		if (lst != null)
		{
			if (DisplayImageCount == 0)
			{
				_CurIndex = -1;
			}
			else if (_CurIndex < 0 || _CurIndex >= DisplayImageCount)
			{
				_CurIndex = 0;
			}
			lst.SelectedIndex = _CurIndex;
		}
	}

	private void MarkImage(bool isOk)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
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
		if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Create && TrainDataMgr.Instance.IsHasModel)
		{
			if (val.Boxes == null || val.Boxes.Length == 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MatchErrorRegisterWarn"), MessageIcons.Warning);
				return;
			}
			if (TrainDataMgr.Instance.Owner.pageTrainRegion.RectEnable && ModuleCommonMethodMgr.Instance.IsOverstep(CurImage, val.Boxes[0]))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ComparisonAreaRegisterWarn"), MessageIcons.Warning);
				return;
			}
		}
		bool flag = false;
		if (!TrainDataMgr.Instance.IsStartTrain)
		{
			TrainDataMgr.Instance.IsStartTrain = true;
		}
		if (val.IsCalibrated)
		{
			if ((isOk && val.IsOKCalibrated) || (!isOk && !val.IsOKCalibrated))
			{
				return;
			}
			if (isOk)
			{
				if (MarkOkNum >= 20)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MarkOKImageWarning"), MessageIcons.Warning);
					return;
				}
				val.IsOKCalibrated = true;
				MarkOkNum++;
				MarkNgNum--;
				if (imageListType == ImageListType.NGImage)
				{
					flag = true;
				}
			}
			else
			{
				if (MarkNgNum >= 20)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MarkNGImageWarning"), MessageIcons.Warning);
					return;
				}
				val.IsOKCalibrated = false;
				MarkOkNum--;
				MarkNgNum++;
				if (imageListType == ImageListType.OKImage)
				{
					flag = true;
				}
			}
		}
		else
		{
			if (isOk)
			{
				if (MarkOkNum >= 20)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MarkOKImageWarning"), MessageIcons.Warning);
					return;
				}
				val.IsOKCalibrated = true;
				MarkOkNum++;
			}
			else
			{
				if (MarkNgNum >= 20)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MarkNGImageWarning"), MessageIcons.Warning);
					return;
				}
				val.IsOKCalibrated = false;
				MarkNgNum++;
			}
			val.IsCalibrated = true;
			if (val.IsVerify)
			{
				val.IsVerify = false;
				if (val.IsOKVerify)
				{
					ScoreOkNum--;
				}
				else
				{
					ScoreNgNum--;
				}
				val.IsOKVerify = false;
			}
			if (imageListType == ImageListType.UnregisterImage || imageListType == ImageListType.VerifyImage)
			{
				flag = true;
			}
		}
		_isUpdateOffset = false;
		if (lst.SelectedIndex + 1 < lst.Items.Count)
		{
			lst.SelectedIndex++;
			if (lst.SelectedIndex > 1)
			{
				ScrollImageList();
			}
		}
		if (flag)
		{
			int num = _CurIndex;
			OnPropertyChanged("DisplayImageList");
			if (num + 1 > DisplayImageCount)
			{
				num--;
			}
			int curIndex = (lst.SelectedIndex = num);
			_CurIndex = curIndex;
			OnPropertyChanged("CurIndex");
		}
		_isUpdateOffset = true;
	}

	private void UpdateDisplayInfo(bool bInit = false)
	{
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		CurImage = null;
		ShapeList.Clear();
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val == null)
		{
			return;
		}
		int num = 0;
		IntPtr imagePtr = ModuleCommonMethodMgr.Instance.GetImagePtr(val.ImagePath, ref num);
		if (imagePtr == IntPtr.Zero)
		{
			DeleteImage(lst.SelectedItem);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetImageDataError"), MessageIcons.Error);
			return;
		}
		ImageData mono8ImageData = ModuleCommonMethodMgr.Instance.GetMono8ImageData(imagePtr);
		if (mono8ImageData != null)
		{
			val.ImageWidth = mono8ImageData.Width;
			val.ImageHeight = mono8ImageData.Height;
			CurImage = mono8ImageData;
			if (val.Boxes != null && val.Boxes.Length != 0)
			{
				for (int i = 0; i < val.Boxes.Length; i++)
				{
					MVBI_BOX_F val2 = val.Boxes[i];
					Apps.Data.ShapeDefine.Rect rect = new Apps.Data.ShapeDefine.Rect();
					rect.Width = val2.width;
					rect.Height = val2.height;
					rect.Angle = val2.angle;
					rect.CenterX = val2.center_x;
					rect.CenterY = val2.center_y;
					rect.Color = "#0F6AFC";
					rect.Opacity = 1.0;
					rect.StrokeThickness = 1.0;
					ShapeList.Add(rect);
				}
			}
			if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Create && TrainDataMgr.Instance.IsHasModel)
			{
				DrawMatchBox(val.MatchBox);
			}
			if (val.IsVerify)
			{
				string text = AppLocalizationService.GetLang("Lang_OKImageSimilarity");
				if (TrainDataMgr.Instance.IsEnglish)
				{
					text += " ";
				}
				text = text + val.ImageScore + "%";
				Text text2 = new Text();
				text2.PositionX = 6f;
				text2.PositionY = 6f;
				text2.Content = text;
				text2.FontSize = 14;
				text2.Opacity = 1.0;
				text2.StrokeThickness = 1.0;
				if (val.IsOKVerify)
				{
					text2.Color = "#FF00d53c";
				}
				else
				{
					text2.Color = "#FFCA0831";
				}
				List<object> list = new List<object>();
				list.Add(text2);
				object item = DrawShapeHelper.PretreatTexts(list);
				ShapeList.Add(item);
			}
		}
		Marshal.FreeHGlobal(imagePtr);
	}

	private void DrawMatchBox(MVBI_BOX_F box)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Apps.Data.ShapeDefine.Rect rect = new Apps.Data.ShapeDefine.Rect();
			rect.Width = box.width;
			rect.Height = box.height;
			rect.Angle = box.angle;
			rect.CenterX = box.center_x;
			rect.CenterY = box.center_y;
			rect.Color = "#F76900";
			rect.Opacity = 1.0;
			rect.StrokeThickness = 1.0;
			ShapeList.Add(rect);
		}
		catch
		{
		}
	}

	private void ModelMatch()
	{
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val == null || ((val.IsCalibrated || val.IsVerify) && val.Boxes != null))
		{
			return;
		}
		int num = 0;
		IntPtr imagePtr = ModuleCommonMethodMgr.Instance.GetImagePtr(val.ImagePath, ref num);
		if (imagePtr == IntPtr.Zero)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetImageDataError"), MessageIcons.Error);
			return;
		}
		List<byte> list = new List<byte>();
		byte[] bytes = BitConverter.GetBytes(1);
		list.AddRange(bytes);
		byte[] array = new byte[num];
		Marshal.Copy(imagePtr, array, 0, num);
		list.AddRange(array);
		Marshal.FreeHGlobal(imagePtr);
		try
		{
			TrainDataMgr.Instance.Param.TimeOut = 2147483647u;
			byte[] value = list.ToArray();
			((IUserBytesData)TrainDataMgr.Instance.Param)["SetImage"] = value;
			byte[] value2 = new byte[1];
			((IUserBytesData)TrainDataMgr.Instance.Param)["MapingROI"] = value2;
			TrainDataMgr.Instance.Param.TimeOut = 3000u;
			byte[] array2 = null;
			array2 = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetDetectROIData"];
			if (array2 != null && array2.Length != 0)
			{
				val.Boxes = MVBI_BOX_F.GetBoxesWithNum(array2);
			}
			array2 = null;
			array2 = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetMatchBoxData"];
			if (array2 != null && array2.Length != 0)
			{
				IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(array2, 0);
				val.MatchBox = default;
				val.MatchBox = (MVBI_BOX_F)Marshal.PtrToStructure(ptr, typeof(MVBI_BOX_F));
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("模板匹配错误") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), text2, MessageIcons.Error);
		}
	}

	private void GetBoxes()
	{
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val == null || ((val.IsCalibrated || val.IsVerify) && val.Boxes != null))
		{
			return;
		}
		try
		{
			byte[] array = null;
			array = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetDetectROIData"];
			if (array != null && array.Length != 0)
			{
				val.Boxes = MVBI_BOX_F.GetBoxesWithNum(array);
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("获取输入ROI失败") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			LogHelper.moduleLog.Error($"GetBoxes error, {text2}");
		}
	}

	private bool IsCanMark(string path)
	{
		if (TrainDataMgr.Instance.Owner.CurModelType == ModelType.Inherit)
		{
			return true;
		}
		if (File.Exists(path))
		{
			int num = 0;
			IntPtr imagePtr = ModuleCommonMethodMgr.Instance.GetImagePtr(path, ref num);
			if (imagePtr == IntPtr.Zero)
			{
				return false;
			}
			ImageData imageData = ModuleCommonMethodMgr.Instance.GetMono8ImageData(imagePtr);
			List<byte> list = new List<byte>();
			byte[] bytes = BitConverter.GetBytes(1);
			list.AddRange(bytes);
			byte[] array = new byte[num];
			Marshal.Copy(imagePtr, array, 0, num);
			list.AddRange(array);
			Marshal.FreeHGlobal(imagePtr);
			try
			{
				byte[] value = list.ToArray();
				((IUserBytesData)TrainDataMgr.Instance.Param)["SetImage"] = value;
				byte[] value2 = new byte[1];
				((IUserBytesData)TrainDataMgr.Instance.Param)["MapingROI"] = value2;
				byte[] array2 = null;
				array2 = ((IUserBytesData)TrainDataMgr.Instance.Param)["GetDetectROIData"];
				if (array2 != null && array2.Length != 0)
				{
					MVBI_BOX_F[] boxes = MVBI_BOX_F.GetBoxesWithNum(array2);
					if (boxes != null)
					{
						bool isCanMark = true;
						Dispatcher.Invoke(() =>
						{
							//IL_0038: Unknown result type (might be due to invalid IL or missing references)
							if (TrainDataMgr.Instance.Owner.pageTrainRegion.RectEnable && ModuleCommonMethodMgr.Instance.IsOverstep(imageData, boxes[0]))
							{
								isCanMark = false;
							}
						});
						return isCanMark;
					}
				}
			}
			catch
			{
			}
		}
		return false;
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

	private async void AddImagesAsync(string[] fileNames, bool isCalibrate = false, bool isOk = false)
	{
		await Task.Run(() =>
		{
			TrainDataMgr.Instance.Owner.ShowProgressDialog("正在加载图片");
			int num = fileNames.Length;
			int num2 = 0;
			int num3 = 0;
			try
			{
				string[] array = fileNames;
				foreach (string text in array)
				{
					if (ImageList.Count >= 500)
					{
						throw new Exception(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 500);
					}
					if (isCalibrate)
					{
						if (isOk)
						{
							if (MarkOkNum >= 20)
							{
								throw new Exception(AppLocalizationService.GetLang("Lang_MarkOKImageWarning"));
							}
						}
						else if (MarkNgNum >= 20)
						{
							throw new Exception(AppLocalizationService.GetLang("Lang_MarkNGImageWarning"));
						}
					}
					try
					{
						if (Directory.Exists(text))
						{
							DoAddImages(Directory.GetFileSystemEntries(text).ToArray(), isCalibrate, isOk);
							goto IL_01fa;
						}
						string text2 = System.IO.Path.GetExtension(text).ToUpper();
						if (!(text2 != ".JPG") || !(text2 != ".JPEG") || !(text2 != ".BMP") || !(text2 != ".PNG"))
						{
							DoAddImage(text, isCalibrate, isOk);
							goto IL_01fa;
						}
					}
					catch (Exception ex)
					{
						Exception ex2 = ex;
						Exception ex3 = ex2;
						TrainDataMgr.Instance.Owner.CloseProgressDialog();
						Dispatcher.Invoke(() =>
						{
							HikMessageBox.Show(ex3.Message, MessageIcons.Error);
						});
						break;
					}
					continue;
					IL_01fa:
					num2++;
					num3 = 10 + (int)((float)num2 / (float)num * 90f);
					TrainDataMgr.Instance.Owner.SetProgressValue(num3);
				}
			}
			catch (Exception ex4)
			{
				Exception ex2 = ex4;
				Exception ex5 = ex2;
				TrainDataMgr.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex5.Message, MessageIcons.Warning);
				});
			}
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
			TrainDataMgr.Instance.Owner.CloseProgressDialog();
		});
	}

	private void DoAddImages(string[] fileNames, bool isCalibrate = false, bool isOk = false)
	{
		foreach (string text in fileNames)
		{
			if (ImageList.Count >= 500)
			{
				break;
			}
			if (isCalibrate)
			{
				if (isOk)
				{
					if (MarkOkNum >= 20)
					{
						break;
					}
				}
				else if (MarkNgNum >= 20)
				{
					break;
				}
			}
			if (Directory.Exists(text))
			{
				DoAddImages(Directory.GetFileSystemEntries(text).ToArray(), isCalibrate, isOk);
				continue;
			}
			string text2 = System.IO.Path.GetExtension(text).ToUpper();
			if (!(text2 != ".JPG") || !(text2 != ".JPEG") || !(text2 != ".BMP") || !(text2 != ".PNG"))
			{
				DoAddImage(text, isCalibrate, isOk);
			}
		}
	}

	private string DoAddImage(string name, bool isCalibrate = false, bool isOk = false)
	{
		if (name.Length >= 260)
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_FilePathLong"));
		}
		string text = null;
		if (System.IO.Path.GetDirectoryName(name) != GetSavePath())
		{
			string extension = System.IO.Path.GetExtension(name);
			text = GetNewImageName(extension);
		}
		if (isCalibrate && !IsCanMark(name))
		{
			return null;
		}
		AddImageToList(name, text, isCalibrate, isOk);
		return text;
	}

	private void AddImageToList(string strImagePath, string newPath = null, bool isCalibrate = false, bool isOk = false)
	{
		if (strImagePath.Length > 0 && strImagePath[0] == '\\')
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_LocalImageNeed"));
		}
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
						strImagePath = newPath;
						bHasImageData = true;
					}
				});
				task.Start();
				Task.WaitAll(task);
			}
			else
			{
				bHasImageData = true;
			}
			if (!bHasImageData || _ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == strImagePath) != null)
			{
				return;
			}
			Dispatcher.Invoke(() =>
			{
				//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b8: Expected Obj, but got Unknown
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				//IL_001f: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				//IL_004b: Expected Obj, but got Unknown
				if (isCalibrate)
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = strImagePath,
						IsCalibrated = isCalibrate,
						IsOKCalibrated = isOk
					});
					if (isOk)
					{
						int markOkNum = MarkOkNum;
						MarkOkNum = markOkNum + 1;
					}
					else
					{
						int markOkNum = MarkNgNum;
						MarkNgNum = markOkNum + 1;
					}
				}
				else
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = strImagePath
					});
				}
			});
		}
		catch (Exception ex)
		{
			throw ex;
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
