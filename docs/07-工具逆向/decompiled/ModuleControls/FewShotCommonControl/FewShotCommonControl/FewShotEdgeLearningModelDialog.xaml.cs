using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Apps.XmlParser.ParamTab;
using HikExternalCall.Common;
using HikPcUI.Controls;
using HikPcUI.ImageView;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.RenderInterface;

namespace FewShotCommonControl;

public partial class FewShotEdgeLearningModelDialog : System.Windows.Controls.UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private ObservableCollection<RegisterImageEntity> _ImageList = new ObservableCollection<RegisterImageEntity>();

	private int _FreshListCount = 0;

	private int _SelectedItemsCount;

	private int _SelectListViewIndex = 0;

	private int _CurIndex = -1;

	private double _CurHorizontalOffset = 0.0;

	private int _markNum = 0;

	private ImageListType imageListType = ImageListType.All;

	private int _displayImageCount = 0;

	private string _imagePath = "";

	private bool _isRegistering = false;

	private bool _isUpdateOffset = true;

	private ObservableCollection<CategoryData> _CategoryList = new ObservableCollection<CategoryData>();

	private bool _isExportToModel = false;

	private RoiParameter _SelectedRoi = null;

	private bool isSettingParam = false;

	private bool hasModel = false;

	private List<IList> _ROILists;

	private Apps.XmlParser.ParamTab.Boolean angleEnable;

	private Integer epoch;

	private Apps.XmlParser.ParamTab.Boolean colorEnhancementEnable;

	private Apps.XmlParser.ParamTab.Boolean flipEnhancementEnable;

	private Apps.XmlParser.ParamTab.Boolean rotateEnhancementEnable;

	private Apps.XmlParser.ParamTab.Boolean zoomEnhancementEnable;

	private int IMAGE_MAXCOUNT = 10;

	private int CLASS_MAXCOUNT = 10;

	private int SINGLEIMAGE_ROIMAXCOUNT = 500;

	public bool _IsExpand = false;

	public static readonly DependencyProperty CurImageProperty = DependencyProperty.Register("CurImage", typeof(ImageData), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata((object)null));

	public static readonly DependencyProperty IsDrawRectProperty = DependencyProperty.Register("IsDrawRect", typeof(bool), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata(false));

	public static readonly DependencyProperty IsDrawPolygonProperty = DependencyProperty.Register("IsDrawPolygon", typeof(bool), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata(false));

	public static readonly DependencyProperty ROIHitTestVisibleProperty = DependencyProperty.Register("ROIHitTestVisible", typeof(bool), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata(true));

	public static readonly DependencyProperty RoisProperty = DependencyProperty.Register("Rois", typeof(ObservableCollection<object>), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata(new ObservableCollection<object>()));

	public static readonly DependencyProperty IsStateForDrawROIProperty = DependencyProperty.Register("IsStateForDrawROI", typeof(bool), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata(false));

	public static readonly DependencyProperty DrawROITypeProperty = DependencyProperty.Register("DrawROIType", typeof(Type), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata(null));

	public static readonly DependencyProperty SelectedCategoryProperty = DependencyProperty.Register("SelectedCategory", typeof(CategoryData), typeof(FewShotEdgeLearningModelDialog), new PropertyMetadata((object)null));

	private static List<string> moreOptions = new List<string> { "当前图像", "缓存图像", "导入本地图像", "导入文件夹" };

	private List<string> comboOptions = new List<string>
	{
		AppLocalizationService.GetLang("All"),
		AppLocalizationService.GetLang("Lang_MarkedImages"),
		AppLocalizationService.GetLang("Lang_UnmarkedImages")
	};

	private DelegateCommand freshOptionsCommand = null;

	private DelegateCommand<object> clickCommand = null;

	private ICommand _AddROICommand;

	private ICommand _SelectedROICommand;

	private ICommand _CopyRoiCommand = null;

	private ICommand _RoiPasteCommand = null;

	private ICommand _DeleteRoiCommand = null;

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

	public Integer Epoch
	{
		get
		{
			if (epoch == null)
			{
				epoch = new Integer
				{
					Name = "Epoch",
					AccessMode = "O",
					SubscribeVisible = false,
					CurValue = 100,
					DefaultValue = 100,
					MinValue = 1,
					MaxValue = 500,
					IncValue = 1
				};
				epoch.OnCurValueChanged += OnParamCurValueChanged;
			}
			return epoch;
		}
	}

	public Apps.XmlParser.ParamTab.Boolean ColorEnhancementEnable
	{
		get
		{
			if (colorEnhancementEnable == null)
			{
				colorEnhancementEnable = new Apps.XmlParser.ParamTab.Boolean
				{
					Name = "ColorEnhanceEnable",
					AccessMode = "O",
					CurValue = true,
					DefaultValue = true
				};
				colorEnhancementEnable.OnCurValueChanged += OnParamCurValueChanged;
			}
			return colorEnhancementEnable;
		}
	}

	public Apps.XmlParser.ParamTab.Boolean FlipEnhancementEnable
	{
		get
		{
			if (flipEnhancementEnable == null)
			{
				flipEnhancementEnable = new Apps.XmlParser.ParamTab.Boolean
				{
					Name = "FlipEnhanceEnable",
					AccessMode = "O",
					CurValue = true,
					DefaultValue = true
				};
				flipEnhancementEnable.OnCurValueChanged += OnParamCurValueChanged;
			}
			return flipEnhancementEnable;
		}
	}

	public Apps.XmlParser.ParamTab.Boolean RotateEnhancementEnable
	{
		get
		{
			if (rotateEnhancementEnable == null)
			{
				rotateEnhancementEnable = new Apps.XmlParser.ParamTab.Boolean
				{
					Name = "RotateEnhanceEnable",
					AccessMode = "O",
					CurValue = false,
					DefaultValue = false
				};
				rotateEnhancementEnable.OnCurValueChanged += OnParamCurValueChanged;
			}
			return rotateEnhancementEnable;
		}
	}

	public Apps.XmlParser.ParamTab.Boolean ZoomEnhancementEnable
	{
		get
		{
			if (zoomEnhancementEnable == null)
			{
				zoomEnhancementEnable = new Apps.XmlParser.ParamTab.Boolean
				{
					Name = "ResizeEnhanceEnable",
					AccessMode = "O",
					CurValue = false,
					DefaultValue = false
				};
				zoomEnhancementEnable.OnCurValueChanged += OnParamCurValueChanged;
			}
			return zoomEnhancementEnable;
		}
	}

	public int ParamListHeight => IsExpand ? 280 : 105;

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

	public bool IsExpand
	{
		get
		{
			return _IsExpand;
		}
		set
		{
			try
			{
				if (_IsExpand != value)
				{
					_IsExpand = value;
					OnPropertyChanged("IsExpand");
					OnPropertyChanged("ParamListHeight");
				}
				((IUserStringData)EdgeLearningData.Instance.Param)["CustomConfigEnable"] = _IsExpand.ToString();
			}
			catch (VmException ex)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
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

	public ObservableCollection<RegisterImageEntity> ImageList => _ImageList;

	public ObservableCollection<RegisterImageEntity> DisplayImageList
	{
		get
		{
			if (imageListType == ImageListType.All)
			{
				DisplayImageCount = _ImageList.Count;
				return _ImageList;
			}
			ObservableCollection<RegisterImageEntity> observableCollection = new ObservableCollection<RegisterImageEntity>();
			switch (imageListType)
			{
			case ImageListType.MarkImage:
				foreach (RegisterImageEntity image in ImageList)
				{
					if (!string.IsNullOrWhiteSpace(((ImageEntity)image).ImageLabel))
					{
						observableCollection.Add(image);
					}
				}
				break;
			case ImageListType.UnMarkImage:
				foreach (RegisterImageEntity image2 in ImageList)
				{
					if (string.IsNullOrWhiteSpace(((ImageEntity)image2).ImageLabel))
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

	public int AllImageCount => _ImageList.Count;

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

	public bool IsDrawPolygon
	{
		get
		{
			return (bool)GetValue(IsDrawPolygonProperty);
		}
		set
		{
			SetValue(IsDrawPolygonProperty, value);
		}
	}

	public bool ROIHitTestVisible
	{
		get
		{
			return (bool)GetValue(ROIHitTestVisibleProperty);
		}
		set
		{
			SetValue(ROIHitTestVisibleProperty, value);
		}
	}

	public ObservableCollection<object> Rois
	{
		get
		{
			return (ObservableCollection<object>)GetValue(RoisProperty);
		}
		set
		{
			SetValue(RoisProperty, value);
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

	public int CurImageROINum
	{
		get
		{
			if (lst.SelectedItem is RegisterImageEntity registerImageEntity)
			{
				return registerImageEntity.RoiList.Count;
			}
			return 0;
		}
	}

	public double PageLeftWidth => EdgeLearningData.Instance.IsEnglish ? 575 : 590;

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

	public IList<CategoryData> DisplayCategoryList => CategoryList;

	public object ROILists
	{
		get
		{
			if (_ROILists == null)
			{
				_ROILists = new List<IList>();
				_ROILists.Add(Rois);
			}
			return _ROILists;
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

	public ICommand AddROICommand => _AddROICommand ?? (_AddROICommand = (ICommand)new DelegateCommand<object>((Action<object>)AddROICommandExecute));

	public ICommand SelectedROICommand => _SelectedROICommand ?? (_SelectedROICommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)SelectedROICommandExecute));

	public ICommand CopyRoiCommand => _CopyRoiCommand ?? (_CopyRoiCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)CopyRoiCommandExecute, (Func<RoiWithTagEventArgs, bool>)CanCopyRoiCommandExecute, false));

	public ICommand RoiPasteCommand => _RoiPasteCommand ?? (_RoiPasteCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiPasteCommandExecute));

	public ICommand DeleteRoiCommand => _DeleteRoiCommand ?? (_DeleteRoiCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)DeleteRoiCommandExecute));

	public event PropertyChangedEventHandler PropertyChanged;

	public FewShotEdgeLearningModelDialog()
	{
		InitializeComponent();
		try
		{
			isSettingParam = true;
			string value = ((IUserStringData)EdgeLearningData.Instance.Param)["EnableModelAngle"];
			angleEnable.CurValueA = Convert.ToBoolean(value);
			OnPropertyChanged("EnableModelAngle");
			string value2 = ((IUserStringData)EdgeLearningData.Instance.Param)["CustomConfigEnable"];
			IsExpand = Convert.ToBoolean(value2);
			OnPropertyChanged("ParamListHeight");
			string s = ((IUserStringData)EdgeLearningData.Instance.Param)["Epoch"];
			epoch.CurValueA = int.Parse(s);
			OnPropertyChanged("Epoch");
			string value3 = ((IUserStringData)EdgeLearningData.Instance.Param)["ColorEnhanceEnable"];
			colorEnhancementEnable.CurValueA = Convert.ToBoolean(value3);
			OnPropertyChanged("ColorEnhancementEnable");
			string value4 = ((IUserStringData)EdgeLearningData.Instance.Param)["FlipEnhanceEnable"];
			flipEnhancementEnable.CurValueA = Convert.ToBoolean(value4);
			OnPropertyChanged("FlipEnhancementEnable");
			string value5 = ((IUserStringData)EdgeLearningData.Instance.Param)["RotateEnhanceEnable"];
			rotateEnhancementEnable.CurValueA = Convert.ToBoolean(value5);
			OnPropertyChanged("RotateEnhancementEnable");
			string value6 = ((IUserStringData)EdgeLearningData.Instance.Param)["ResizeEnhanceEnable"];
			zoomEnhancementEnable.CurValueA = Convert.ToBoolean(value6);
			OnPropertyChanged("ZoomEnhancementEnable");
		}
		catch (VmException ex)
		{
			string text = "Get mode or param type error, " + AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			LogHelper.moduleLog.Error(text);
		}
		finally
		{
			isSettingParam = false;
		}
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private void OnParamCurValueChanged(object sender, EventArgs e)
	{
		if (isSettingParam || !(sender is NameValueItem nameValueItem))
		{
			return;
		}
		isSettingParam = true;
		try
		{
			byte[] array = EdgeLearningData.Instance.OwnerModule.UserData["HasModel"];
			bool result = false;
			if (array != null)
			{
				bool.TryParse(StringUtil.UTF8GetString(array), out result);
				HasModel = result;
			}
			if (HasModel && HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDeleteCurModel"), MessageButtons.OkOrCancel, MessageIcons.Question) != VM.Controls.DialogResult.Ok)
			{
				if (sender is Apps.XmlParser.ParamTab.Boolean boolean)
				{
					boolean.CurValueA = !boolean.CurValueA;
				}
				if (sender is Integer integer)
				{
					int result2 = 0;
					int.TryParse(((IUserStringData)EdgeLearningData.Instance.OwnerModule.Params)["Epoch"], out result2);
					integer.CurValueA = result2;
				}
			}
			else
			{
				((IUserStringData)EdgeLearningData.Instance.Param)[nameValueItem.NameA] = nameValueItem.Value;
				if (HasModel)
				{
					DeleteGallery();
				}
			}
		}
		catch (VmException ex)
		{
			HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
		}
		catch (Exception ex2)
		{
			int result3 = 0;
			string strMsg = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result3) ? ErrorCodeInfo.GetErrorInfo(result3) : ex2.Message);
			HikMessageBox.Show(strMsg);
		}
		finally
		{
			isSettingParam = false;
		}
	}

	private void DeleteGallery()
	{
		try
		{
			((IUserStringData)EdgeLearningData.Instance.Param)["GalleryDelete"] = "";
			if (ImageList.Count != 0)
			{
				List<string> list = new List<string>();
				bool flag = false;
				foreach (RegisterImageEntity image in ImageList)
				{
					if (((ImageEntity)image).IsOKCalibrated)
					{
						((ImageEntity)image).IsOKCalibrated = false;
						image.RoiList.ToList().ForEach((RoiParameter p) =>
						{
							p.IsROIRegister = false;
						});
						List<string> list2 = new List<string>();
						foreach (RoiParameter roiItem in image.RoiList)
						{
							CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == roiItem.Lable);
							if (categoryData != null)
							{
								if (!list2.Contains(roiItem.Lable))
								{
									categoryData.CalibratedNum++;
									if (!string.IsNullOrEmpty(roiItem.Lable) && !list.Contains(roiItem.Lable))
									{
										list.Add(roiItem.Lable);
									}
								}
								categoryData.CalibratedROINum++;
								categoryData.RegisteredNum = 0u;
								categoryData.RegisteredROINum = 0u;
							}
							list2.Add(roiItem.Lable);
							if (string.IsNullOrEmpty(roiItem.Lable))
							{
								flag = true;
							}
						}
						continue;
					}
					foreach (CategoryData category in CategoryList)
					{
						category.RegisteredNum = 0u;
						category.RegisteredROINum = 0u;
					}
				}
				if (flag)
				{
					string text = string.Empty;
					byte[] array = EdgeLearningData.Instance.OwnerModule.UserData["RegisterClassNames"];
					if (array != null)
					{
						text = Encoding.UTF8.GetString(array);
					}
					char[] separator = new char[1] { ';' };
					string[] array2 = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					if (list.Count != array2.Length)
					{
						List<string> list3 = array2.Except(list).ToList();
						foreach (string item in list3)
						{
							CategoryData categoryData2 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == item);
							if (categoryData2 != null)
							{
								categoryData2.RegisteredNum = 0u;
								categoryData2.RegisteredROINum = 0u;
							}
						}
					}
				}
			}
			else
			{
				foreach (CategoryData category2 in CategoryList)
				{
					category2.CalibratedNum = 0u;
					category2.RegisteredNum = 0u;
					category2.CalibratedROINum = 0u;
					category2.RegisteredROINum = 0u;
				}
			}
			HasModel = false;
			EdgeLearningData.Instance.OwnerModule.UserData["HasModel"] = Encoding.UTF8.GetBytes(HasModel.ToString());
			EdgeLearningData.Instance.OwnerModule.UserData["RegisterClassNames"] = null;
		}
		catch (VmException ex)
		{
			LogHelper.moduleLog.Error(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
		}
		catch (Exception ex2)
		{
			LogHelper.moduleLog.Error(ex2.Message);
		}
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
			GC.Collect();
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
		if (lst.SelectedIndex + 1 == lst.Items.Count && ((ImageEntity)ImageList[lst.SelectedIndex]).IsCalibrated)
		{
			SelectListViewIndex = lst.SelectedIndex + 1;
		}
	}

	private void lst_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (lst != null)
		{
			System.Windows.Controls.ListView listView = lst;
			if (listView != null && listView.SelectedIndex >= 0)
			{
				RegisterImageEntity lastImage = null;
				if (_CurIndex >= 0 && _CurIndex < DisplayImageList.Count)
				{
					lastImage = DisplayImageList[_CurIndex];
				}
				_CurIndex = lst.SelectedIndex;
				UpdateDisplayInfo(lastImage);
				OnPropertyChanged("CurIndex");
			}
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

	private void Register_OnClick(object sender, RoutedEventArgs e)
	{
		if (EdgeLearningData.Instance.Param != null)
		{
			if (ImageList.Count == 1 && ImageList[0].RoiList.Count == 0)
			{
				HikMessageBox.Show("请至少绘制并标记一个ROI才能注册", MessageIcons.Warning, isTranslate: true);
			}
			else if (!IsROIHasLable())
			{
				HikMessageBox.Show("绘制的ROI必须被标记后才能注册", MessageIcons.Warning, isTranslate: true);
			}
			else if (!IsHasROI())
			{
				HikMessageBox.Show("至少标记一张未注册的图像", MessageIcons.Warning, isTranslate: true);
			}
			else if (!_isRegistering)
			{
				_isRegistering = true;
				RegisterAsync();
			}
		}
	}

	private bool IsHasROI()
	{
		bool flag = true;
		bool flag2 = false;
		foreach (RegisterImageEntity image in ImageList)
		{
			if (image.RoiList.Count == 0)
			{
				flag = false;
			}
			else
			{
				flag2 = true;
			}
		}
		if (!flag2 && !flag)
		{
			return false;
		}
		return true;
	}

	private bool IsROIHasLable()
	{
		foreach (RegisterImageEntity image in ImageList)
		{
			foreach (RoiParameter roi in image.RoiList)
			{
				if (string.IsNullOrEmpty(roi.Lable))
				{
					return false;
				}
			}
		}
		return true;
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

	private void Category_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Border { DataContext: CategoryData dataContext }))
		{
			return;
		}
		if (!(lst.SelectedItem is RegisterImageEntity registerImageEntity))
		{
			return;
		}
		if (registerImageEntity.RoiList.Count == 0)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("至少绘制一个ROI"), MessageIcons.Warning);
			return;
		}
		if (_SelectedRoi == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NoSelectedROI"), MessageIcons.Warning);
			return;
		}
		if (CurImageROINum > SINGLEIMAGE_ROIMAXCOUNT)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit") + "：" + SINGLEIMAGE_ROIMAXCOUNT, MessageIcons.Error);
			return;
		}
		string oldCategoryName = _SelectedRoi.Lable;
		string curCategoryName = dataContext.CategoryName;
		if (!(oldCategoryName != dataContext.CategoryName))
		{
			return;
		}
		if (Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => !string.IsNullOrWhiteSpace(p.Lable)) == 0)
		{
			MarkNum++;
			((ImageEntity)registerImageEntity).ImageLabel = AppLocalizationService.GetLang("Lang_Marked");
			dataContext.CalibratedNum++;
			dataContext.LabelText = "已标为此类";
			dataContext.CalibratedROINum++;
			((ImageEntity)registerImageEntity).IsOKCalibrated = false;
		}
		else
		{
			((ImageEntity)registerImageEntity).IsCalibrated = false;
			if (((ImageEntity)registerImageEntity).IsOKCalibrated)
			{
				List<string> list = new List<string>();
				foreach (RoiParameter roi in registerImageEntity.RoiList)
				{
					if (!string.IsNullOrWhiteSpace(roi.Lable))
					{
						CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == roi.Lable);
						if (categoryData != null)
						{
							categoryData.CalibratedROINum++;
						}
						if (!list.Contains(roi.Lable))
						{
							categoryData.CalibratedNum++;
							list.Add(roi.Lable);
						}
					}
				}
				if (Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == curCategoryName) == 0)
				{
					dataContext.CalibratedNum++;
				}
				dataContext.CalibratedROINum++;
				((ImageEntity)registerImageEntity).IsOKCalibrated = false;
			}
			else
			{
				CategoryData categoryData2 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == curCategoryName);
				if (categoryData2 != null)
				{
					if (Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == curCategoryName) == 0)
					{
						categoryData2.CalibratedNum++;
					}
					categoryData2.LabelText = "已标为此类";
					categoryData2.CalibratedROINum++;
				}
			}
			if (!string.IsNullOrWhiteSpace(oldCategoryName))
			{
				CategoryData categoryData3 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == oldCategoryName);
				if (categoryData3 != null)
				{
					categoryData3.LabelText = "标记为此类";
					categoryData3.CalibratedROINum--;
					if (Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == oldCategoryName) == 1)
					{
						categoryData3.CalibratedNum--;
					}
				}
			}
		}
		_SelectedRoi.Lable = dataContext.CategoryName;
		_SelectedRoi.RoiColor = dataContext.LabelColor;
		_SelectedRoi.IsROIRegister = false;
	}

	private void AddCategory_Click(object sender, RoutedEventArgs e)
	{
		if (CategoryList.Count >= CLASS_MAXCOUNT)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageClassAdd"), AppLocalizationService.GetLang("Lang_ImageClassLimitTip") + "：" + CLASS_MAXCOUNT, MessageIcons.Error);
			return;
		}
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
				CategoryList.Add(newCategory);
				flag = true;
			}
		}
		if (flag)
		{
			OnPropertyChanged("DisplayCategoryList");
		}
	}

	private void FullScreen_Click(object sender, RoutedEventArgs e)
	{
		FullROI();
		if (lst.SelectedItem is RegisterImageEntity registerImageEntity)
		{
			DeleteImageTag(registerImageEntity);
			registerImageEntity.RoiList.Clear();
		}
	}

	private void RectROI_Click(object sender, RoutedEventArgs e)
	{
		if (CurImageROINum >= SINGLEIMAGE_ROIMAXCOUNT)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit") + "：" + SINGLEIMAGE_ROIMAXCOUNT, MessageIcons.Error);
			return;
		}
		IsDrawPolygon = false;
		IsDrawRect = true;
		DrawROIType = typeof(HikCoverRoiParameter);
		IsStateForDrawROI = true;
	}

	private void PolygonROI_Click(object sender, RoutedEventArgs e)
	{
		if (CurImageROINum >= SINGLEIMAGE_ROIMAXCOUNT)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit") + "：" + SINGLEIMAGE_ROIMAXCOUNT, MessageIcons.Error);
			return;
		}
		IsDrawRect = false;
		IsDrawPolygon = true;
		DrawROIType = typeof(HikPolygonRoiParameter);
		IsStateForDrawROI = true;
	}

	private void EditCategory_Click(object sender, RoutedEventArgs e)
	{
		System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
		CategoryData category;
		if ((category = button.DataContext as CategoryData) == null)
		{
			return;
		}
		string oldCategoryName = category.CategoryName;
		string labelColor = category.LabelColor;
		category.IsEdit = true;
		EditCategoryDialog editCategoryDialog = new EditCategoryDialog();
		editCategoryDialog.DataContext = category;
		editCategoryDialog.Init(CategoryList);
		category.SetParentNode(editCategoryDialog);
		editCategoryDialog.ShowDialog();
		category.IsEdit = false;
		if (editCategoryDialog.DialogResult == true)
		{
			if (!(oldCategoryName != category.CategoryName) && !(labelColor != category.LabelColor))
			{
				return;
			}
			{
				foreach (RegisterImageEntity image in ImageList)
				{
					IEnumerable<RoiParameter> enumerable = image.RoiList.Where((RoiParameter p) => p.Lable == oldCategoryName);
					if (enumerable != null && enumerable.Count() > 0)
					{
						enumerable.ToList().ForEach((RoiParameter q) =>
						{
							q.Lable = category.CategoryName;
							q.RoiColor = category.LabelColor;
							q.IsROIRegister = false;
						});
					}
				}
				return;
			}
		}
		category.CategoryName = oldCategoryName;
		category.LabelColor = labelColor;
	}

	private void ClearCategoryMark_Click(object sender, RoutedEventArgs e)
	{
		System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
		if (button.DataContext is CategoryData category)
		{
			VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_EdgeLearningClearCategoryTagWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
			{
				ClearCategoryMark(category);
			}
		}
	}

	private void DeleteCategory_Click(object sender, RoutedEventArgs e)
	{
		if (EdgeLearningData.Instance.Param == null)
		{
			return;
		}
		System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
		if (!(button.DataContext is CategoryData categoryData))
		{
			return;
		}
		try
		{
			string text = string.Empty;
			byte[] array = EdgeLearningData.Instance.OwnerModule.UserData["RegisterClassNames"];
			if (array != null)
			{
				text = Encoding.UTF8.GetString(array);
			}
			byte[] array2 = EdgeLearningData.Instance.OwnerModule.UserData["HasModel"];
			if (array2 != null)
			{
				string text2 = StringUtil.UTF8GetString(array2);
				if (text2 == "True" && text.Contains(categoryData.CategoryName))
				{
					VM.Controls.DialogResult dialogResult = HikMessageBox.Show("Lang_DeleteCategory", "Lang_EdgeLearningDeleteCategoryAndModelTip", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
					if (dialogResult == VM.Controls.DialogResult.Yes || dialogResult == VM.Controls.DialogResult.SDKModel)
					{
						ClearCategoryMark(categoryData);
						if (CategoryList.Contains(categoryData))
						{
							CategoryList.Remove(categoryData);
						}
						OnPropertyChanged("DisplayCategoryList");
						((IUserStringData)EdgeLearningData.Instance.Param)["GalleryDelete"] = "";
						HasModel = false;
						EdgeLearningData.Instance.OwnerModule.UserData["HasModel"] = Encoding.UTF8.GetBytes(HasModel.ToString());
					}
					return;
				}
			}
			VM.Controls.DialogResult dialogResult2 = HikMessageBox.Show("Lang_DeleteCategory", "Lang_EdgeLearningDeleteCategoryTip", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult2 == VM.Controls.DialogResult.Yes || dialogResult2 == VM.Controls.DialogResult.SDKModel)
			{
				ClearCategoryMark(categoryData);
				if (CategoryList.Contains(categoryData))
				{
					CategoryList.Remove(categoryData);
				}
				OnPropertyChanged("DisplayCategoryList");
			}
		}
		catch (VmException ex)
		{
			LogHelper.moduleLog.Error(ErrorCodeInfo.GetErrorInfo(ex.errorCode));
		}
		catch (Exception ex2)
		{
			LogHelper.moduleLog.Error(ex2.Message);
		}
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
				if (Enumerable.Count(_ImageList) >= IMAGE_MAXCOUNT)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + IMAGE_MAXCOUNT, MessageButtons.Normal, MessageIcons.Warning);
				}
				else
				{
					bool flag = false;
					EdgeLearningData.Instance.OnSetWindowShadowEvent(bVisible: true);
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
					if (flag && lst.SelectedIndex < 0)
					{
						lst.SelectedIndex = 0;
					}
					EdgeLearningData.Instance.OnSetWindowShadowEvent(bVisible: false);
				}
			}
		}));
	}

	protected virtual void AddROICommandExecute(object roiParam)
	{
		Arrow.IsChecked = true;
		IsStateForDrawROI = false;
		if (lst.SelectedItem is RegisterImageEntity registerImageEntity)
		{
			registerImageEntity.RoiList.Add(roiParam as RoiParameter);
		}
		if (roiParam is IRectROI)
		{
			IsDrawRect = false;
		}
		else if (roiParam is IPolygonROI)
		{
			IsDrawPolygon = false;
		}
		(roiParam as RoiParameter).IsSelected = true;
	}

	private void SelectedROICommandExecute(RoiWithTagEventArgs roiParam)
	{
		SelectedRoi(roiParam.Roi as RoiParameter);
	}

	private bool CanCopyRoiCommandExecute(RoiWithTagEventArgs args)
	{
		if (_SelectedRoi.RoiType != "HikCoverRoi")
		{
			MethodInfo method = typeof(HikPolygonRoiParameter).GetMethod("BeforSaveBaseNode", BindingFlags.Instance | BindingFlags.NonPublic);
			if (method != null)
			{
				method.Invoke(_SelectedRoi, null);
			}
		}
		return true;
	}

	private void CopyRoiCommandExecute(RoiWithTagEventArgs args)
	{
	}

	private void RoiPasteCommandExecute(RoiWithTagEventArgs args)
	{
		RegisterImageEntity registerImageEntity = lst.SelectedItem as RegisterImageEntity;
		if (registerImageEntity != null && registerImageEntity.RoiList.Count >= SINGLEIMAGE_ROIMAXCOUNT)
		{
			if (args.Roi is RoiParameter roiParameter)
			{
				registerImageEntity.RoiList.Add(roiParameter);
				roiParameter.Lable = "";
				roiParameter.RoiColor = "#0F6AF3";
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrawROI"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit") + "：" + SINGLEIMAGE_ROIMAXCOUNT, MessageIcons.Error);
			}
			return;
		}
		AddROICommandExecute(args.Roi);
		RoiParameter roiParam = args.Roi as RoiParameter;
		if (roiParam == null || string.IsNullOrWhiteSpace(roiParam.Lable))
		{
			return;
		}
		CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == roiParam.Lable);
		if (categoryData == null)
		{
			return;
		}
		((ImageEntity)registerImageEntity).IsCalibrated = false;
		if (((ImageEntity)registerImageEntity).IsOKCalibrated)
		{
			string categoryName = roiParam.Lable;
			if (!string.IsNullOrWhiteSpace(categoryName))
			{
				uint num = (uint)Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == categoryName);
				if (categoryData != null && num != 0)
				{
					categoryData.CalibratedNum++;
					categoryData.CalibratedROINum += num;
					categoryData.LabelText = "标记为此类";
				}
			}
			List<string> list = new List<string>();
			foreach (RoiParameter item in registerImageEntity.RoiList)
			{
				if (!(item.Lable == categoryName) && !list.Contains(item.Lable))
				{
					CategoryData categoryData2 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == item.Lable);
					uint num2 = (uint)Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == item.Lable);
					if (categoryData2 != null)
					{
						categoryData2.CalibratedNum++;
						categoryData2.CalibratedROINum += num2;
					}
					list.Add(item.Lable);
				}
			}
			((ImageEntity)registerImageEntity).IsOKCalibrated = false;
		}
		else
		{
			if (Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == roiParam.Lable) == 1)
			{
				categoryData.CalibratedNum++;
				((ImageEntity)registerImageEntity).ImageLabel = AppLocalizationService.GetLang("Lang_Marked");
			}
			categoryData.LabelText = "已标为此类";
			categoryData.CalibratedROINum++;
		}
	}

	private void DeleteRoiCommandExecute(RoiWithTagEventArgs args)
	{
		RoiParameter roiParameter = args.Roi as RoiParameter;
		RegisterImageEntity registerImageEntity = lst.SelectedItem as RegisterImageEntity;
		if (roiParameter == null || registerImageEntity == null)
		{
			return;
		}
		string categoryName = roiParameter.Lable;
		registerImageEntity.RoiList.Remove(roiParameter);
		if (((ImageEntity)registerImageEntity).IsOKCalibrated)
		{
			if (!string.IsNullOrWhiteSpace(categoryName))
			{
				CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == categoryName);
				uint num = (uint)Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == categoryName);
				if (categoryData != null && num != 0)
				{
					categoryData.CalibratedNum++;
					categoryData.CalibratedROINum += num;
					categoryData.LabelText = "标记为此类";
				}
			}
			List<string> list = new List<string>();
			foreach (RoiParameter item in registerImageEntity.RoiList)
			{
				if (!(item.Lable == categoryName) && !list.Contains(item.Lable))
				{
					CategoryData categoryData2 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == item.Lable);
					uint num2 = (uint)Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == item.Lable);
					if (categoryData2 != null)
					{
						categoryData2.CalibratedNum++;
						categoryData2.CalibratedROINum += num2;
					}
					list.Add(item.Lable);
				}
			}
		}
		else if (!string.IsNullOrWhiteSpace(categoryName))
		{
			CategoryData categoryData3 = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == categoryName);
			if (categoryData3 != null)
			{
				if (categoryData3.CalibratedNum != 0 && Enumerable.Count(registerImageEntity.RoiList, (RoiParameter p) => p.Lable == categoryName) == 0)
				{
					categoryData3.CalibratedNum--;
				}
				categoryData3.LabelText = "标记为此类";
				if (categoryData3.CalibratedROINum != 0)
				{
					categoryData3.CalibratedROINum--;
				}
			}
		}
		((ImageEntity)registerImageEntity).IsOKCalibrated = false;
		if (registerImageEntity.RoiList.Where((RoiParameter p) => !string.IsNullOrWhiteSpace(p.Lable)).Count() == 0 && !string.IsNullOrWhiteSpace(((ImageEntity)registerImageEntity).ImageLabel))
		{
			((ImageEntity)registerImageEntity).IsCalibrated = false;
			((ImageEntity)registerImageEntity).IsOKCalibrated = false;
			((ImageEntity)registerImageEntity).ImageLabel = "";
			MarkNum--;
		}
		if (registerImageEntity.RoiList.Count != 0)
		{
			registerImageEntity.RoiList.First().IsSelected = true;
		}
	}

	public void Init(ObservableCollection<CategoryData> categoryList)
	{
		if (categoryList != null)
		{
			foreach (CategoryData category in categoryList)
			{
				UICategoryData uICategoryData = EdgeLearningData.Instance.UICategoryList.FirstOrDefault((UICategoryData p) => p.CategoryName == category.CategoryName);
				if (uICategoryData != null)
				{
					category.LabelColor = uICategoryData.LabelColor;
					category.RegisteredNum = uICategoryData.RegisteredNum;
					category.RegisteredROINum = uICategoryData.RegisteredROINum;
					category.CalibratedNum = uICategoryData.CalibratedNum;
					category.CalibratedROINum = uICategoryData.CalibratedROINum;
				}
				CategoryList.Add(category);
			}
		}
		List<UICategoryData> list = new List<UICategoryData>();
		if (!CVmObjectBase.GetIsSDKMode())
		{
			list = EdgeLearningData.Instance.UICategoryList.Where((UICategoryData p) => p.IsEmptyCategory).ToList();
		}
		else if (categoryList.Count == 0 && EdgeLearningData.Instance.UICategoryList.Count != 0)
		{
			list = EdgeLearningData.Instance.UICategoryList;
		}
		if (list != null)
		{
			foreach (UICategoryData item in list)
			{
				if (CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == item.CategoryName) == null)
				{
					CategoryData categoryData = new CategoryData
					{
						CategoryName = item.CategoryName,
						CalibratedNum = item.CalibratedNum,
						CalibratedROINum = item.CalibratedROINum,
						RegisteredNum = item.RegisteredNum,
						RegisteredROINum = item.RegisteredROINum
					};
					categoryData.LabelColor = item.LabelColor;
					CategoryList.Add(categoryData);
				}
			}
		}
		IsExportToModel = EdgeLearningData.Instance.IsExportToModel;
		EdgeLearningData.Instance.DeleteImageEvent += DeleteImage;
		EdgeLearningData.Instance.DeleteImageTagEvent += DeleteImageTag;
		LoadImages();
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
				return imageData;
			}
		}
		return null;
	}

	public void DeInit()
	{
		EdgeLearningData.Instance.DeleteImageEvent -= DeleteImage;
		EdgeLearningData.Instance.DeleteImageTagEvent -= DeleteImageTag;
		if (imageView != null)
		{
			imageView.DataContext = null;
		}
		EdgeLearningData.Instance.SampleImageDataList.Clear();
		foreach (RegisterImageEntity image in ImageList)
		{
			SampleImageData sampleImageData = new SampleImageData();
			sampleImageData.ImagePath = ((ImageEntity)image).ImagePath;
			sampleImageData.IsCalibrated = ((ImageEntity)image).IsCalibrated;
			sampleImageData.IsOKCalibrated = ((ImageEntity)image).IsOKCalibrated;
			sampleImageData.ImageLabel = ((ImageEntity)image).ImageLabel;
			if (image.RoiList.Count > 0)
			{
				sampleImageData.RoiData = GetRoiBuffer(image.RoiList);
			}
			sampleImageData.ImageBuffer = File.ReadAllBytes(((ImageEntity)image).ImagePath);
			EdgeLearningData.Instance.SampleImageDataList.Add(sampleImageData);
		}
		EdgeLearningData.Instance.UICategoryList.Clear();
		foreach (CategoryData category in CategoryList)
		{
			UICategoryData uICategoryData = new UICategoryData();
			uICategoryData.CategoryName = category.CategoryName;
			uICategoryData.LabelColor = category.LabelColor;
			uICategoryData.IsEmptyCategory = category.RegisteredNum == 0;
			uICategoryData.RegisteredNum = category.RegisteredNum;
			uICategoryData.RegisteredROINum = category.RegisteredROINum;
			uICategoryData.CalibratedNum = category.CalibratedNum;
			uICategoryData.CalibratedROINum = category.CalibratedROINum;
			EdgeLearningData.Instance.UICategoryList.Add(uICategoryData);
		}
		ClearMarkData();
		DataContext = null;
	}

	private bool IsFullVisible()
	{
		bool result = true;
		if (lst != null && lst.SelectedItem is RegisterImageEntity item)
		{
			FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(item);
			result = IsFullVisibleInParent(child, listScroll);
		}
		return result;
	}

	private void ScrollToRight()
	{
		if (lst.SelectedItem is RegisterImageEntity item)
		{
			FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(item);
			if (!IsFullVisibleInParent(child, listScroll))
			{
				_CurHorizontalOffset += GetHorizontalOffset(child, listScroll);
				listScroll.ScrollToHorizontalOffset(_CurHorizontalOffset);
			}
		}
	}

	private void ScrollToLeft()
	{
		if (lst.SelectedItem is RegisterImageEntity item)
		{
			FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(item);
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
			foreach (RegisterImageEntity item in (IEnumerable)lst.Items)
			{
				FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(item);
				if (item != null)
				{
					if (IsChildVisibleInParent(child, listScroll))
					{
						if (((ImageEntity)item).ImageSource == null)
						{
							BitmapImage bitmapImage = ImageData.CreateThumbnail(((ImageEntity)item).ImagePath, 126.0, 102.0);
							if (bitmapImage != null)
							{
								((ImageEntity)item).ImageSource = bitmapImage;
							}
						}
					}
					else
					{
						((ImageEntity)item).ImageSource = null;
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

	private void DeleteImageTag(object obj)
	{
		if (!(obj is RegisterImageEntity registerImageEntity))
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (RoiParameter roi in registerImageEntity.RoiList)
		{
			CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == roi.Lable);
			if (categoryData != null)
			{
				if (categoryData.CalibratedNum != 0 && !list.Contains(roi.Lable))
				{
					categoryData.CalibratedNum--;
				}
				categoryData.LabelText = "标记为此类";
				if (categoryData.CalibratedROINum != 0)
				{
					categoryData.CalibratedROINum--;
				}
			}
			list.Add(roi.Lable);
			roi.Lable = "";
			roi.RoiColor = "#0F6AF3";
		}
		((ImageEntity)registerImageEntity).IsCalibrated = false;
		((ImageEntity)registerImageEntity).ImageLabel = "";
		MarkNum--;
	}

	private void DeleteImage(object obj)
	{
		try
		{
			int selectedIndex = lst.SelectedIndex;
			if (obj is RegisterImageEntity registerImageEntity)
			{
				if (!string.IsNullOrWhiteSpace(((ImageEntity)registerImageEntity).ImageLabel))
				{
					DeleteImageTag(registerImageEntity);
				}
				registerImageEntity.RoiList.Clear();
				_ImageList.Remove(registerImageEntity);
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
				Rois.Clear();
				OnPropertyChanged("ROILists");
				CurImage = null;
				IsDrawRect = false;
				IsDrawPolygon = false;
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
			EdgeLearningData.Instance.Owner.ShowProgressDialog("Lang_RegisterProgressText");
			EdgeLearningData.Instance.Param.TimeOut = 2147483647u;
			int count = ImageList.Count;
			EdgeLearningData.Instance.OwnerModule.UserData["ImageNumber"] = BitConverter.GetBytes(count);
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			string strErrorMsg = "";
			int num4 = 0;
			foreach (RegisterImageEntity image in ImageList)
			{
				num++;
				if (image.RoiList.Count != 0)
				{
					((ImageEntity)image).IsOKCalibrated = false;
					if (Enumerable.Count(image.RoiList, (RoiParameter p) => !p.IsROIRegister) > 0 || !((ImageEntity)image).IsOKCalibrated)
					{
						try
						{
							int num5 = ImageList.IndexOf(image);
							List<byte> list = new List<byte>();
							byte[] bytes = BitConverter.GetBytes(1);
							list.AddRange(bytes);
							ImageData imageDataFromFile = ImageData.GetImageDataFromFile(((ImageEntity)image).ImagePath, 0, 0, needGC: true);
							if (imageDataFromFile == null || imageDataFromFile.Buffer == null)
							{
								throw new Exception(AppLocalizationService.GetLang("Lang_LoadingImageError"));
							}
							int width = imageDataFromFile.Width;
							int height = imageDataFromFile.Height;
							byte[] bytes2 = BitConverter.GetBytes(width);
							byte[] bytes3 = BitConverter.GetBytes(height);
							byte[] bytes4 = BitConverter.GetBytes(imageDataFromFile.Buffer.Length);
							list.AddRange(bytes2);
							list.AddRange(bytes3);
							list.AddRange(bytes4);
							list.AddRange(imageDataFromFile.Buffer);
							try
							{
								((IUserStringData)EdgeLearningData.Instance.Param)["GalleryDelete"] = "";
								byte[] value = list.ToArray();
								((IUserBytesData)EdgeLearningData.Instance.Param)["SetRegisterImage"] = value;
								GetRoiBuffer(image, width, height, out var roiDatas);
								if (roiDatas != null)
								{
									((IUserBytesData)EdgeLearningData.Instance.Param)["SetRegisterROIData"] = roiDatas;
								}
								num4 += image.RoiList.Count;
								foreach (RoiParameter roi in image.RoiList)
								{
									((IUserStringData)EdgeLearningData.Instance.Param)["SetRegisterClassName"] = roi.Lable;
									roi.IsROIRegister = true;
								}
								((ImageEntity)image).IsOKCalibrated = true;
								((ImageEntity)image).IsCalibrated = true;
								num2++;
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
							strErrorMsg += $"\r\n{((ImageEntity)image).ImageName} ({ex3.Message})";
							((ImageEntity)image).IsOKCalibrated = false;
							((ImageEntity)image).IsCalibrated = true;
						}
						finally
						{
							num3 = 10 + (int)((float)num / (float)count * 80f);
							EdgeLearningData.Instance.Owner.SetProgressValue(num3);
						}
					}
				}
			}
			if (num2 > 0)
			{
				try
				{
					((IUserStringData)EdgeLearningData.Instance.Param)["GalleryRegister"] = "";
					foreach (RegisterImageEntity image2 in ImageList)
					{
						if (((ImageEntity)image2).IsOKCalibrated)
						{
							foreach (CategoryData category in CategoryList)
							{
								int num6 = Enumerable.Count(image2.RoiList, (RoiParameter p) => p.Lable == category.CategoryName);
								if (num6 > 0)
								{
									if (category.CalibratedNum != 0)
									{
										category.CalibratedNum--;
									}
									if (category.CalibratedROINum != 0 && category.CalibratedROINum >= (uint)num6)
									{
										category.CalibratedROINum -= (uint)num6;
									}
									else
									{
										category.CalibratedROINum = 0u;
									}
								}
							}
						}
					}
				}
				catch (VmException ex4)
				{
					Exception ex5 = new Exception(ErrorCodeInfo.GetErrorInfo(ex4.errorCode));
					strErrorMsg = ex5.Message;
				}
				catch (Exception ex6)
				{
					int result2 = 0;
					string message2 = (int.TryParse(ex6.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex6.Message);
					throw new Exception(message2);
				}
				if (string.IsNullOrEmpty(strErrorMsg))
				{
					Dispatcher.Invoke(() =>
					{
						QueryAllCategory();
					});
				}
			}
			EdgeLearningData.Instance.Param.TimeOut = 3000u;
			EdgeLearningData.Instance.Owner.CloseProgressDialog();
			Dispatcher.Invoke(() =>
			{
				if (string.IsNullOrEmpty(strErrorMsg))
				{
					HasModel = true;
					EdgeLearningData.Instance.OwnerModule.UserData["HasModel"] = Encoding.UTF8.GetBytes(HasModel.ToString());
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RegisterSuccess"), MessageIcons.Ok);
				}
				else
				{
					HasModel = false;
					EdgeLearningData.Instance.OwnerModule.UserData["HasModel"] = Encoding.UTF8.GetBytes(HasModel.ToString());
					foreach (RegisterImageEntity image3 in ImageList)
					{
						((ImageEntity)image3).IsOKCalibrated = false;
						foreach (RoiParameter roi2 in image3.RoiList)
						{
							roi2.IsROIRegister = false;
						}
					}
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RegisterFail"), AppLocalizationService.GetLang("Lang_RegisterFailImageList") + strErrorMsg, MessageIcons.Error);
					LogHelper.moduleLog.Error("Register failure, " + AppLocalizationService.GetLang("Lang_RegisterFailImageList") + strErrorMsg);
				}
			});
			_isRegistering = false;
			GC.Collect();
		});
	}

	private void QueryAllCategory()
	{
		int num = 0;
		string text = string.Empty;
		try
		{
			string empty = string.Empty;
			empty = ((IUserStringData)EdgeLearningData.Instance.Param)["GetLabelTypeNum"];
			num = int.Parse(empty);
			for (int i = 0; i < num; i++)
			{
				string text2 = ((IUserStringData)EdgeLearningData.Instance.Param)["GetLabelType_" + i];
				string text3 = text2 + ";";
				text += text3;
			}
			if (!string.IsNullOrEmpty(text))
			{
				EdgeLearningData.Instance.OwnerModule.UserData["RegisterClassNames"] = Encoding.UTF8.GetBytes(text);
			}
		}
		catch (VmException ex)
		{
			string text4 = AppLocalizationService.GetLang("Lang_GetCategoryCountFail") + "\r\n(";
			text4 += AppLocalizationService.GetLang("errInfo:");
			text4 += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text4 += ")";
			LogHelper.moduleLog.Error(text4);
			return;
		}
		string strCategoryName = string.Empty;
		string empty2 = string.Empty;
		string empty3 = string.Empty;
		char[] separator = new char[1] { ';' };
		string[] array = null;
		if (!string.IsNullOrEmpty(text))
		{
			array = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		}
		if (array.Length == 0 || array.Length != num)
		{
			return;
		}
		for (int j = 0; j < num; j++)
		{
			try
			{
				strCategoryName = array[j];
				CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == strCategoryName);
				if (categoryData != null)
				{
					empty2 = ((IUserStringData)EdgeLearningData.Instance.Param)["GetTrainImageNum_" + j];
					categoryData.RegisteredNum = Convert.ToUInt32(empty2);
					empty3 = ((IUserStringData)EdgeLearningData.Instance.Param)["GetTrainInstanceNum_" + j];
					categoryData.RegisteredROINum = Convert.ToUInt32(empty3);
				}
			}
			catch (VmException ex2)
			{
				LogHelper.moduleLog.Error($"Get category {j} information error: {ErrorCodeInfo.GetErrorInfo(ex2.errorCode)}");
			}
			catch (Exception ex3)
			{
				int result = 0;
				string arg = (int.TryParse(ex3.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex3.Message);
				LogHelper.moduleLog.Error($"Get category {j} information error: {arg}");
			}
		}
		foreach (CategoryData category in CategoryList)
		{
			if (!array.Contains(category.CategoryName))
			{
				category.RegisteredNum = 0u;
				category.RegisteredROINum = 0u;
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

	private void GetRoiBuffer(RegisterImageEntity image, int imageWidth, int imageHeight, out byte[] roiDatas)
	{
		roiDatas = null;
		if (image == null)
		{
			return;
		}
		ObservableCollection<RoiParameter> roiList = image.RoiList;
		if (roiList == null || roiList.Count <= 0)
		{
			return;
		}
		IMVS_POLYGON_ROI_LIST_EL structure = new IMVS_POLYGON_ROI_LIST_EL
		{
			nCount = roiList.Count,
			stPolygonROIs = new IMVS_POLYGON_ROI_EL[500]
		};
		for (int i = 0; i < roiList.Count; i++)
		{
			if (roiList[i] is IPolygonROI)
			{
				ObservableCollection<Point> polygonPoints = (roiList[i] as IPolygonROI).PolygonPoints;
				structure.stPolygonROIs[i] = GetPolygonStruct(polygonPoints, imageWidth, imageHeight);
				continue;
			}
			List<Point> list = new List<Point>();
			IRectROI rectROI = roiList[i] as IRectROI;
			double pointX = rectROI.CenterPoint.X - rectROI.Width / 2.0;
			double pointY = rectROI.CenterPoint.Y + rectROI.Height / 2.0;
			Point item = CalculateRectROIVertex(pointX, pointY, rectROI.CenterPoint, rectROI.Angle);
			list.Add(item);
			double pointX2 = (float)(rectROI.CenterPoint.X + rectROI.Width / 2.0);
			double pointY2 = (float)(rectROI.CenterPoint.Y + rectROI.Height / 2.0);
			Point item2 = CalculateRectROIVertex(pointX2, pointY2, rectROI.CenterPoint, rectROI.Angle);
			list.Add(item2);
			double pointX3 = (float)(rectROI.CenterPoint.X + rectROI.Width / 2.0);
			double pointY3 = (float)(rectROI.CenterPoint.Y - rectROI.Height / 2.0);
			Point item3 = CalculateRectROIVertex(pointX3, pointY3, rectROI.CenterPoint, rectROI.Angle);
			list.Add(item3);
			float num = (float)(rectROI.CenterPoint.X - rectROI.Width / 2.0);
			float num2 = (float)(rectROI.CenterPoint.Y - rectROI.Height / 2.0);
			Point item4 = CalculateRectROIVertex(num, num2, rectROI.CenterPoint, rectROI.Angle);
			list.Add(item4);
			structure.stPolygonROIs[i] = GetPolygonStruct(list, imageWidth, imageHeight);
		}
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			int num3 = Marshal.SizeOf<IMVS_POLYGON_ROI_LIST_EL>();
			intPtr = Marshal.AllocHGlobal(num3);
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
			roiDatas = new byte[num3];
			Marshal.Copy(intPtr, roiDatas, 0, num3);
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

	private IMVS_POLYGON_ROI_EL GetPolygonStruct(IList<Point> points, int width, int height)
	{
		int num = points?.Count ?? 0;
		IMVS_POLYGON_ROI_EL result = new IMVS_POLYGON_ROI_EL
		{
			nPointCount = num,
			stPoints = new HKA_POINT_F[32]
		};
		for (int i = 0; i < num; i++)
		{
			result.stPoints[i] = new HKA_POINT_F
			{
				x = (float)points[i].X,
				y = (float)points[i].Y
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

	private bool IsHasMark()
	{
		return true;
	}

	private void ClearMarkData()
	{
		foreach (RegisterImageEntity image in ImageList)
		{
			((ImageEntity)image).IsCalibrated = false;
			((ImageEntity)image).IsOKCalibrated = false;
			((ImageEntity)image).ImageLabel = "";
			image.RoiList.Clear();
		}
		foreach (CategoryData category in CategoryList)
		{
			category.CalibratedNum = 0u;
			category.LabelText = "标记为此类";
			category.CalibratedROINum = 0u;
		}
		Rois.Clear();
		MarkNum = 0;
		IsDrawRect = false;
		IsDrawPolygon = false;
		_SelectedRoi = null;
		listScroll.ScrollToLeftEnd();
		lst.SelectedIndex = 0;
		_CurHorizontalOffset = 0.0;
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

	private async void DeleteROIHandle()
	{
		await Task.Run(() =>
		{
			foreach (RegisterImageEntity image in _ImageList)
			{
				ImageData imageDataFromFile = ImageData.GetImageDataFromFile(((ImageEntity)image).ImagePath, 0, 0, needGC: true);
				GetRoiBuffer(image, imageDataFromFile.Width, imageDataFromFile.Height, out var _);
			}
		});
	}

	private void UpdateDisplayInfo(RegisterImageEntity lastImage)
	{
		CurImage = null;
		if (!(lst.SelectedItem is RegisterImageEntity registerImageEntity))
		{
			return;
		}
		ImageData imageDataFromFile = ImageData.GetImageDataFromFile(((ImageEntity)registerImageEntity).ImagePath, 0, 0, needGC: true);
		if (imageDataFromFile != null)
		{
			((ImageEntity)registerImageEntity).ImageWidth = imageDataFromFile.Width;
			((ImageEntity)registerImageEntity).ImageHeight = imageDataFromFile.Height;
			CurImage = imageDataFromFile;
			if (registerImageEntity.RoiList.Count > 0)
			{
				Rois.Clear();
				foreach (RoiParameter roi in registerImageEntity.RoiList)
				{
					Rois.Add(roi);
				}
				RoiParameter roiParameter = registerImageEntity.RoiList.FirstOrDefault((RoiParameter p) => p.IsSelected);
				if (roiParameter != null)
				{
					SelectedRoi(roiParameter);
				}
				else
				{
					registerImageEntity.RoiList.First().IsSelected = true;
				}
			}
			else
			{
				FullROI();
			}
			{
				foreach (CategoryData category in CategoryList)
				{
					if (Rois.Where((object p) => (p as RoiParameter).Lable == category.CategoryName).Count() > 0)
					{
						category.LabelText = "已标为此类";
					}
					else
					{
						category.LabelText = "标记为此类";
					}
				}
				return;
			}
		}
		DeleteImage(lst.SelectedItem);
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetImageDataError"), MessageIcons.Error);
	}

	private void FullROI()
	{
		imageView.UnPrepareRawRoi();
		IsDrawRect = false;
		IsDrawPolygon = false;
		_SelectedRoi = null;
		if (Rois.Count > 0)
		{
			Rois.Clear();
		}
	}

	private async void ClearRegisterDataAsync()
	{
		await Task.Run(() =>
		{
			if (EdgeLearningData.Instance.Param != null && Enumerable.Count(CategoryList, (CategoryData p) => p.RegisteredNum != 0) != 0)
			{
				bool flag = true;
				string strErrorMsg = "";
				try
				{
					byte[] value = new byte[1];
					((IUserBytesData)EdgeLearningData.Instance.Param)["ClearAllClass"] = value;
				}
				catch (Exception ex)
				{
					int result = 0;
					strErrorMsg = AppLocalizationService.GetLang("Lang_DeleteCategoryFail") + Environment.NewLine;
					strErrorMsg += AppLocalizationService.GetLang("errInfo:");
					string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
					strErrorMsg += text;
					flag = false;
				}
				EdgeLearningData.Instance.Owner.SetProgressValue(50);
				if (flag)
				{
					int num = 0;
					int num2 = 0;
					int count = CategoryList.Count;
					foreach (CategoryData category in CategoryList)
					{
						num++;
						if (category.RegisteredNum != 0)
						{
							category.CalibratedNum += category.RegisteredNum;
							category.RegisteredNum = 0u;
							IEnumerable<RegisterImageEntity> enumerable = ImageList.Where((RegisterImageEntity p) => ((ImageEntity)p).ImageLabel == category.CategoryName);
							if (enumerable != null)
							{
								foreach (RegisterImageEntity item in enumerable)
								{
									((ImageEntity)item).IsCalibrated = false;
									((ImageEntity)item).IsOKCalibrated = false;
								}
							}
						}
						num2 = 50 + (int)((float)num / (float)count * 50f);
						EdgeLearningData.Instance.Owner.SetProgressValue(num2);
					}
				}
				EdgeLearningData.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					if (!string.IsNullOrEmpty(strErrorMsg))
					{
						HikMessageBox.Show(strErrorMsg, MessageIcons.Error);
						LogHelper.moduleLog.Error($"ClearRegisterDataAsync error: {strErrorMsg}");
					}
				});
			}
		});
	}

	private void ClearCategoryMark(CategoryData category)
	{
		if (category == null)
		{
			return;
		}
		foreach (RegisterImageEntity image in ImageList)
		{
			IEnumerable<RoiParameter> enumerable = image.RoiList.Where((RoiParameter p) => p.Lable == category.CategoryName);
			if (enumerable != null && enumerable.Count() > 0)
			{
				enumerable.ToList().ForEach((RoiParameter q) =>
				{
					q.Lable = "";
					q.RoiColor = "#0F6AF3";
				});
				if (Enumerable.Count(image.RoiList, (RoiParameter p) => !string.IsNullOrWhiteSpace(p.Lable)) == 0)
				{
					((ImageEntity)image).ImageLabel = "";
					MarkNum--;
				}
			}
		}
		category.CalibratedNum = 0u;
		category.LabelText = "标记为此类";
		category.CalibratedROINum = 0u;
	}

	private byte[] GetRoiBuffer(ObservableCollection<RoiParameter> roiList)
	{
		if (roiList == null)
		{
			return null;
		}
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		if (roiList.Count > 0)
		{
			foreach (RoiParameter roi3 in roiList)
			{
				byte[] array = null;
				if (roi3 is HikCoverRoiParameter roi)
				{
					array = roi.GetRoiBytes();
				}
				else if (roi3 is HikPolygonRoiParameter roi2)
				{
					array = roi2.GetRoiBytes();
				}
				if (array != null && array.Length != 0)
				{
					binaryWriter.Write(array);
				}
			}
		}
		else
		{
			binaryWriter.Write((byte)1);
			binaryWriter.Write((byte)0);
		}
		return memoryStream.ToArray();
	}

	private void GetRoiList(byte[] roiBuffer, ObservableCollection<RoiParameter> roiList)
	{
		if (roiBuffer == null || roiList == null)
		{
			return;
		}
		using MemoryStream memoryStream = new MemoryStream(roiBuffer);
		BinaryReader binaryReader = new BinaryReader(memoryStream);
		while (memoryStream.Position < roiBuffer.Length - 1)
		{
			byte b = binaryReader.ReadByte();
			byte b2 = binaryReader.ReadByte();
			switch ((ROIType)b)
			{
			case ROIType.Box:
			{
				int count = Marshal.SizeOf(typeof(ROIBox));
				byte[] array = binaryReader.ReadBytes(count);
				ROIBox rOIBox = ExternalCallHelper.BytesToStruct<ROIBox>(array, 0, array.Length);
				HikCoverRoiParameter hikCoverRoiParameter = new HikCoverRoiParameter();
				hikCoverRoiParameter.CenterX = rOIBox.CenterX;
				hikCoverRoiParameter.CenterY = rOIBox.CenterY;
				hikCoverRoiParameter.Width = rOIBox.Width;
				hikCoverRoiParameter.Height = rOIBox.Height;
				hikCoverRoiParameter.Angle = rOIBox.Angle;
				hikCoverRoiParameter.Lable = binaryReader.ReadString();
				hikCoverRoiParameter.RoiColor = binaryReader.ReadString();
				roiList.Add(hikCoverRoiParameter);
				break;
			}
			case ROIType.Polygon:
			{
				uint num = binaryReader.ReadUInt32();
				HikPolygonRoiParameter hikPolygonRoiParameter = new HikPolygonRoiParameter();
				for (int i = 0; i < num; i++)
				{
					float num2 = binaryReader.ReadSingle();
					float num3 = binaryReader.ReadSingle();
					((IPolygonROI)hikPolygonRoiParameter).PolygonPoints.Add(new Point
					{
						X = num2,
						Y = num3
					});
				}
				hikPolygonRoiParameter.Lable = binaryReader.ReadString();
				hikPolygonRoiParameter.RoiColor = binaryReader.ReadString();
				roiList.Add(hikPolygonRoiParameter);
				break;
			}
			}
		}
	}

	private void SelectedRoi(RoiParameter roiParameter)
	{
		if (roiParameter == null)
		{
			return;
		}
		_SelectedRoi = roiParameter;
		foreach (CategoryData category in CategoryList)
		{
			if (_SelectedRoi.Lable != category.CategoryName)
			{
				category.LabelText = "标记为此类";
			}
			else
			{
				category.LabelText = "已标为此类";
			}
		}
	}

	private bool LoadCurrentImage()
	{
		string newPath = GetNewImageName();
		if (newPath == null)
		{
			return false;
		}
		ImageData curImageData = EdgeLearningData.Instance.CurImageData;
		if (curImageData != null)
		{
			try
			{
				ImageSaveOpenCvHelper.SaveRawImageSyn(newPath, curImageData);
				if (_ImageList.FirstOrDefault((RegisterImageEntity x) => ((ImageEntity)x).ImagePath == newPath) == null)
				{
					ObservableCollection<RegisterImageEntity> imageList = _ImageList;
					RegisterImageEntity registerImageEntity = new RegisterImageEntity();
					((ImageEntity)registerImageEntity).ImagePath = newPath;
					imageList.Add(registerImageEntity);
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
		bool flag = false;
		List<ImageData> cacheImageList = ModuleCommonMethodMgr.Instance.GetCacheImageList(EdgeLearningData.Instance.OwnerModule);
		if (cacheImageList != null && cacheImageList.Count > 0 && Enumerable.Count(_ImageList) < IMAGE_MAXCOUNT)
		{
			foreach (ImageData item in cacheImageList)
			{
				if (Enumerable.Count(_ImageList) >= IMAGE_MAXCOUNT)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + IMAGE_MAXCOUNT, MessageButtons.Normal, MessageIcons.Warning);
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
					if (_ImageList.FirstOrDefault((RegisterImageEntity x) => ((ImageEntity)x).ImagePath == imageName) == null)
					{
						ObservableCollection<RegisterImageEntity> imageList = _ImageList;
						RegisterImageEntity registerImageEntity = new RegisterImageEntity();
						((ImageEntity)registerImageEntity).ImagePath = imageName;
						imageList.Add(registerImageEntity);
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
		string text = ConfigurationHelper.INIGetDefaultOpenPath(EdgeLearningData.Instance.OwnerModule?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff";
		if (openFileDialog.ShowDialog() == false)
		{
			return false;
		}
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), EdgeLearningData.Instance.OwnerModule?.ModuleDir);
		AddImagesAsync(openFileDialog.FileNames);
		return true;
	}

	private bool LoadFolder(bool isCalibrate = false)
	{
		string text = "";
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		string text2 = ConfigurationHelper.INIGetDefaultOpenPath(EdgeLearningData.Instance.OwnerModule?.ModuleDir);
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
		ConfigurationHelper.INISetDefaultOpenPath(text, EdgeLearningData.Instance.OwnerModule?.ModuleDir);
		return true;
	}

	private async void LoadImages()
	{
		await Task.Run(() =>
		{
			EdgeLearningData.Instance.Owner.ShowProgressDialog("正在加载图片");
			int count = EdgeLearningData.Instance.SampleImageDataList.Count;
			int num = 0;
			int num2 = 0;
			try
			{
				string text = "";
				string text2 = "";
				if (IsExportToModel)
				{
					text = GetSavePath();
					CreateFolder(text);
				}
				foreach (SampleImageData image in EdgeLearningData.Instance.SampleImageDataList)
				{
					if (!File.Exists(image.ImagePath) && image.ImageBuffer != null)
					{
						text2 = $"{text}\\{Path.GetFileName(image.ImagePath)}";
						File.WriteAllBytes(text2, image.ImageBuffer);
						image.ImagePath = text2;
					}
					if (File.Exists(image.ImagePath) && _ImageList.FirstOrDefault((RegisterImageEntity x) => ((ImageEntity)x).ImagePath == image.ImagePath) == null)
					{
						Dispatcher.Invoke(() =>
						{
							BitmapImage imageSource = ImageData.CreateThumbnail(image.ImagePath, 104.0, 78.0);
							RegisterImageEntity registerImageEntity = new RegisterImageEntity();
							((ImageEntity)registerImageEntity).ImageSource = imageSource;
							((ImageEntity)registerImageEntity).ImagePath = image.ImagePath;
							RegisterImageEntity registerImageEntity2 = registerImageEntity;
							if (image.RoiData != null)
							{
								GetRoiList(image.RoiData, registerImageEntity2.RoiList);
							}
							if (!string.IsNullOrWhiteSpace(image.ImageLabel))
							{
								IEnumerable<RoiParameter> enumerable = registerImageEntity2.RoiList.Where((RoiParameter p) => !string.IsNullOrWhiteSpace(p.Lable));
								if (enumerable != null && enumerable.Count() > 0)
								{
									List<string> list = new List<string>();
									foreach (RoiParameter item in enumerable)
									{
										if (!list.Contains(item.Lable))
										{
											CategoryData categoryData = CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == item.Lable);
											if (categoryData != null)
											{
												categoryData.LabelText = "已标为此类";
											}
											else
											{
												item.Lable = "";
												item.RoiColor = "#0F6AF3";
											}
											list.Add(item.Lable);
										}
									}
									foreach (RoiParameter item2 in enumerable)
									{
										item2.IsROIRegister = false;
									}
									((ImageEntity)registerImageEntity2).IsCalibrated = image.IsCalibrated;
									((ImageEntity)registerImageEntity2).IsOKCalibrated = image.IsOKCalibrated;
									((ImageEntity)registerImageEntity2).ImageLabel = image.ImageLabel;
									if (((ImageEntity)registerImageEntity2).IsOKCalibrated)
									{
										foreach (RoiParameter item3 in enumerable)
										{
											item3.IsROIRegister = true;
										}
									}
									int markNum = MarkNum;
									MarkNum = markNum + 1;
								}
							}
							_ImageList.Add(registerImageEntity2);
						});
					}
					num++;
					num2 = 10 + (int)((float)num / (float)count * 90f);
					EdgeLearningData.Instance.Owner.SetProgressValue(num2);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				EdgeLearningData.Instance.Owner.CloseProgressDialog();
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
			EdgeLearningData.Instance.Owner.CloseProgressDialog();
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
			EdgeLearningData.Instance.Owner.ShowProgressDialog("正在加载图片");
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
						if (!EdgeLearningData.Instance.IsSupportImage(Path.GetExtension(text)))
						{
							continue;
						}
						DoAddImage(text, isCalibrate);
					}
					num2++;
					num3 = 10 + (int)((float)num2 / (float)num * 90f);
					EdgeLearningData.Instance.Owner.SetProgressValue(num3);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				EdgeLearningData.Instance.Owner.CloseProgressDialog();
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex3.Message, MessageIcons.Warning);
				});
			}
			OnPropertyChanged("AllImageCount");
			OnPropertyChanged("DisplayImageList");
			EdgeLearningData.Instance.Owner.CloseProgressDialog();
		});
	}

	private void DoAddImages(string[] fileNames, bool isCalibrate = false)
	{
		foreach (string text in fileNames)
		{
			if (Directory.Exists(text))
			{
				DoAddImages(Directory.GetFileSystemEntries(text).ToArray(), isCalibrate);
			}
			else if (EdgeLearningData.Instance.IsSupportImage(Path.GetExtension(text)))
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
		if (name.Length > 0 && name[0] == '\\')
		{
			throw new Exception(AppLocalizationService.GetLang("Lang_LocalImageNeed"));
		}
		if (ImageList.Count >= IMAGE_MAXCOUNT)
		{
			throw new Exception(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + IMAGE_MAXCOUNT);
		}
		if (isCalibrate)
		{
			Dispatcher.Invoke(() =>
			{
				if (SelectedCategory.CalibratedNum >= SelectedCategory.RemainedNum)
				{
					throw new Exception(string.Format(AppLocalizationService.GetLang("Lang_RegisterCategoryLimitTip"), 10u));
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
		RegisterImageEntity image = _ImageList.FirstOrDefault((RegisterImageEntity x) => ((ImageEntity)x).ImagePath == strImagePath);
		if (image == null)
		{
			Dispatcher.Invoke(() =>
			{
				BitmapImage imageSource = ImageData.CreateThumbnail(strImagePath, 104.0, 78.0);
				if (isCalibrate)
				{
					ObservableCollection<RegisterImageEntity> imageList = _ImageList;
					RegisterImageEntity registerImageEntity = new RegisterImageEntity();
					((ImageEntity)registerImageEntity).ImagePath = strImagePath;
					((ImageEntity)registerImageEntity).ImageSource = imageSource;
					((ImageEntity)registerImageEntity).ImageLabel = SelectedCategory.CategoryName;
					imageList.Add(registerImageEntity);
					SelectedCategory.CalibratedNum++;
					int markNum = MarkNum;
					MarkNum = markNum + 1;
				}
				else
				{
					ObservableCollection<RegisterImageEntity> imageList2 = _ImageList;
					RegisterImageEntity registerImageEntity2 = new RegisterImageEntity();
					((ImageEntity)registerImageEntity2).ImagePath = strImagePath;
					((ImageEntity)registerImageEntity2).ImageSource = imageSource;
					imageList2.Add(registerImageEntity2);
				}
			});
			return;
		}
		Dispatcher.Invoke(() =>
		{
			if (isCalibrate && string.IsNullOrWhiteSpace(((ImageEntity)image).ImageLabel))
			{
				((ImageEntity)image).ImageLabel = SelectedCategory.CategoryName;
				SelectedCategory.CalibratedNum++;
				int markNum = MarkNum;
				MarkNum = markNum + 1;
			}
		});
	}

	private string GetNewImageName(string extension = ".bmp")
	{
		string result = null;
		if (EdgeLearningData.Instance != null)
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

	private void RegionScale_Click(object sender, RoutedEventArgs e)
	{
		imageView.Init();
	}

	private void FullScreen_Click1(object sender, RoutedEventArgs e)
	{
		FullScreen instance = FullScreen.Instance;
		instance.DataContext = DataContext;
		viewGrid.Children.Remove(imageView);
		viewGrid.Children.Remove(btnLeft);
		viewGrid.Children.Remove(btnRight);
		instance.SetChild(imageView);
		instance.SetChild(btnLeft);
		instance.SetChild(btnRight);
		instance.ShowDialog();
		instance.RemoveChild();
		instance.DataContext = null;
		viewGrid.Children.Add(imageView);
		viewGrid.Children.Add(btnLeft);
		viewGrid.Children.Add(btnRight);
	}
}
