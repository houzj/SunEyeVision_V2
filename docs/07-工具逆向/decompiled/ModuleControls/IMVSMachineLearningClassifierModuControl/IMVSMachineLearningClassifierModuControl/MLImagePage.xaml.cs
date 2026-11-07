using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.Data.Common;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using HikPcUI.ImageView;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;

namespace IMVSMachineLearningClassifierModuControl;

public partial class MLImagePage : System.Windows.Controls.UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private ObservableCollection<ImageEntity> _ImageList = new ObservableCollection<ImageEntity>();

	private int _FreshListCount = 0;

	private int _SelectedItemsCount;

	private int _SelectListViewIndex = 0;

	private int _CurIndex = -1;

	private double _CurHorizontalOffset = 0.0;

	private int _markNum = 0;

	private ImageListType imageListType = ImageListType.All;

	private int _displayImageCount = 0;

	private bool _isEnable = false;

	private string _imagePath = "";

	private bool _isRegistering = false;

	private bool _isUpdateOffset = true;

	private ObservableCollection<CategoryData> _CategoryList = new ObservableCollection<CategoryData>();

	private bool _selectbox = false;

	private string _CategoryWord = "";

	private int _CategorySortIndex = 0;

	private bool _init = true;

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(MLImagePage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty RectEnableProperty = DependencyProperty.Register("RectEnable", typeof(bool), typeof(MLImagePage), new PropertyMetadata(false));

	public static readonly DependencyProperty IsDrawRectProperty = DependencyProperty.Register("IsDrawRect", typeof(bool), typeof(MLImagePage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectRoisProperty = DependencyProperty.Register("RectRois", typeof(ObservableCollection<object>), typeof(MLImagePage), new PropertyMetadata(new ObservableCollection<object>()));

	public static readonly DependencyProperty SelectedBoxProperty = DependencyProperty.Register("SelectedBox", typeof(HikCoverRoiParameter), typeof(MLImagePage), new PropertyMetadata((object)null));

	public static readonly DependencyProperty SelectedCategoryProperty = DependencyProperty.Register("SelectedCategory", typeof(CategoryData), typeof(MLImagePage), new PropertyMetadata((object)null));

	private static List<string> moreOptions = new List<string> { "当前图像", "缓存图像", "导入本地图像", "导入文件夹", "按类别导入图像" };

	private List<string> comboOptions = new List<string>
	{
		AppLocalizationService.GetLang("All"),
		AppLocalizationService.GetLang("Lang_MarkedImages"),
		AppLocalizationService.GetLang("Lang_UnmarkedImages")
	};

	private List<string> sortOptions = new List<string>
	{
		AppLocalizationService.GetLang("None"),
		AppLocalizationService.GetLang("Lang_CategoryNameA-Z"),
		AppLocalizationService.GetLang("Lang_CategoryNameZ-A"),
		AppLocalizationService.GetLang("Lang_ImageNumberLargeSmall"),
		AppLocalizationService.GetLang("Lang_ImageNumberSmallLarge")
	};

	private DelegateCommand freshOptionsCommand = null;

	private DelegateCommand<object> clickCommand = null;

	public bool _TextureFeatureValue;

	public bool _HogFeatureValue;

	public bool _LbpFeatureValue;

	public bool _GlcmFeatureValue;

	public bool _EdgeFeatureValue;

	public bool _PolarFeatureValue;

	public bool _GrayFeatureValue;

	public bool _GrayRangeFeatureValue;

	public bool _GrayMeanFeatureValue;

	public bool _GrayDeviationFeatureValue;

	public bool _GrayEntropyFeatureValue;

	public bool _GrayAnisotropyFeatureValue;

	public bool _GrayProjHorFeatureValue;

	public bool _GrayProjVertFeatureValue;

	public bool _GrayHisValue;

	public int _SvmMaxIterValue;

	public int _SvmEspValue;

	public int _ClassifyTypeValue;

	private bool lstSelectionChangedDisable = false;

	private bool lstDeleteImage = false;

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

	public ObservableCollection<CategoryData> CategoryList => _CategoryList;

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
			case ImageListType.MarkImage:
				foreach (ImageEntity image in ImageList)
				{
					if (!string.IsNullOrWhiteSpace(image.ImageLabel))
					{
						observableCollection.Add(image);
					}
				}
				break;
			case ImageListType.UnMarkImage:
				foreach (ImageEntity image2 in ImageList)
				{
					if (string.IsNullOrWhiteSpace(image2.ImageLabel))
					{
						observableCollection.Add(image2);
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

	public double PageLeftWidth => MLClassifyMgr.Instance.IsEnglish ? 600 : 690;

	public double ParamTextWidth => MLClassifyMgr.Instance.IsEnglish ? 120 : 80;

	public double SearchBorderWidth => MLClassifyMgr.Instance.IsEnglish ? 200 : 185;

	public double SearchTextWidth => MLClassifyMgr.Instance.IsEnglish ? 170 : 155;

	public int TagFontSize => MLClassifyMgr.Instance.IsEnglish ? 10 : 12;

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

	public CategoryData SelectedCategory
	{
		get
		{
			return (CategoryData)GetValue(SelectedCategoryProperty);
		}
		set
		{
			SetValue(SelectedCategoryProperty, value);
		}
	}

	public string CategoryWord
	{
		get
		{
			return _CategoryWord;
		}
		set
		{
			if (_CategoryWord != value)
			{
				_CategoryWord = value;
				OnPropertyChanged("CategoryWord");
				OnPropertyChanged("DisplayCategoryList");
			}
		}
	}

	public int CategorySortIndex
	{
		get
		{
			return _CategorySortIndex;
		}
		set
		{
			if (_CategorySortIndex != value)
			{
				_CategorySortIndex = value;
				OnPropertyChanged("CategorySortIndex");
				OnPropertyChanged("DisplayCategoryList");
			}
		}
	}

	public IList<CategoryData> DisplayCategoryList
	{
		get
		{
			IList<CategoryData> list = ((!string.IsNullOrWhiteSpace(_CategoryWord)) ? ((IList<CategoryData>)CategoryList.Where((CategoryData p) => p.CategoryName.Contains(_CategoryWord)).ToList()) : ((IList<CategoryData>)CategoryList));
			if (CategorySortIndex == 1)
			{
				return list.OrderBy((CategoryData p) => p.CategoryName).ToList();
			}
			if (CategorySortIndex == 2)
			{
				return list.OrderByDescending((CategoryData p) => p.CategoryName).ToList();
			}
			if (CategorySortIndex == 3)
			{
				return list.OrderByDescending((CategoryData p) => p.RegisteredNum).ToList();
			}
			if (CategorySortIndex == 4)
			{
				return list.OrderBy((CategoryData p) => p.RegisteredNum).ToList();
			}
			return list;
		}
	}

	public List<string> MoreOptions => moreOptions;

	public List<string> ComboOptions => comboOptions;

	public List<string> SortOptions => sortOptions;

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

	public bool TextureFeatureValue
	{
		get
		{
			return _TextureFeatureValue;
		}
		set
		{
			if (value != _TextureFeatureValue)
			{
				_TextureFeatureValue = value;
				if (CheckFeature())
				{
					OnPropertyChanged("TextureFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["TextureFeature"] = _TextureFeatureValue.ToString();
				}
				else
				{
					_TextureFeatureValue = !value;
				}
			}
		}
	}

	public bool HogFeatureValue
	{
		get
		{
			return _HogFeatureValue;
		}
		set
		{
			if (value != _HogFeatureValue)
			{
				_HogFeatureValue = value;
				if (CheckTextureFeatureItem())
				{
					OnPropertyChanged("HogFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["HogFeature"] = _HogFeatureValue.ToString();
				}
				else
				{
					_HogFeatureValue = !value;
				}
			}
		}
	}

	public bool LbpFeatureValue
	{
		get
		{
			return _LbpFeatureValue;
		}
		set
		{
			if (value != _LbpFeatureValue)
			{
				_LbpFeatureValue = value;
				if (CheckTextureFeatureItem())
				{
					OnPropertyChanged("LbpFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["LbpFeature"] = _LbpFeatureValue.ToString();
				}
				else
				{
					_LbpFeatureValue = !value;
				}
			}
		}
	}

	public bool GlcmFeatureValue
	{
		get
		{
			return _GlcmFeatureValue;
		}
		set
		{
			if (value != _GlcmFeatureValue)
			{
				_GlcmFeatureValue = value;
				if (CheckTextureFeatureItem())
				{
					OnPropertyChanged("GlcmFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GlcmFeature"] = _GlcmFeatureValue.ToString();
				}
				else
				{
					_GlcmFeatureValue = !value;
				}
			}
		}
	}

	public bool EdgeFeatureValue
	{
		get
		{
			return _EdgeFeatureValue;
		}
		set
		{
			if (value != _EdgeFeatureValue)
			{
				_EdgeFeatureValue = value;
				if (CheckTextureFeatureItem())
				{
					OnPropertyChanged("EdgeFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["EdgeFeature"] = _EdgeFeatureValue.ToString();
				}
				else
				{
					_EdgeFeatureValue = !value;
				}
			}
		}
	}

	public bool PolarFeatureValue
	{
		get
		{
			return _PolarFeatureValue;
		}
		set
		{
			if (value != _PolarFeatureValue)
			{
				_PolarFeatureValue = value;
				if (CheckTextureFeatureItem())
				{
					OnPropertyChanged("PolarFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["PolarFeature"] = _PolarFeatureValue.ToString();
				}
				else
				{
					_PolarFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayFeatureValue
	{
		get
		{
			return _GrayFeatureValue;
		}
		set
		{
			if (value != _GrayFeatureValue)
			{
				_GrayFeatureValue = value;
				if (CheckFeature())
				{
					OnPropertyChanged("GrayFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayFeature"] = _GrayFeatureValue.ToString();
				}
				else
				{
					_GrayFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayRangeFeatureValue
	{
		get
		{
			return _GrayRangeFeatureValue;
		}
		set
		{
			if (value != _GrayRangeFeatureValue)
			{
				_GrayRangeFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayRangeFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayRangeFeature"] = _GrayRangeFeatureValue.ToString();
				}
				else
				{
					_GrayRangeFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayMeanFeatureValue
	{
		get
		{
			return _GrayMeanFeatureValue;
		}
		set
		{
			if (value != _GrayMeanFeatureValue)
			{
				_GrayMeanFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayMeanFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayMeanFeature"] = _GrayMeanFeatureValue.ToString();
				}
				else
				{
					_GrayMeanFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayDeviationFeatureValue
	{
		get
		{
			return _GrayDeviationFeatureValue;
		}
		set
		{
			if (value != _GrayDeviationFeatureValue)
			{
				_GrayDeviationFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayDeviationFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayDeviationFeature"] = _GrayDeviationFeatureValue.ToString();
				}
				else
				{
					_GrayDeviationFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayEntropyFeatureValue
	{
		get
		{
			return _GrayEntropyFeatureValue;
		}
		set
		{
			if (value != _GrayEntropyFeatureValue)
			{
				_GrayEntropyFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayEntropyFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayEntropyFeature"] = _GrayEntropyFeatureValue.ToString();
				}
				else
				{
					_GrayEntropyFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayAnisotropyFeatureValue
	{
		get
		{
			return _GrayAnisotropyFeatureValue;
		}
		set
		{
			if (value != _GrayAnisotropyFeatureValue)
			{
				_GrayAnisotropyFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayAnisotropyFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayAnisotropyFeature"] = _GrayAnisotropyFeatureValue.ToString();
				}
				else
				{
					_GrayAnisotropyFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayProjHorFeatureValue
	{
		get
		{
			return _GrayProjHorFeatureValue;
		}
		set
		{
			if (value != _GrayProjHorFeatureValue)
			{
				_GrayProjHorFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayProjHorFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayProjHorFeature"] = _GrayProjHorFeatureValue.ToString();
				}
				else
				{
					_GrayProjHorFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayProjVertFeatureValue
	{
		get
		{
			return _GrayProjVertFeatureValue;
		}
		set
		{
			if (value != _GrayProjVertFeatureValue)
			{
				_GrayProjVertFeatureValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayProjVertFeatureValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayProjVertFeature"] = _GrayProjVertFeatureValue.ToString();
				}
				else
				{
					_GrayProjVertFeatureValue = !value;
				}
			}
		}
	}

	public bool GrayHisValue
	{
		get
		{
			return _GrayHisValue;
		}
		set
		{
			if (value != _GrayHisValue)
			{
				_GrayHisValue = value;
				if (CheckGrayFeatureItem())
				{
					OnPropertyChanged("GrayHisValue");
					((IUserStringData)MLClassifyMgr.Instance.Param)["GrayHis"] = _GrayHisValue.ToString();
				}
				else
				{
					_GrayHisValue = !value;
				}
			}
		}
	}

	public int SvmMaxIterValue
	{
		get
		{
			return _SvmMaxIterValue;
		}
		set
		{
			if (value != _SvmMaxIterValue)
			{
				_SvmMaxIterValue = value;
				((IUserStringData)MLClassifyMgr.Instance.Param)["SvmMaxIter"] = _SvmMaxIterValue.ToString();
				OnPropertyChanged("SvmMaxIterValue");
			}
		}
	}

	public int SvmEspValue
	{
		get
		{
			return _SvmEspValue;
		}
		set
		{
			if (value != _SvmEspValue)
			{
				_SvmEspValue = value;
				((IUserStringData)MLClassifyMgr.Instance.Param)["SvmEsp"] = _SvmEspValue.ToString();
				OnPropertyChanged("SvmEspValue");
			}
		}
	}

	public int ClassifyTypeValue
	{
		get
		{
			return _ClassifyTypeValue;
		}
		set
		{
			if (value != _ClassifyTypeValue)
			{
				_ClassifyTypeValue = value;
				((IUserStringData)MLClassifyMgr.Instance.Param)["ClassifyType"] = _ClassifyTypeValue.ToString();
				OnPropertyChanged("ClassifyTypeValue");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public MLImagePage()
	{
		InitializeComponent();
		CategoryBrushData.Init();
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
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
		((Window)(object)val).Owner = Window.GetWindow(this);
		((FrameworkElement)(object)val).DataContext = viewGrid.DataContext;
		viewGrid.Children.Remove(imageView);
		viewGrid.Children.Remove(btnLeft);
		viewGrid.Children.Remove(btnRight);
		val.SetChild((UIElement)imageView);
		val.SetChild((UIElement)btnLeft);
		val.SetChild((UIElement)btnRight);
		((Window)(object)val).ShowDialog();
		val.RemoveChild();
		viewGrid.Children.Add(imageView);
		viewGrid.Children.Add(btnLeft);
		viewGrid.Children.Add(btnRight);
	}

	private void AddImage_Click(object sender, RoutedEventArgs e)
	{
		if (LoadLocalImage() && lst.SelectedIndex < 0)
		{
			lst.SelectedIndex = 0;
		}
		MLClassifyMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
	}

	private void DelAllImage_Click(object sender, RoutedEventArgs e)
	{
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ClearImageWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
		{
			ClearMarkData();
			ImageList.Clear();
			_CurIndex = -1;
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
			OnPropertyChanged("CurIndex");
			CurImage = null;
		}
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
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

	private void CategoryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
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
		SelectedItemsCount = (lst.SelectedItems?.Count).Value;
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
		LogHelper.Info($"lst_SelectionChanged {lstDeleteImage} {lstSelectionChangedDisable} {lst?.SelectedIndex}");
		if (lst == null)
		{
			return;
		}
		System.Windows.Controls.ListView listView = lst;
		if (listView != null && listView.SelectedIndex == -1)
		{
			return;
		}
		if (lstSelectionChangedDisable)
		{
			lst.SelectedIndex = -1;
			CurImage = null;
			lstSelectionChangedDisable = false;
			return;
		}
		System.Windows.Controls.ListView listView2 = lst;
		if (listView2 != null && listView2.SelectedIndex >= 0)
		{
			ImageEntity lastImage = null;
			if (_CurIndex >= 0 && _CurIndex < DisplayImageList.Count)
			{
				lastImage = DisplayImageList[_CurIndex];
			}
			_CurIndex = lst.SelectedIndex;
			UpdateDisplayInfo(lastImage);
			OnPropertyChanged("CurIndex");
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
		object selectedItem = lst.SelectedItem;
		ImageEntity image = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (image == null)
		{
			return;
		}
		ImageEntity val = image;
		if (!string.IsNullOrEmpty((val != null) ? val.ImageLabel : null))
		{
			CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData c) => c.CategoryName == image.ImageLabel);
			if (categoryData == null)
			{
				image.IsCalibrated = false;
				image.IsOKCalibrated = false;
				image.Boxes = null;
				image.ImageLabel = "";
				SelectedCategory = null;
			}
			else
			{
				if (image.IsCalibrated)
				{
					categoryData.MarkLableText = AppLocalizationService.GetLang("标记为此类");
				}
				else
				{
					categoryData.MarkLableText = AppLocalizationService.GetLang("标记到此类");
				}
				SelectedCategory = categoryData;
			}
		}
		else
		{
			image.IsCalibrated = false;
			image.IsOKCalibrated = false;
			image.Boxes = null;
			image.ImageLabel = "";
			SelectedCategory = null;
		}
	}

	private void Register_OnClick(object sender, RoutedEventArgs e)
	{
		if (MLClassifyMgr.Instance.Param != null)
		{
			if (!IsHasMark())
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLTrainError"), MessageIcons.Warning, isTranslate: true);
			}
			else if (!_isRegistering)
			{
				_isRegistering = true;
				RegisterAsync();
			}
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
			imageListType = ImageListType.MarkImage;
		}
		else if (comboBox.SelectedIndex == 2)
		{
			imageListType = ImageListType.UnMarkImage;
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
			_isUpdateOffset = false;
			lst.SelectedIndex = selectedIndex - 1;
			ScrollToLeft();
		}
	}

	private void next_Click(object sender, RoutedEventArgs e)
	{
		int selectedIndex = lst.SelectedIndex;
		if (selectedIndex < lst.Items.Count - 1)
		{
			_isUpdateOffset = false;
			lst.SelectedIndex = selectedIndex + 1;
			ScrollToRight();
		}
	}

	private void ClearMarkData_Click(object sender, RoutedEventArgs e)
	{
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ClearImageCategoryTagWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
		{
			ClearMarkData();
		}
	}

	private void DeleteImageEvent(object obj)
	{
		DeleteImage(obj);
	}

	private void Roi_StartValueChange(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		imageView_OnRoiParamChange(sender);
	}

	private void Category_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Grid { DataContext: CategoryData dataContext }))
		{
			return;
		}
		object selectedItem = lst.SelectedItem;
		ImageEntity image = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (image == null)
		{
			return;
		}
		if (image.ImageLabel != dataContext.CategoryName)
		{
			if (dataContext.CalibratedNum >= dataContext.RemainedNum)
			{
				HikMessageBox.Show(string.Format(AppLocalizationService.GetLang("Lang_RegisterCategoryLimitTip"), 100u), MessageIcons.Warning);
				return;
			}
			if (string.IsNullOrWhiteSpace(image.ImageLabel))
			{
				MarkNum++;
			}
			else
			{
				image.IsCalibrated = false;
				if (image.IsOKCalibrated)
				{
					image.IsOKCalibrated = false;
				}
				else
				{
					CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == image.ImageLabel);
					if (categoryData != null)
					{
						categoryData.CalibratedNum--;
					}
				}
			}
			image.ImageLabel = dataContext.CategoryName;
			dataContext.CalibratedNum++;
		}
		if (lst.SelectedIndex + 1 < lst.Items.Count)
		{
			_isUpdateOffset = false;
			lst.SelectedIndex++;
			if (lst.SelectedIndex > 1)
			{
				ScrollToRight();
			}
		}
	}

	private void AddCategory_Click(object sender, RoutedEventArgs e)
	{
		AddCategoryDialog addCategoryDialog = new AddCategoryDialog();
		addCategoryDialog.Init(CategoryList);
		if (addCategoryDialog.ShowDialog() != true)
		{
			return;
		}
		bool flag = false;
		foreach (CategoryData newCategory in addCategoryDialog.NewCategories)
		{
			if (!string.IsNullOrWhiteSpace(newCategory.CategoryName))
			{
				newCategory.ImageSource = new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/pic_颜色识别.png", UriKind.RelativeOrAbsolute));
				int num = 1;
				int i;
				for (i = 1; CategoryList.Where((CategoryData c) => c.CategoryIndex == i).Count() > 0; i++)
				{
				}
				num = i;
				newCategory.CategoryIndex = num;
				CategoryList.Add(newCategory);
				flag = true;
			}
		}
		if (flag)
		{
			OnPropertyChanged("DisplayCategoryList");
		}
	}

	private void EditCategory_OnClick(object sender, RoutedEventArgs e)
	{
	}

	private void DeleteCategory_OnClick(object sender, RoutedEventArgs e)
	{
		if (MLClassifyMgr.Instance.Param == null || HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DeleteCategoryWarn"), MessageButtons.YesOrNo) != VM.Controls.DialogResult.Yes)
		{
			return;
		}
		System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
		if (!(button.DataContext is CategoryData categoryData))
		{
			return;
		}
		if (categoryData.RegisteredNum != 0)
		{
			try
			{
				((IUserStringData)MLClassifyMgr.Instance.Param)["ClearClass"] = "ClearClass";
			}
			catch (VmException ex)
			{
				string text = AppLocalizationService.GetLang("Lang_DeleteCategoryFail") + "\r\n(";
				text += AppLocalizationService.GetLang("errInfo:");
				text += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				text += ")";
				HikMessageBox.Show(text, MessageIcons.Error);
				return;
			}
		}
		DeleteCategoryClearMarkData(categoryData);
		if (CategoryList.Contains(categoryData))
		{
			CategoryList.Remove(categoryData);
		}
		OnPropertyChanged("DisplayCategoryList");
	}

	private void FullScreen_Click(object sender, RoutedEventArgs e)
	{
		FullROI();
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null)
		{
			val.Boxes = null;
		}
	}

	private void RectROI_Click(object sender, RoutedEventArgs e)
	{
		IsDrawRect = true;
		imageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
	}

	private void imageView_DrawRoi_MouseUpChange(object sender, System.Windows.Input.MouseEventArgs e)
	{
		imageView.UnPrepareRawRoi();
		imageView.ContextMenuVisible = false;
		IsDrawRect = false;
		RectEnable = true;
	}

	private void imageView_OnRoiParamChange(object data)
	{
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null && RectRois.Count > 0)
		{
			HikCoverRoiParameter hikCoverRoiParameter = RectRois[0] as HikCoverRoiParameter;
			if (val.Boxes == null)
			{
				val.Boxes = new MVBI_BOX_F[1];
			}
			val.Boxes[0].center_x = (float)hikCoverRoiParameter.CenterX;
			val.Boxes[0].center_y = (float)hikCoverRoiParameter.CenterY;
			val.Boxes[0].width = (float)hikCoverRoiParameter.Width;
			val.Boxes[0].height = (float)hikCoverRoiParameter.Height;
			val.Boxes[0].angle = (float)hikCoverRoiParameter.Angle;
		}
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
		if (RectRois.Count <= 1)
		{
			return;
		}
		RectRois.RemoveAt(0);
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null && RectRois.Count > 0)
		{
			HikCoverRoiParameter hikCoverRoiParameter = RectRois[0] as HikCoverRoiParameter;
			if (val.Boxes == null)
			{
				val.Boxes = new MVBI_BOX_F[1];
			}
			val.Boxes[0].center_x = (float)hikCoverRoiParameter.CenterX;
			val.Boxes[0].center_y = (float)hikCoverRoiParameter.CenterY;
			val.Boxes[0].width = (float)hikCoverRoiParameter.Width;
			val.Boxes[0].height = (float)hikCoverRoiParameter.Height;
			val.Boxes[0].angle = (float)hikCoverRoiParameter.Angle;
		}
	}

	private void imageView_ROIClick(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement)
		{
			SelectBox(frameworkElement.DataContext as HikCoverRoiParameter);
		}
	}

	private void CategoryWord_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is System.Windows.Controls.TextBox textBox)
		{
			CategoryWord = textBox.Text;
		}
	}

	private void FreshOptionsCommandExecute()
	{
		moreOptions = new List<string>();
		moreOptions.Add(AppLocalizationService.GetLang("Lang_CurImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_CacheImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportLocalImage"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportFolder"));
		moreOptions.Add(AppLocalizationService.GetLang("Lang_ImportFolderByCategory"));
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
					MLClassifyMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
					if (text == AppLocalizationService.GetLang("Lang_CurImage"))
					{
						CreateFolder(GetSavePath());
						flag = LoadCurrentImage();
					}
					else if (text == AppLocalizationService.GetLang("Lang_CacheImage"))
					{
						CreateFolder(GetSavePath());
						flag = LoadCacheImage();
					}
					if (text == AppLocalizationService.GetLang("Lang_ImportLocalImage"))
					{
						flag = LoadLocalImage();
					}
					else if (text == AppLocalizationService.GetLang("Lang_ImportFolder"))
					{
						flag = LoadFolder();
					}
					else if (text == AppLocalizationService.GetLang("Lang_ImportFolderByCategory"))
					{
						if (SelectedCategory == null)
						{
							HikMessageBox.Show("Lang_ImportCategoryWarning", MessageIcons.Warning, isTranslate: true);
						}
						else
						{
							flag = LoadFolder(isCalibrate: true);
						}
					}
					if (flag && !(text == AppLocalizationService.GetLang("Lang_ImportFolderByCategory")) && lst.SelectedIndex < 0)
					{
						lst.SelectedIndex = 0;
					}
					MLClassifyMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
				}
			}
		}));
	}

	public void Init(ObservableCollection<CategoryData> categoryList)
	{
		if (categoryList != null)
		{
			foreach (CategoryData category2 in categoryList)
			{
				CategoryList.Add(category2);
			}
		}
		foreach (KeyValuePair<string, string> emptyCategory in MLClassifyMgr.Instance.EmptyCategoryList)
		{
			BrushConverter brushConverter = new BrushConverter();
			CategoryData category = new CategoryData
			{
				CategoryName = emptyCategory.Key,
				CategoryIndex = Convert.ToInt32(emptyCategory.Value.Split(';')[1])
			};
			category.ImageSource = new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/pic_颜色识别.png", UriKind.RelativeOrAbsolute));
			CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData c) => c.CategoryName == category.CategoryName);
			if (categoryData != null)
			{
				CategoryList.Remove(categoryData);
			}
			CategoryList.Add(category);
		}
		HikImageView hikImageView = imageView;
		hikImageView.OnDrawRoi_MouseUpChange = (HikImageView.DrawRoi_MouseUpChange)Delegate.Combine(hikImageView.OnDrawRoi_MouseUpChange, new HikImageView.DrawRoi_MouseUpChange(imageView_DrawRoi_MouseUpChange));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView2.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		MLClassifyMgr.Instance.DeleteImageEvent += DeleteImageEvent;
		LoadImages();
	}

	public void DeInit()
	{
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		MLClassifyMgr.Instance.DeleteImageEvent -= DeleteImageEvent;
		if (imageView != null)
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
			HikImageView hikImageView2 = imageView;
			hikImageView2.OnDrawRoi_MouseUpChange = (HikImageView.DrawRoi_MouseUpChange)Delegate.Remove(hikImageView2.OnDrawRoi_MouseUpChange, new HikImageView.DrawRoi_MouseUpChange(imageView_DrawRoi_MouseUpChange));
			imageView.Dispose();
			imageView.DataContext = null;
		}
		MLClassifyMgr.Instance.SampleImageDataList.Clear();
		foreach (ImageEntity image in ImageList)
		{
			SampleImageData sampleImageData = new SampleImageData();
			sampleImageData.ImagePath = image.ImagePath;
			sampleImageData.IsCalibrated = image.IsCalibrated;
			sampleImageData.IsOKCalibrated = image.IsOKCalibrated;
			sampleImageData.ImageLabel = image.ImageLabel;
			if (image.Boxes != null)
			{
				sampleImageData.RoiData = GetRoiBuffer(image.Boxes[0]);
			}
			MLClassifyMgr.Instance.SampleImageDataList.Add(sampleImageData);
		}
		MLClassifyMgr.Instance.EmptyCategoryList.Clear();
		foreach (CategoryData category in CategoryList)
		{
			if (category.RegisteredNum == 0)
			{
				MLClassifyMgr.Instance.EmptyCategoryList.Add(category.CategoryName, category.CategoryBrush.ToString() + ";" + category.CategoryIndex);
			}
		}
		ClearMarkData();
		DataContext = null;
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

	private void ScrollToRight()
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

	private void ScrollToLeft()
	{
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val != null)
		{
			FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(val);
			if (!IsFullVisibleInParent(child, listScroll))
			{
				_CurHorizontalOffset -= GetHorizontalOffset(child, listScroll);
				listScroll.ScrollToHorizontalOffset(_CurHorizontalOffset);
			}
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
		Rect rect = generalTransform.TransformBounds(new Rect(new Point(0.0, 0.0), child.RenderSize));
		return new Rect(new Point(0.0, 0.0), parent.RenderSize).IntersectsWith(rect);
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
			result = ((!(rect3 != Rect.Empty)) ? rect.Width : (rect.Width - rect3.Width));
		}
		return result;
	}

	private void DeleteImage(object obj)
	{
		try
		{
			LogHelper.Info($"DeleteImage {lstDeleteImage} {lstSelectionChangedDisable}");
			if (lstSelectionChangedDisable)
			{
				lstSelectionChangedDisable = false;
			}
			int selectedIndex = lst.SelectedIndex;
			ImageEntity item = (ImageEntity)((obj is ImageEntity) ? obj : null);
			if (item != null)
			{
				_ImageList.Remove(item);
				if (!string.IsNullOrWhiteSpace(item.ImageLabel))
				{
					MarkNum--;
					if (!item.IsOKCalibrated)
					{
						CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == item.ImageLabel);
						if (categoryData != null)
						{
							categoryData.CalibratedNum--;
						}
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
				CurImage = null;
				IsDrawRect = false;
				RectEnable = false;
				m_ImageType.SelectedIndex = 0;
				_CurHorizontalOffset = 0.0;
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
			LogHelper.moduleLog.Error("Delete image error: " + ex.Message);
		}
	}

	private async void RegisterAsync()
	{
		await Task.Run(() =>
		{
			//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0189: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
			((IUserStringData)MLClassifyMgr.Instance.Param)["ClearClass"] = "ClearClass";
			CreatModel.Instance.IsCreatingModel = true;
			MLClassifyMgr.Instance.Owner.ShowProgressDialog("Lang_RegisterProgressText");
			MLClassifyMgr.Instance.Param.TimeOut = 2147483647u;
			int count = ImageList.Count;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			string strErrorMsg = "";
			foreach (ImageEntity image in ImageList)
			{
				num++;
				if (!string.IsNullOrWhiteSpace(image.ImageLabel))
				{
					try
					{
						List<byte> list = new List<byte>();
						byte[] bytes = BitConverter.GetBytes(1);
						list.AddRange(bytes);
						IntPtr imagePtr = GetImagePtr(image.ImagePath, out var len, out var width, out var height);
						if (!(imagePtr != IntPtr.Zero))
						{
							CreatModel.Instance.IsCreatingModel = false;
							throw new Exception(AppLocalizationService.GetLang("Lang_LoadingImageError"));
						}
						byte[] array = new byte[len];
						Marshal.Copy(imagePtr, array, 0, len);
						list.AddRange(array);
						Marshal.FreeHGlobal(imagePtr);
						try
						{
							byte[] value = list.ToArray();
							((IUserBytesData)MLClassifyMgr.Instance.Param)["SetRegisterImage"] = value;
							MVBI_BOX_F box = ((image.Boxes != null) ? image.Boxes[0] : new MVBI_BOX_F
							{
								center_x = width / 2,
								center_y = height / 2,
								width = width,
								height = height,
								angle = 0f
							});
							byte[] roiBuffer = GetRoiBuffer(box);
							((IUserStringData)MLClassifyMgr.Instance.Param)["SetRegisterClassName"] = image.ImageLabel;
							CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData c) => c.CategoryName == image.ImageLabel);
							((IUserStringData)MLClassifyMgr.Instance.Param)["SetRegisterClassIndex"] = categoryData.CategoryIndex.ToString();
							((IUserStringData)MLClassifyMgr.Instance.Param)["SetRegisterClassColor"] = categoryData.CategoryBrushStr;
							image.IsOKCalibrated = true;
							image.IsCalibrated = true;
							num2++;
						}
						catch (VmException ex)
						{
							CreatModel.Instance.IsCreatingModel = false;
							throw new Exception(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
						}
						catch (Exception ex2)
						{
							CreatModel.Instance.IsCreatingModel = false;
							int result = 0;
							string message = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
							throw new Exception(message);
						}
					}
					catch (Exception ex3)
					{
						CreatModel.Instance.IsCreatingModel = false;
						strErrorMsg += $"\r\n{image.ImageName} ({ex3.Message})";
						image.IsOKCalibrated = false;
						image.IsCalibrated = true;
					}
					finally
					{
						CategoryData categoryData2 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == image.ImageLabel);
						if (categoryData2 != null && image.IsOKCalibrated)
						{
							categoryData2.CalibratedNum--;
						}
						num3 = 10 + (int)((float)num / (float)count * 80f);
						MLClassifyMgr.Instance.Owner.SetProgressValue(num3);
					}
				}
			}
			if (num2 > 0)
			{
				try
				{
					((IUserStringData)MLClassifyMgr.Instance.Param)["Train"] = "1";
				}
				catch (Exception ex4)
				{
					CreatModel.Instance.IsCreatingModel = false;
					MLClassifyMgr.Instance.Owner.CloseProgressDialog();
					LogHelper.Error(ex4.ToString());
					_isRegistering = false;
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLTrainError"));
					return;
				}
				Dispatcher.Invoke(() =>
				{
					QueryAllCategory();
				});
			}
			MLClassifyMgr.Instance.Param.TimeOut = 3000u;
			MLClassifyMgr.Instance.Owner.CloseProgressDialog();
			Dispatcher.Invoke(() =>
			{
				if (string.IsNullOrEmpty(strErrorMsg))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RegisterSuccess"), MessageIcons.Ok);
				}
				else
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RegisterFail"), AppLocalizationService.GetLang("Lang_RegisterFailImageList") + strErrorMsg, MessageIcons.Error);
					LogHelper.moduleLog.Error("Register failure, " + AppLocalizationService.GetLang("Lang_RegisterFailImageList") + strErrorMsg);
				}
			});
			CreatModel.Instance.IsCreatingModel = false;
			_isRegistering = false;
		});
	}

	private void QueryAllCategory()
	{
		string[] array2;
		try
		{
			string empty = string.Empty;
			empty = ((IUserStringData)MLClassifyMgr.Instance.Param)["GetCurClassNum"];
			string[] array = empty.Split(';');
			if (array.Count() != 2)
			{
				LogHelper.Info("strClassNum  value:" + array.Count());
				return;
			}
			int num = Convert.ToInt32(array[0]);
			array2 = array[1].Split(',');
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("Lang_GetCategoryCountFail") + "\r\n(";
			text += AppLocalizationService.GetLang("errInfo:");
			text += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text += ")";
			LogHelper.moduleLog.Error(text);
			return;
		}
		string empty2 = string.Empty;
		string strCategoryName = string.Empty;
		string empty3 = string.Empty;
		string empty4 = string.Empty;
		ImageSource imageSource = null;
		byte[] array3 = null;
		ObservableCollection<CategoryData> observableCollection = new ObservableCollection<CategoryData>();
		CategoryList.ToList().ForEach((CategoryData c) =>
		{
			c.IsNeedClear = 0;
		});
		for (int num2 = 0; num2 < array2.Count(); num2++)
		{
			imageSource = null;
			array3 = null;
			IntPtr intPtr = IntPtr.Zero;
			string text2 = array2[num2];
			try
			{
				strCategoryName = ((IUserStringData)MLClassifyMgr.Instance.Param)["GetQueriedLabel_" + text2.ToString()];
				CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == strCategoryName);
				if (categoryData == null)
				{
					continue;
				}
				observableCollection.Add(categoryData);
				empty3 = ((IUserStringData)MLClassifyMgr.Instance.Param)["GetRegisterNum_" + text2.ToString()];
				categoryData.RegisteredNum = Convert.ToUInt32(empty3);
				empty2 = ((IUserStringData)MLClassifyMgr.Instance.Param)["GetQueriedIndex_" + text2.ToString()];
				categoryData.CategoryIndex = Convert.ToInt32(empty2);
				array3 = ((IUserBytesData)MLClassifyMgr.Instance.Param)["GetImageSample_" + text2.ToString()];
				if (array3 != null && array3.Length != 0)
				{
					int num3 = array3.Length;
					intPtr = Marshal.AllocHGlobal(num3);
					if (intPtr != IntPtr.Zero)
					{
						Marshal.Copy(array3, 0, intPtr, num3);
						imageSource = MLClassifyMgr.Instance.GetRGBImage(intPtr, num3);
						if (imageSource != null)
						{
							categoryData.ImageSource = imageSource;
						}
					}
				}
				categoryData.IsNeedClear = 1;
			}
			catch (VmException ex2)
			{
				LogHelper.moduleLog.Error($"Get category {text2} information error: {ErrorCodeInfo.GetErrorInfo(ex2.errorCode)}");
			}
			catch (Exception ex3)
			{
				int result = 0;
				string arg = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex3.Message);
				LogHelper.moduleLog.Error($"Get category {text2} information error: {arg}");
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
		foreach (CategoryData category in CategoryList)
		{
			if (category.IsNeedClear == 0)
			{
				category.RegisteredNum = 0u;
				category.ImageSource = null;
			}
		}
	}

	private byte[] GetRoiBuffer(MVBI_BOX_F box)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = new byte[ModuleDefine.RECT_ROI_SIZE];
		IntPtr intPtr = Marshal.AllocHGlobal(ModuleDefine.RECT_ROI_SIZE);
		try
		{
			Marshal.StructureToPtr<MVBI_BOX_F>(box, intPtr, fDeleteOld: false);
			Marshal.Copy(intPtr, array, 0, ModuleDefine.RECT_ROI_SIZE);
			return array;
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error("GetRoiBuffer error: " + ex.Message);
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
		return null;
	}

	private unsafe IntPtr GetImagePtr(string path, out int len, out int width, out int height)
	{
		len = 0;
		width = 0;
		height = 0;
		if (File.Exists(path))
		{
			BitmapImage bitmapImage = ImageData.CreateThumbnail(path);
			if (bitmapImage == null)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_LoadingImageError"));
			}
			if (bitmapImage.PixelWidth > 0)
			{
				WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapImage);
				if (writeableBitmap == null || writeableBitmap.BackBuffer == IntPtr.Zero)
				{
					LogHelper.moduleLog.Error("Create WriteableBitmap image error.");
					return IntPtr.Zero;
				}
				int num = 12;
				int num2 = 0;
				int num3 = writeableBitmap.Format.BitsPerPixel / 8;
				switch (num3)
				{
				case 1:
					num2 = writeableBitmap.PixelWidth * writeableBitmap.PixelHeight;
					break;
				case 4:
					num2 = writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * 3;
					break;
				default:
					return IntPtr.Zero;
				}
				len = num + num2;
				width = writeableBitmap.PixelWidth;
				height = writeableBitmap.PixelHeight;
				IntPtr intPtr = Marshal.AllocHGlobal(len);
				if (intPtr == IntPtr.Zero)
				{
					throw new Exception(AppLocalizationService.GetLang("内存不足"));
				}
				byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
				int backBufferStride = writeableBitmap.BackBufferStride;
				void* ptr2 = intPtr.ToPointer();
				*(int*)ptr2 = writeableBitmap.PixelWidth;
				((int*)ptr2)[1] = writeableBitmap.PixelHeight;
				((int*)ptr2)[2] = num2;
				byte* ptr3 = (byte*)ptr2 + num;
				int pixelHeight = writeableBitmap.PixelHeight;
				int pixelWidth = writeableBitmap.PixelWidth;
				switch (num3)
				{
				case 1:
				{
					for (int k = 0; k < pixelHeight; k++)
					{
						byte* ptr5 = ptr + k * backBufferStride;
						for (int l = 0; l < pixelWidth; l++)
						{
							*ptr3 = ptr5[l];
							ptr3++;
						}
					}
					break;
				}
				case 4:
				{
					for (int i = 0; i < pixelHeight; i++)
					{
						byte* ptr4 = ptr + i * backBufferStride;
						for (int j = 0; j < pixelWidth; j++)
						{
							*ptr3 = (ptr4 + j * num3)[2];
							ptr3[1] = (ptr4 + j * num3)[1];
							ptr3[2] = ptr4[j * num3];
							ptr3 += 3;
						}
					}
					break;
				}
				default:
					return IntPtr.Zero;
				}
				return intPtr;
			}
		}
		return IntPtr.Zero;
	}

	private bool IsHasMark()
	{
		int num = ImageList.Where((ImageEntity c) => !string.IsNullOrWhiteSpace(c.ImageLabel)).Count();
		if (num < 4)
		{
			return false;
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (ImageEntity image in ImageList)
		{
			if (!string.IsNullOrEmpty(image.ImageLabel))
			{
				if (dictionary.Keys.Contains(image.ImageLabel))
				{
					dictionary[image.ImageLabel]++;
				}
				else
				{
					dictionary.Add(image.ImageLabel, 1);
				}
			}
		}
		if (dictionary.Count < 2)
		{
			return false;
		}
		foreach (KeyValuePair<string, int> item in dictionary)
		{
			if (item.Value < 2)
			{
				return false;
			}
		}
		foreach (ImageEntity image2 in ImageList)
		{
			if (!string.IsNullOrWhiteSpace(image2.ImageLabel) && (!image2.IsCalibrated || (image2.IsCalibrated && !image2.IsOKCalibrated)))
			{
				return true;
			}
		}
		return false;
	}

	private void DeleteCategoryClearMarkData(CategoryData category)
	{
		foreach (ImageEntity image in ImageList)
		{
			if (image.ImageLabel == category.CategoryName)
			{
				image.IsCalibrated = false;
				image.IsOKCalibrated = false;
				image.ImageLabel = "";
				MarkNum--;
			}
			else
			{
				image.IsOKCalibrated = false;
				image.IsCalibrated = false;
			}
		}
		foreach (CategoryData item in CategoryList)
		{
			if (item.CategoryName == category.CategoryName)
			{
				item.CalibratedNum = 0u;
				item.RegisteredNum = 0u;
				continue;
			}
			item.CalibratedNum = (uint)ImageList.Where((ImageEntity c) => c.ImageLabel == item.CategoryName).Count();
			item.RegisteredNum = 0u;
		}
		m_ImageType.SelectedIndex = 0;
		listScroll.ScrollToLeftEnd();
		lst.SelectedIndex = 0;
		_CurHorizontalOffset = 0.0;
		SelectedCategory = null;
	}

	private void ClearMarkData()
	{
		foreach (ImageEntity image in ImageList)
		{
			image.IsCalibrated = false;
			image.IsOKCalibrated = false;
			image.Boxes = null;
			image.ImageLabel = "";
		}
		foreach (CategoryData category in CategoryList)
		{
			category.CalibratedNum = 0u;
		}
		MarkNum = 0;
		IsDrawRect = false;
		RectEnable = false;
		if (RectRois.Count > 0)
		{
			RectRois.Clear();
			SelectedBox = null;
		}
		m_ImageType.SelectedIndex = 0;
		listScroll.ScrollToLeftEnd();
		lst.SelectedIndex = 0;
		_CurHorizontalOffset = 0.0;
		SelectedCategory = null;
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

	private void UpdateDisplayInfo(ImageEntity lastImage)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		CurImage = null;
		object selectedItem = lst.SelectedItem;
		ImageEntity val = (ImageEntity)((selectedItem is ImageEntity) ? selectedItem : null);
		if (val == null)
		{
			return;
		}
		ImageData imageDataFromFile = ImageData.GetImageDataFromFile(val.ImagePath);
		if (imageDataFromFile != null)
		{
			val.ImageWidth = imageDataFromFile.Width;
			val.ImageHeight = imageDataFromFile.Height;
			CurImage = imageDataFromFile;
			if (_init)
			{
				imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
				_init = false;
			}
			if (!val.IsCalibrated && val.Boxes == null && lastImage != null && lastImage.Boxes != null && !IsOverstep(lastImage.Boxes[0]))
			{
				val.Boxes = new MVBI_BOX_F[1];
				val.Boxes[0].center_x = lastImage.Boxes[0].center_x;
				val.Boxes[0].center_y = lastImage.Boxes[0].center_y;
				val.Boxes[0].width = lastImage.Boxes[0].width;
				val.Boxes[0].height = lastImage.Boxes[0].height;
				val.Boxes[0].angle = lastImage.Boxes[0].angle;
			}
			if (val.Boxes != null && val.Boxes.Length != 0)
			{
				MVBI_BOX_F val2 = val.Boxes[0];
				if (RectRois.Count > 0)
				{
					if (RectRois[0] is HikCoverRoiParameter hikCoverRoiParameter)
					{
						hikCoverRoiParameter.CenterX = val2.center_x;
						hikCoverRoiParameter.CenterY = val2.center_y;
						hikCoverRoiParameter.Width = val2.width;
						hikCoverRoiParameter.Height = val2.height;
						hikCoverRoiParameter.Angle = val2.angle;
					}
				}
				else
				{
					RectRois.Add(new HikCoverRoiParameter
					{
						CenterX = val2.center_x,
						CenterY = val2.center_y,
						Width = val2.width,
						Height = val2.height,
						Angle = val2.angle
					});
					RectEnable = true;
				}
			}
			else
			{
				FullROI();
			}
		}
		else
		{
			DeleteImage(lst.SelectedItem);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetImageDataError"), MessageIcons.Error);
		}
	}

	private bool IsOverstep(MVBI_BOX_F box)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		bool result = false;
		if (CurImage == null)
		{
			return result;
		}
		Rect rect = new Rect(0.0, 0.0, CurImage.Width, CurImage.Height);
		Rect rect2 = new Rect(box.center_x - box.width / 2f, box.center_y - box.height / 2f, box.width, box.height);
		Matrix matrix = default;
		matrix.RotateAt(box.angle, box.center_x, box.center_y);
		rect2.Transform(matrix);
		Point[] array = rect2.RectToPoints();
		Point[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			Point point = array2[i];
			if (Math.Round(point.X) < rect.Left)
			{
				result = true;
				break;
			}
			if (Math.Round(point.X) > rect.Right)
			{
				result = true;
				break;
			}
			if (Math.Round(point.Y) < rect.Top)
			{
				result = true;
				break;
			}
			if (Math.Round(point.Y) > rect.Bottom)
			{
				result = true;
				break;
			}
		}
		return result;
	}

	private void FullROI()
	{
		imageView.UnPrepareRawRoi();
		IsDrawRect = false;
		RectEnable = false;
		if (RectRois.Count > 0)
		{
			RectRois.Clear();
			SelectedBox = null;
		}
		imageView.ContextMenuVisible = false;
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

	private bool LoadCurrentImage()
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected Obj, but got Unknown
		string newPath = GetNewImageName();
		if (newPath == null)
		{
			return false;
		}
		ImageData curImageData = MLClassifyMgr.Instance.CurImageData;
		if (curImageData != null)
		{
			try
			{
				ImageSaveOpenCvHelper.SaveRawImageSyn(newPath, curImageData);
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
				LogHelper.moduleLog.Error("Load current image error: " + ex.Message + Environment.NewLine + "path: " + newPath);
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
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Expected Obj, but got Unknown
		bool flag = false;
		List<ImageData> cacheImageList = ModuleCommonMethodMgr.Instance.GetCacheImageList(MLClassifyMgr.Instance.Module);
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
					ImageSaveOpenCvHelper.SaveRawImageSyn(imageName, item);
					if (_ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == imageName) == null)
					{
						_ImageList.Add(new ImageEntity
						{
							ImagePath = imageName
						});
					}
					flag = true;
				}
				catch (Exception ex)
				{
					LogHelper.moduleLog.Error("Load cache image error: " + ex.Message);
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
		string text = ConfigurationHelper.INIGetDefaultOpenPath(MLClassifyMgr.Instance.Module?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpg;*.jpeg;*.png";
		if (openFileDialog.ShowDialog() == false)
		{
			return false;
		}
		if (openFileDialog.FileName.Length > 260 || openFileDialog.FileName == "")
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathLong"), MessageIcons.Warning);
			return false;
		}
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), MLClassifyMgr.Instance.Module?.ModuleDir);
		AddImagesAsync(openFileDialog.FileNames);
		return true;
	}

	private bool LoadFolder(bool isCalibrate = false)
	{
		string text = "";
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		string text2 = ConfigurationHelper.INIGetDefaultOpenPath(MLClassifyMgr.Instance.Module?.ModuleDir);
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
		catch (PathTooLongException ex)
		{
			HikMessageBox.Show("Lang_DrAdd", "Lang_FilePathLong", MessageIcons.Error, isTranslate: true);
			LogHelper.moduleLog.Error("Load folder error: " + ex.Message);
			return false;
		}
		finally
		{
			folderBrowserDialog.Dispose();
		}
		AddImageByDir(text, isCalibrate);
		ConfigurationHelper.INISetDefaultOpenPath(text, MLClassifyMgr.Instance.Module?.ModuleDir);
		return true;
	}

	private async void LoadImages()
	{
		await Task.Run(() =>
		{
			MLClassifyMgr.Instance.Owner.ShowProgressDialog("正在加载图片");
			int count = MLClassifyMgr.Instance.SampleImageDataList.Count;
			int num = 0;
			int num2 = 0;
			try
			{
				foreach (SampleImageData image in MLClassifyMgr.Instance.SampleImageDataList)
				{
					if (File.Exists(image.ImagePath) && _ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == image.ImagePath) == null)
					{
						Dispatcher.Invoke(() =>
						{
							//IL_0024: Unknown result type (might be due to invalid IL or missing references)
							//IL_0029: Unknown result type (might be due to invalid IL or missing references)
							//IL_0031: Unknown result type (might be due to invalid IL or missing references)
							//IL_0043: Unknown result type (might be due to invalid IL or missing references)
							//IL_005a: Expected Obj, but got Unknown
							BitmapImage imageSource = ImageData.CreateThumbnail(image.ImagePath, 104.0, 78.0);
							ImageEntity val = new ImageEntity
							{
								ImageSource = imageSource,
								ImagePath = image.ImagePath,
								Boxes = MVBI_BOX_F.GetBoxesWithoutNum(image.RoiData)
							};
							if (!string.IsNullOrWhiteSpace(image.ImageLabel))
							{
								CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == image.ImageLabel);
								if (categoryData != null)
								{
									val.IsCalibrated = image.IsCalibrated;
									val.IsOKCalibrated = image.IsOKCalibrated;
									val.ImageLabel = image.ImageLabel;
									int markNum = MarkNum;
									MarkNum = markNum + 1;
									if (!image.IsOKCalibrated)
									{
										categoryData.CalibratedNum++;
									}
								}
							}
							_ImageList.Add(val);
						});
					}
					num++;
					num2 = 10 + (int)((float)num / (float)count * 90f);
					MLClassifyMgr.Instance.Owner.SetProgressValue(num2);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				MLClassifyMgr.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex3.Message, MessageIcons.Warning);
					LogHelper.moduleLog.Error("Load images error when initializing: " + ex3.Message);
				});
			}
			Dispatcher.Invoke(() =>
			{
				if (_ImageList.Count > 0)
				{
					lst.SelectedIndex = 0;
				}
			});
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
			MLClassifyMgr.Instance.Owner.CloseProgressDialog();
		});
	}

	private void AddImageByDir(string dir, bool isCalibrate = false)
	{
		try
		{
			if (Directory.Exists(dir))
			{
				AddImagesAsync(Directory.GetFileSystemEntries(dir).ToArray(), isCalibrate);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), ex.Message, MessageIcons.Error);
		}
	}

	private async void AddImagesAsync(string[] fileNames, bool isCalibrate = false)
	{
		await Task.Run(() =>
		{
			MLClassifyMgr.Instance.Owner.ShowProgressDialog("正在加载图片");
			int num = fileNames.Length;
			int num2 = 0;
			int num3 = 0;
			try
			{
				string[] array = fileNames;
				foreach (string text in array)
				{
					if (Directory.Exists(text))
					{
						DoAddImages(Directory.GetFileSystemEntries(text).ToArray(), isCalibrate);
					}
					else
					{
						if (!MLClassifyMgr.Instance.IsSupportImage(Path.GetExtension(text)))
						{
							continue;
						}
						DoAddImage(text, isCalibrate);
					}
					num2++;
					num3 = 10 + (int)((float)num2 / (float)num * 90f);
					MLClassifyMgr.Instance.Owner.SetProgressValue(num3);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				MLClassifyMgr.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex3.Message, MessageIcons.Warning);
				});
			}
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
			MLClassifyMgr.Instance.Owner.CloseProgressDialog();
		});
		if (isCalibrate && lst.SelectedIndex < 0)
		{
			lst.SelectedIndex = 0;
		}
	}

	private void DoAddImages(string[] fileNames, bool isCalibrate = false)
	{
		foreach (string text in fileNames)
		{
			if (Directory.Exists(text))
			{
				DoAddImages(Directory.GetFileSystemEntries(text).ToArray(), isCalibrate);
			}
			else if (MLClassifyMgr.Instance.IsSupportImage(Path.GetExtension(text)))
			{
				DoAddImage(text, isCalibrate);
			}
		}
	}

	private void DoAddImage(string name, bool isCalibrate = false)
	{
		if (name.Length >= 260)
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_FilePathLong"));
		}
		byte[] bytes = Encoding.UTF8.GetBytes(name);
		if (bytes.Length > 255)
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_FilePathLong"));
		}
		if (name.Length > 0 && name[0] == '\\')
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_LocalImageNeed"));
		}
		if (ImageList.Count >= 500)
		{
			throw new Exception(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 500);
		}
		if (isCalibrate)
		{
			Dispatcher.Invoke(() =>
			{
				if (SelectedCategory.CalibratedNum >= SelectedCategory.RemainedNum)
				{
					throw new Exception(string.Format(AppLocalizationService.GetLang("Lang_RegisterCategoryLimitTip"), 100u));
				}
			});
		}
		AddImageToList(name, isCalibrate);
	}

	private void AddImageToList(string strImagePath, bool isCalibrate = false)
	{
		if (!File.Exists(strImagePath))
		{
			return;
		}
		ImageEntity image = _ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == strImagePath);
		if (image == null)
		{
			Dispatcher.Invoke(() =>
			{
				//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d0: Expected Obj, but got Unknown
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				//IL_003a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0047: Unknown result type (might be due to invalid IL or missing references)
				//IL_004f: Unknown result type (might be due to invalid IL or missing references)
				//IL_006b: Expected Obj, but got Unknown
				BitmapImage imageSource = ImageData.CreateThumbnail(strImagePath, 104.0, 78.0);
				if (isCalibrate)
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = strImagePath,
						ImageSource = imageSource,
						ImageLabel = SelectedCategory.CategoryName
					});
					SelectedCategory.CalibratedNum++;
					int markNum = MarkNum;
					MarkNum = markNum + 1;
				}
				else
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = strImagePath,
						ImageSource = imageSource
					});
				}
			});
			return;
		}
		Dispatcher.Invoke(() =>
		{
			if (isCalibrate && string.IsNullOrWhiteSpace(image.ImageLabel))
			{
				image.ImageLabel = SelectedCategory.CategoryName;
				SelectedCategory.CalibratedNum++;
				int markNum = MarkNum;
				MarkNum = markNum + 1;
			}
		});
	}

	private string GetNewImageName(string extension = ".bmp")
	{
		string result = null;
		if (MLClassifyMgr.Instance.Module != null)
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

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		GetTrainParam();
	}

	public void GetTrainParam()
	{
		try
		{
			_TextureFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["TextureFeature"]);
			_HogFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["HogFeature"]);
			_LbpFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["LbpFeature"]);
			_GlcmFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GlcmFeature"]);
			_EdgeFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["EdgeFeature"]);
			_PolarFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["PolarFeature"]);
			_GrayFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayFeature"]);
			_GrayRangeFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayRangeFeature"]);
			_GrayMeanFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayMeanFeature"]);
			_GrayDeviationFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayDeviationFeature"]);
			_GrayEntropyFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayEntropyFeature"]);
			_GrayAnisotropyFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayAnisotropyFeature"]);
			_GrayProjHorFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayProjHorFeature"]);
			_GrayProjVertFeatureValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayProjVertFeature"]);
			_GrayHisValue = Convert.ToBoolean(((IUserStringData)MLClassifyMgr.Instance.Param)["GrayHis"]);
			_SvmMaxIterValue = Convert.ToInt32(((IUserStringData)MLClassifyMgr.Instance.Param)["SvmMaxIter"]);
			_SvmEspValue = Convert.ToInt32(((IUserStringData)MLClassifyMgr.Instance.Param)["SvmEsp"]);
			_ClassifyTypeValue = Convert.ToInt32(((IUserStringData)MLClassifyMgr.Instance.Param)["ClassifyType"]);
			OnPropertyChanged("TextureFeatureValue");
			OnPropertyChanged("HogFeatureValue");
			OnPropertyChanged("LbpFeatureValue");
			OnPropertyChanged("GlcmFeatureValue");
			OnPropertyChanged("EdgeFeatureValue");
			OnPropertyChanged("PolarFeatureValue");
			OnPropertyChanged("GrayFeatureValue");
			OnPropertyChanged("GrayRangeFeatureValue");
			OnPropertyChanged("GrayMeanFeatureValue");
			OnPropertyChanged("GrayDeviationFeatureValue");
			OnPropertyChanged("GrayEntropyFeatureValue");
			OnPropertyChanged("GrayAnisotropyFeatureValue");
			OnPropertyChanged("GrayProjHorFeatureValue");
			OnPropertyChanged("GrayProjVertFeatureValue");
			OnPropertyChanged("GrayHisValue");
			OnPropertyChanged("SvmMaxIterValue");
			OnPropertyChanged("SvmEspValue");
			OnPropertyChanged("ClassifyTypeValue");
		}
		catch (Exception)
		{
		}
	}

	private void TrainParam_Checked(object sender, RoutedEventArgs e)
	{
	}

	public bool CheckFeature()
	{
		try
		{
			if (!_TextureFeatureValue && !_GrayFeatureValue)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLClassTrainFeatrueOpenError"));
				return false;
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool CheckTextureFeatureItem()
	{
		try
		{
			if (!_HogFeatureValue && !_LbpFeatureValue && !_GlcmFeatureValue && !_EdgeFeatureValue && !_PolarFeatureValue)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLClassTrainParamOpenError"));
				return false;
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool CheckGrayFeatureItem()
	{
		try
		{
			if (!_GrayRangeFeatureValue && !_GrayMeanFeatureValue && !_GrayDeviationFeatureValue && !_GrayEntropyFeatureValue && !_GrayAnisotropyFeatureValue && !_GrayProjHorFeatureValue && !_GrayProjVertFeatureValue && !_GrayHisValue)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLClassTrainParamOpenError"));
				return false;
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void ImageClick(object sender, MouseButtonEventArgs e)
	{
		LogHelper.Info($"ImageClick {lstDeleteImage} {lstSelectionChangedDisable}");
		ImageEntity val;
		if (sender is ImageView { Tag: var tag } && (val = (ImageEntity)((tag is ImageEntity) ? tag : null)) != null && lst.SelectedItem == val)
		{
			lstSelectionChangedDisable = true;
			lst.SelectedIndex = -1;
			CurImage = null;
		}
	}
}
