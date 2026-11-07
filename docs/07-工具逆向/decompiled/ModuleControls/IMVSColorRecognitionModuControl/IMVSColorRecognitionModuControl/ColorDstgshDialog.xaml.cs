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
using System.Windows.Controls.Primitives;
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
using Apps.XmlParser;
using Hik.Util;
using HikExternalCall.Common;
using HikExternalCall.ImageProcess;
using HikPcUI.Controls;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSColorRecognitionModuControl;

public partial class ColorDstgshDialog : System.Windows.Controls.UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	protected const int PATH_MAX = 255;

	private bool _init = false;

	public Window Owner;

	private uint IMVS_OK = 0u;

	private uint _timeOut = 10000u;

	private int _SelectedItemsCount;

	protected ObservableCollection<ImageEntity> _ImageList = new ObservableCollection<ImageEntity>();

	protected ObservableCollection<LabelType> _LabelList;

	protected ObservableCollection<string> _SensitivityModeList;

	private LabelMask _CurMask;

	private int _SelectListViewIndex = 0;

	protected ImagePageData myOCRPageData = new ImagePageData();

	private RoiParameter _SelectRoi;

	private bool _IsShowCheck = false;

	private bool _IsShowProcessMask = false;

	private ObservableCollection<string> _FeatureTypeList;

	private ObservableCollection<string> _IntensityList;

	private int FreshListCount = 0;

	public IVmModule Module { get; set; }

	public IParamsConfig Param { get; set; }

	protected HikImageView ImageView => imageView;

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

	public ObservableCollection<ImageEntity> ImageList => _ImageList;

	public ObservableCollection<LabelType> LabelList
	{
		get
		{
			if (_LabelList == null)
			{
				_LabelList = new ObservableCollection<LabelType>();
			}
			return _LabelList;
		}
		set
		{
			if (_LabelList != value)
			{
				_LabelList = value;
				OnPropertyChanged("LabelList");
			}
		}
	}

	public ObservableCollection<string> SensitivityModeList
	{
		get
		{
			if (_SensitivityModeList == null)
			{
				_SensitivityModeList = new ObservableCollection<string>();
				_SensitivityModeList.Add(AppLocalizationService.GetLang("Low_Sensitivity_Mode"));
				_SensitivityModeList.Add(AppLocalizationService.GetLang("Medium_Sensitivity_Mode"));
				_SensitivityModeList.Add(AppLocalizationService.GetLang("High_Sensitivity_Mode"));
			}
			return _SensitivityModeList;
		}
	}

	public ObservableCollection<string> FeatureTypeList
	{
		get
		{
			if (_FeatureTypeList == null)
			{
				_FeatureTypeList = new ObservableCollection<string>();
				_FeatureTypeList.Add(AppLocalizationService.GetLang("Feature_Histogram"));
				_FeatureTypeList.Add(AppLocalizationService.GetLang("Feature_Spectrum"));
			}
			return _FeatureTypeList;
		}
	}

	public ObservableCollection<string> IntensityList
	{
		get
		{
			if (_IntensityList == null)
			{
				_IntensityList = new ObservableCollection<string>();
				_IntensityList.Add(AppLocalizationService.GetLang("Off"));
				_IntensityList.Add(AppLocalizationService.GetLang("Lang_Enabled"));
			}
			return _IntensityList;
		}
	}

	public LabelMask CurMask
	{
		get
		{
			if (_CurMask == null)
			{
				_CurMask = new LabelMask();
			}
			return _CurMask;
		}
		set
		{
			if (_CurMask != value)
			{
				_CurMask = value;
			}
		}
	}

	public int ShowAllTextCount => _ImageList.Count;

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

	public IItemsData CurData => myOCRPageData;

	public RoiParameter SelectRoi
	{
		get
		{
			return _SelectRoi;
		}
		set
		{
			if (_SelectRoi != value)
			{
				_SelectRoi = value;
			}
		}
	}

	public bool IsShowCheck
	{
		get
		{
			return _IsShowCheck;
		}
		set
		{
			if (_IsShowCheck != value)
			{
				_IsShowCheck = value;
				OnPropertyChanged("IsShowCheck");
			}
		}
	}

	public bool IsShowProcessMask
	{
		get
		{
			return _IsShowProcessMask;
		}
		set
		{
			if (_IsShowProcessMask != value)
			{
				_IsShowProcessMask = value;
				OnPropertyChanged("IsShowProcessMask");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public ColorDstgshDialog()
	{
		InitializeComponent();
		HikImageView hikImageView = imageView;
		hikImageView.OnSelectedBaseROIChange = (HikImageView.SelectedBaseROIChange)Delegate.Combine(hikImageView.OnSelectedBaseROIChange, new HikImageView.SelectedBaseROIChange(imageView_OnSelectedBaseROIChange));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView2.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		HikImageView hikImageView3 = imageView;
		hikImageView3.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView3.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
		HikImageView hikImageView4 = imageView;
		hikImageView4.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Combine(hikImageView4.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChanged));
		Loaded += (object s, RoutedEventArgs e) =>
		{
			LabelList = GetModelInfos(null);
			if (labelListView.HasItems)
			{
				labelListView.SelectedIndex = 0;
			}
			string directoryName = Path.GetDirectoryName(Param.XmlPath);
			directoryName += "\\temp";
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			AddImageByDir(directoryName);
			listScroll.ScrollToRightEnd();
			if (lst.Items.Count > 0)
			{
				lst.SelectedIndex = lst.Items.Count - 1;
			}
			try
			{
				int result = 0;
				int.TryParse(((IUserStringData)Param)["SensitivityMode"], out result);
				SensitivityComb.SelectedIndex = result - 1;
				int result2 = 0;
				int.TryParse(((IUserStringData)Param)["FeatureType"], out result2);
				FeatureComb.SelectedIndex = result2 - 1;
				int result3 = 0;
				int.TryParse(((IUserStringData)Param)["Intensity"], out result3);
				IntensityComb.SelectedIndex = result3;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
			}
			if (!_init)
			{
				_init = true;
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			DeInit();
		};
	}

	private void imageView_OnRoiCountChanged(object Roi, EventArgs e = null)
	{
		Arrow.IsChecked = true;
	}

	private void imageView_OnRoiDeCountChanged(object Roi)
	{
		if (CurData == null)
		{
			return;
		}
		if (CurData.ItemsData.Count == 0)
		{
			IsShowCheck = false;
			CurMask.Clear();
			return;
		}
		try
		{
			IsShowCheck = GetMaskColorData();
		}
		catch (Exception ex)
		{
			int num = int.Parse(ex.Message, NumberStyles.HexNumber);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err") + ":" + ErrorCodeInfo.GetErrorInfo(num), MessageIcons.Error);
			IsShowCheck = false;
		}
		if (!IsShowCheck)
		{
			CurMask.ColorDataHSI.Clear();
		}
	}

	private void imageView_OnRoiParamChange(object Date)
	{
		try
		{
			IsShowCheck = GetMaskColorData();
		}
		catch (Exception ex)
		{
			int num = int.Parse(ex.Message, NumberStyles.HexNumber);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err") + ":" + ErrorCodeInfo.GetErrorInfo(num), MessageIcons.Error);
			IsShowCheck = false;
		}
	}

	private bool GetMaskColorData()
	{
		ImageEntity imageEntity = null;
		int num = 0;
		int num2 = 0;
		imageEntity = lst.SelectedItem as ImageEntity;
		num = ImageView.ImageDataSource.Height;
		num2 = ImageView.ImageDataSource.Width;
		CurMask.Image.ImagePath = imageEntity.ImagePath;
		CurMask.Image.ImageName = imageEntity.ImageName;
		CurMask.Image.ImageHeight = num;
		CurMask.Image.ImageWidth = num2;
		CurMask.Module = Module;
		CurMask.Param = Param;
		try
		{
			CurMask.GenerateMask(CurData.ItemsData);
			return CurMask.CaculateColor(_timeOut);
		}
		catch (Exception)
		{
			throw;
		}
	}

	protected void lst_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		SelectListViewIndex = lst.SelectedIndex + 1;
		if (lst != null)
		{
			SelectedItemsCount = lst.SelectedItems.Count;
		}
		if ((sender as System.Windows.Controls.ListView).SelectedItem is ImageEntity imageEntity)
		{
			CurData.ItemsData.Clear();
			CurMask.Clear();
			IsShowCheck = false;
			ShowSelectedImage(imageEntity.ImagePath);
		}
	}

	private void clear_Click(object sender, RoutedEventArgs e)
	{
		string strMsg = AppLocalizationService.GetLang("是否清除") + "?\r\n";
		VM.Controls.DialogResult dialogResult = HikMessageBox.Show(strMsg, MessageButtons.YesOrNo, MessageIcons.Warning);
		if (dialogResult == VM.Controls.DialogResult.Yes)
		{
			if (CurData != null)
			{
				CurData.ItemsData.Clear();
				IsShowCheck = false;
			}
			if (CurMask != null)
			{
				CurMask.Clear();
			}
		}
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		switch ((sender as FrameworkElement).Name)
		{
		case "hikFrame":
			ImageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
			break;
		case "hikPolygon":
			ImageView.PrepareRawRoi(RoiUIType.HikPolygonRoi);
			break;
		case "hikCoverCircle":
			ImageView.PrepareRawRoi(RoiUIType.HikCoverCircleRoi);
			break;
		default:
			ImageView.UnPrepareRawRoi();
			break;
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		ImageView.UnPrepareRawRoi();
	}

	private void zoomOrgBtn_Click(object sender, RoutedEventArgs e)
	{
		ImageView.Init();
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		FullScreen instance = FullScreen.Instance;
		instance.Owner = Window.GetWindow(this);
		instance.DataContext = viewGrid.DataContext;
		viewGrid.Children.Remove(ImageView);
		viewGrid.Children.Remove(btnLeft);
		viewGrid.Children.Remove(btnRight);
		instance.SetChild(ImageView);
		instance.SetChild(btnLeft);
		instance.SetChild(btnRight);
		instance.ShowDialog();
		instance.RemoveChild();
		instance.DataContext = null;
		viewGrid.Children.Add(ImageView);
		viewGrid.Children.Add(btnLeft);
		viewGrid.Children.Add(btnRight);
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

	protected void Cur_File_Click(object sender, RoutedEventArgs e)
	{
		if (Module == null || Param == null)
		{
			return;
		}
		try
		{
			string path = Path.GetDirectoryName(Param.XmlPath) + $"\\{Module.ModuleTypeName}Display.xml";
			ParamRoot paramRoot = new ParamRoot(path);
			paramRoot.Owner = Module;
			if (paramRoot == null)
			{
				return;
			}
			IList<object> displayList = paramRoot.GetDisplayList();
			foreach (Apps.XmlParser.Object item in displayList)
			{
				if (!(item.TypeA == "image") && !(item.TypeA == "pixelinfoimage"))
				{
					continue;
				}
				ImageData imageData = (ImageData)item.GetValue(uint.MaxValue);
				if (imageData != null && imageData.ImageBuffer == null)
				{
					imageData.ImageBuffer = Byte2ImageSource.GetImageBufferByFileName(imageData.MemoryAddress, imageData.Width, imageData.Height, imageData.PixelFormat);
					imageData = ((imageData.ImageBuffer == null) ? null : imageData);
				}
				if (imageData != null && imageData.ImageBuffer.Length != 0)
				{
					string path2 = "";
					if (saveImageToFold(imageData, ref path2))
					{
						DoAddImage(path2);
						listScroll.ScrollToRightEnd();
						if (lst.Items.Count > 0)
						{
							lst.SelectedIndex = lst.Items.Count - 1;
						}
						OnPropertyChanged("ShowAllTextCount");
					}
				}
				else
				{
					HikMessageBox.Show("添加图片", "Lang_GetImageFromProcessFailed", MessageIcons.Warning, isTranslate: true);
				}
				break;
			}
		}
		catch (Exception ex)
		{
			int num = int.Parse(ex.Message, NumberStyles.HexNumber);
			HikMessageBox.Show(AppLocalizationService.GetLang("添加图片") + ":" + ErrorCodeInfo.GetErrorInfo(num), MessageIcons.Warning);
		}
	}

	private bool saveImageToFold(ImageData image, ref string path)
	{
		string text = Path.GetDirectoryName(Param.XmlPath) + "\\temp\\image";
		string text2 = ".bmp";
		string text3 = text + text2;
		string text4 = "";
		if (File.Exists(text3))
		{
			for (uint num = 0u; num < uint.MaxValue; num++)
			{
				if (File.Exists(text3))
				{
					text3 = text + "_" + num + text2;
					continue;
				}
				text4 = text3;
				break;
			}
		}
		else
		{
			text4 = text3;
		}
		path = text4;
		ImageSaveOpenCvHelper.SaveRawImageSyn(text4, image);
		return true;
	}

	protected void File_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Multiselect = true;
		string text = Path.GetDirectoryName(Param.XmlPath) + "\\temp";
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		openFileDialog.Filter = AppLocalizationService.GetLang("图像文件") + "|*.bmp;*.jpg;*.jpeg;*.png";
		if (openFileDialog.ShowDialog() != false)
		{
			DoAddImages(openFileDialog.FileNames);
			listScroll.ScrollToRightEnd();
			if (lst.Items.Count > 0)
			{
				lst.SelectedIndex = lst.Items.Count - 1;
			}
		}
	}

	protected void Fold_Click(object sender, RoutedEventArgs e)
	{
		string text = "";
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		string text2 = Path.GetDirectoryName(Param.XmlPath) + "\\temp";
		if (text2 != null)
		{
			folderBrowserDialog.SelectedPath = text2;
		}
		System.Windows.Forms.DialogResult dialogResult = folderBrowserDialog.ShowDialog();
		if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
		{
			folderBrowserDialog.Dispose();
			return;
		}
		try
		{
			text = folderBrowserDialog.SelectedPath.Trim();
		}
		catch (PathTooLongException)
		{
			folderBrowserDialog.Dispose();
			HikMessageBox.Show("Lang_DrAdd", "Lang_FilePathLong", MessageIcons.Error, isTranslate: true);
			return;
		}
		folderBrowserDialog.Dispose();
		byte[] bytes = Encoding.Default.GetBytes(text);
		if (bytes.Length > 255)
		{
			HikMessageBox.Show("Lang_DrAdd", "Lang_FilePathLong", MessageIcons.Error, isTranslate: true);
			return;
		}
		AddImageByDir(text);
		listScroll.ScrollToRightEnd();
		if (lst.Items.Count > 0)
		{
			lst.SelectedIndex = lst.Items.Count - 1;
		}
	}

	protected void Del_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			int num = lst.SelectedIndex - 1;
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
			if (num >= 0)
			{
				if (num >= lst.Items.Count)
				{
					lst.SelectedIndex = lst.Items.Count - 1;
				}
				else
				{
					lst.SelectedIndex = num;
				}
			}
			else
			{
				lst.SelectedIndex = -1;
			}
			if (lst.SelectedIndex == -1)
			{
				ImageView.ImageDataSource = null;
			}
		}
		catch
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_NoneChoose", MessageIcons.Error, isTranslate: true);
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

	private bool IsChildVisibleInParent(FrameworkElement child, FrameworkElement parent)
	{
		GeneralTransform generalTransform = child.TransformToAncestor(parent);
		Rect rect = generalTransform.TransformBounds(new Rect(new Point(0.0, 0.0), child.RenderSize));
		return new Rect(new Point(0.0, 0.0), parent.RenderSize).IntersectsWith(rect);
	}

	private void FreshList()
	{
		if (FreshListCount >= 5)
		{
			return;
		}
		FreshListCount++;
		Dispatcher.BeginInvoke((Action)(() =>
		{
			foreach (ImageEntity item in (IEnumerable)lst.Items)
			{
				FrameworkElement child = (FrameworkElement)lst.ItemContainerGenerator.ContainerFromItem(item);
				if (item != null)
				{
					if (IsChildVisibleInParent(child, listScroll))
					{
						if (item.ImageSource == null)
						{
							BitmapImage bitmapImage = ImageData.CreateThumbnail(item.ImagePath, 126.0, 102.0);
							if (bitmapImage != null)
							{
								item.ImageSource = bitmapImage;
							}
						}
					}
					else
					{
						item.ImageSource = null;
					}
				}
			}
			FreshListCount--;
		}));
	}

	private void listScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		FreshList();
		if (lst != null)
		{
			SelectedItemsCount = lst.SelectedItems.Count;
			SelectListViewIndex = lst.SelectedIndex + 1;
		}
	}

	private void importModel_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Multiselect = false;
		openFileDialog.Title = AppLocalizationService.GetLang("获取") + AppLocalizationService.GetLang("Lang_TemplateFile");
		openFileDialog.Filter = AppLocalizationService.GetLang("Lang_TemplateFile") + "|*.bin";
		string text = ConfigurationHelper.INIGetDefaultOpenPath(Module?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		if (openFileDialog.ShowDialog() == false)
		{
			return;
		}
		text = openFileDialog.FileName;
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(text), Module?.ModuleDir);
		byte[] libFile = EncryptionAndDecryptionHelper.GetLibFile(text);
		if (libFile == null)
		{
			return;
		}
		try
		{
			((IUserBytesData)Param)["SetModelData"] = libFile;
			GetModelInfos(LabelList);
			HikMessageBox.Show(AppLocalizationService.GetLang("加载") + AppLocalizationService.GetLang("Lang_TemplateFile") + AppLocalizationService.GetLang("Lang_OK"), MessageIcons.Ok);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadErrtoDecode"), AppLocalizationService.GetLang("errCode:") + ex.Message, MessageIcons.Error);
		}
	}

	private void addToLabel_Click(object sender, RoutedEventArgs e)
	{
		if (ImageView == null || lst == null)
		{
			return;
		}
		LabelType labelType = null;
		if (LabelList.Count == 0)
		{
			labelType = new LabelType();
			labelType.Owner = this;
			labelType.Name = AppLocalizationService.GetLang("InspectLabel") + "1";
			LabelList.Add(labelType);
			labelListView.SelectedIndex = 0;
		}
		if (!(labelListView.SelectedItem is LabelType labelType2))
		{
			HikMessageBox.Show("Color Model", "标签未选中", MessageIcons.Error, isTranslate: true);
			return;
		}
		if (labelType2.MaskList.Count >= 100)
		{
			HikMessageBox.Show("Color Model", "标签已满", MessageIcons.Error, isTranslate: true);
			return;
		}
		LabelMask labelMask = new LabelMask();
		labelMask.Image.ImagePath = CurMask.Image.ImagePath;
		labelMask.Image.ImageWidth = CurMask.Image.ImageWidth;
		labelMask.Image.ImageHeight = CurMask.Image.ImageHeight;
		labelMask.Image.ImageSize = labelMask.Image.ImageWidth * labelMask.Image.ImageHeight * 3;
		labelMask.MinRoiX = CurMask.MinRoiX;
		labelMask.MinRoiY = CurMask.MinRoiY;
		labelMask.MaxRoiX = CurMask.MaxRoiX;
		labelMask.MaxRoiY = CurMask.MaxRoiY;
		string text = Path.GetDirectoryName(CurMask.MaskImagePath) + "\\" + Path.GetFileNameWithoutExtension(CurMask.MaskImagePath);
		string extension = Path.GetExtension(CurMask.MaskImagePath);
		string text2 = text + extension;
		string text3 = string.Empty;
		if (File.Exists(text2))
		{
			for (uint num = 0u; num < uint.MaxValue; num++)
			{
				if (File.Exists(text2))
				{
					text2 = text + "_" + num + extension;
					continue;
				}
				text3 = text2;
				break;
			}
		}
		else
		{
			text3 = text2;
		}
		try
		{
			if (text3 != null)
			{
				File.Copy(CurMask.MaskImagePath, text3);
				labelMask.MaskImagePath = text3;
			}
		}
		catch
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_ImagePathError", MessageIcons.Error, isTranslate: true);
			return;
		}
		BitmapImage bitmapImage = ImageData.CreateRoiThumbnail(rect: new Int32Rect
		{
			X = CurMask.MaskData.MinRoiX,
			Y = CurMask.MaskData.MinRoiY,
			Width = CurMask.MaskData.MaxRoiX - CurMask.MaskData.MinRoiX,
			Height = CurMask.MaskData.MaxRoiY - CurMask.MaskData.MinRoiY
		}, fileName: labelMask.Image.ImagePath, panelWidth: 80.0, panelHeight: 50.0);
		if (bitmapImage == null)
		{
			HikMessageBox.Show("IMVSColorRecognitionModu", "生成缩略图失败", MessageIcons.Error, isTranslate: true);
			labelMask.DeleteMaskImg();
		}
		else
		{
			labelMask.Thbnail.ImageSource = bitmapImage;
			labelMask.Owner = labelType2;
			labelType2.MaskList.Add(labelMask);
			ScrollViewer.ScrollToBottom();
		}
		CurMask.Clear();
		IsShowCheck = false;
	}

	private void CreatClassify_Click(object sender, RoutedEventArgs e)
	{
		string lang = AppLocalizationService.GetLang("InspectLabel");
		int num = 1;
		if (Enumerable.Count(LabelList) >= 100)
		{
			HikMessageBox.Show("label_list", "标签类已满", MessageIcons.Error, isTranslate: true);
			return;
		}
		string newName = lang + num;
		while (LabelList.FirstOrDefault((LabelType x) => x.Name == newName) != null)
		{
			num++;
			newName = lang + num;
		}
		LabelType labelType = new LabelType();
		labelType.Owner = this;
		labelType.Name = newName;
		LabelList.Add(labelType);
		labelListView.SelectedIndex = LabelList.Count - 1;
		labelListScroll.ScrollToBottom();
	}

	private void AddImageToList(string strResult, string newPath = null, bool AddToList = true)
	{
		if (strResult.Length > 0 && strResult[0] == '\\')
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_LocalImageNeed", MessageIcons.Error, isTranslate: true);
		}
		else
		{
			if (!File.Exists(strResult))
			{
				return;
			}
			try
			{
				if (newPath != null)
				{
					File.Copy(strResult, newPath);
					strResult = newPath;
				}
				if (AddToList && _ImageList.FirstOrDefault((ImageEntity x) => x.ImagePath == strResult) == null)
				{
					_ImageList.Add(new ImageEntity
					{
						ImagePath = strResult
					});
				}
			}
			catch
			{
				HikMessageBox.Show("Lang_ImageAdd", "Lang_ImagePathError", MessageIcons.Error, isTranslate: true);
			}
		}
	}

	private string DoAddImage(string name, bool needCopy = false, bool AddToList = true)
	{
		string text = Path.GetExtension(name).ToUpper();
		if (text != ".JPG" && text != ".JPEG" && text != ".BMP" && text != ".PNG")
		{
			return null;
		}
		byte[] bytes = Encoding.Default.GetBytes(name);
		if (bytes.Length > 255)
		{
			HikMessageBox.Show("Lang_ImageAdd", "Lang_FilePathLong", MessageIcons.Error);
			return null;
		}
		string text2 = null;
		string directoryName = Path.GetDirectoryName(Param.XmlPath);
		directoryName = directoryName.Substring(1);
		if (!Path.GetDirectoryName(name).Contains(directoryName))
		{
			string text3 = Path.GetDirectoryName(Param.XmlPath) + "\\temp\\image";
			string extension = Path.GetExtension(name);
			string text4 = text3 + extension;
			if (File.Exists(text4))
			{
				for (uint num = 0u; num < uint.MaxValue; num++)
				{
					if (File.Exists(text4))
					{
						text4 = text3 + "_" + num + extension;
						continue;
					}
					text2 = text4;
					break;
				}
			}
			else
			{
				text2 = text4;
			}
		}
		AddImageToList(name, text2, AddToList);
		return text2;
	}

	private void DoAddImages(string[] fileNames)
	{
		foreach (string text in fileNames)
		{
			string text2 = Path.GetExtension(text).ToUpper();
			if (!(text2 != ".JPG") || !(text2 != ".JPEG") || !(text2 != ".BMP") || !(text2 != ".PNG"))
			{
				DoAddImage(text);
			}
		}
		OnPropertyChanged("ShowAllTextCount");
	}

	protected bool AddImageByDir(string dir, bool IsFlaw = false)
	{
		try
		{
			if (Directory.Exists(dir))
			{
				DoAddImages((from x in Directory.GetFileSystemEntries(dir)
					orderby x.Length, x
					select x).ToArray());
			}
		}
		catch (Exception)
		{
			HikMessageBox.Show("Lang_Err", "Lang_FileScanErr", MessageIcons.Error, isTranslate: true);
		}
		return true;
	}

	protected virtual bool ShowSelectedImage(string path)
	{
		ImageData imageData = new ImageData();
		try
		{
			int PixelFormat = 0;
			ImageProcessManager.GetRgbC3ImageData(path, ref imageData.Width, ref imageData.Height, ref imageData.Size, ref imageData.ImageBuffer, ref PixelFormat);
			imageData.PixelFormat = PixelFormats.Rgb24;
			if (imageData.ImageBuffer == null)
			{
				if (lst != null)
				{
					if (lst.SelectedItem is ImageEntity item)
					{
						ImageList.Remove(item);
					}
					lst.SelectedItem = null;
				}
				throw new Exception(AppLocalizationService.GetLang("imageLoadError"));
			}
			if (imageData.Width > 0)
			{
				imageView.ImageDataSource = imageData;
				if (lst != null && lst.SelectedItem is ImageEntity imageEntity)
				{
					imageEntity.ImageWidth = imageData.Width;
					imageEntity.ImageHeight = imageData.Height;
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_ImageOpen", ex.Message, MessageButtons.Normal, MessageIcons.Error, isTranslate: true);
			return false;
		}
	}

	private void imageView_OnSelectedBaseROIChange(object sender, object Data)
	{
		SelectRoi = Data as RoiParameter;
	}

	private void delete_Click(object sender, RoutedEventArgs e)
	{
		if (CurData != null && SelectRoi != null)
		{
			CurData.ItemsData.Remove(SelectRoi);
		}
	}

	private void submit_Click(object sender, RoutedEventArgs e)
	{
		if (Enumerable.Count(LabelList) == 0)
		{
			HikMessageBox.Show("label_list", "Lang_PmNameEmpty", MessageIcons.Error, isTranslate: true);
		}
		else
		{
			AsyncSubmit();
		}
	}

	private async Task AsyncSubmit()
	{
		if (await AsyncCreateModel())
		{
			DeInit();
			Owner.DialogResult = true;
			Owner.Close();
		}
	}

	private async Task<bool> AsyncCreateModel()
	{
		IsShowProcessMask = true;
		bool bRet = await Task.Run(() =>
		{
			byte[] ImageBuffer = null;
			try
			{
				int num = GeneralLabelTypeListBuffer(ref ImageBuffer);
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error, isTranslate: true);
				return false;
			}
			try
			{
				((IUserBytesData)Param)["SetTranningData"] = ImageBuffer;
			}
			catch (Exception ex2)
			{
				IsShowProcessMask = false;
				if (ex2.Message == "E0000108")
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("发送数据超时"), AppLocalizationService.GetLang("errCode:") + ex2.Message, MessageIcons.Error);
				}
				else
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("发送数据失败"), AppLocalizationService.GetLang("errCode:") + ex2.Message, MessageIcons.Error);
				}
				return false;
			}
			try
			{
				byte[] array = null;
				array = ((IUserBytesData)Param)["GetLabelModelPath"];
			}
			catch (Exception ex3)
			{
				IsShowProcessMask = false;
				if (ex3.Message == "E0000108")
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("生成模型超时"), AppLocalizationService.GetLang("errCode:") + ex3.Message, MessageIcons.Error);
				}
				else
				{
					int num2 = int.Parse(ex3.Message, NumberStyles.HexNumber);
					HikMessageBox.Show(AppLocalizationService.GetLang("模型未生成"), ErrorCodeInfo.GetErrorInfo(num2), MessageIcons.Error);
				}
				return false;
			}
			IsShowProcessMask = false;
			return true;
		});
		IsShowProcessMask = false;
		return bRet;
	}

	private unsafe int GeneralLabelTypeListBuffer(ref byte[] ImageBuffer)
	{
		int num = 0;
		num += 4;
		foreach (LabelType item in (IEnumerable)labelListView.Items)
		{
			num += 256;
			num += 4;
			foreach (LabelMask mask in item.MaskList)
			{
				num += 4;
				num += 4;
				num += 4;
				num += 4;
				num += 4;
				num += 4;
				num += 4;
				num += 4;
				num += 4;
				if (mask.MaskImagePath != string.Empty || mask.MaskBuffer.ImageSize != 0)
				{
					num += 4;
					num += 4;
					num += 4;
				}
				num += mask.Image.ImageSize;
				if (mask.MaskImagePath != string.Empty)
				{
					num += mask.Image.ImageHeight * mask.Image.ImageWidth;
				}
				else if (mask.MaskBuffer.ImageSize != 0)
				{
					num += mask.MaskBuffer.ImageSize;
				}
			}
		}
		byte[] array = new byte[num];
		try
		{
			byte* ptr = (byte*)ExternalCallHelper.Bytes2IntPtr(array, out var hObject).ToPointer();
			int num2 = 0;
			*(int*)(ptr + num2) = labelListView.Items.Count;
			num2 += 4;
			foreach (LabelType item2 in (IEnumerable)labelListView.Items)
			{
				byte[] array2 = StringUtil.UTF8GetFixLenBytes(item2.Name, 256);
				Marshal.Copy(array2, 0, (IntPtr)(ptr + num2), array2.Length);
				num2 += 256;
				*(int*)(ptr + num2) = item2.MaskList.Count;
				num2 += 4;
				foreach (LabelMask mask2 in item2.MaskList)
				{
					ImageData imageData = new ImageData();
					if (mask2.Image.Buffer != null)
					{
						imageData.Size = mask2.Image.ImageSize;
						imageData.Width = mask2.Image.ImageWidth;
						imageData.Height = mask2.Image.ImageHeight;
						imageData.ImageBuffer = mask2.Image.Buffer;
					}
					else
					{
						int PixelFormat = 0;
						ImageProcessManager.GetRgbC3ImageData(mask2.Image.ImagePath, ref imageData.Width, ref imageData.Height, ref imageData.Size, ref imageData.ImageBuffer, ref PixelFormat);
						imageData.PixelFormat = PixelFormats.Rgb24;
					}
					*(int*)(ptr + num2) = imageData.Size;
					num2 += 4;
					*(int*)(ptr + num2) = imageData.Width;
					num2 += 4;
					*(int*)(ptr + num2) = imageData.Height;
					num2 += 4;
					*(int*)(ptr + num2) = 201;
					num2 += 4;
					*(int*)(ptr + num2) = mask2.MinRoiX;
					num2 += 4;
					*(int*)(ptr + num2) = mask2.MinRoiY;
					num2 += 4;
					*(int*)(ptr + num2) = mask2.MaxRoiX - mask2.MinRoiX;
					num2 += 4;
					*(int*)(ptr + num2) = mask2.MaxRoiY - mask2.MinRoiY;
					num2 += 4;
					ImageData imageData2 = new ImageData();
					if (mask2.MaskBuffer.Buffer != null)
					{
						imageData2.Size = mask2.MaskBuffer.ImageSize;
						imageData2.Width = mask2.MaskBuffer.ImageWidth;
						imageData2.Height = mask2.MaskBuffer.ImageHeight;
						imageData2.ImageBuffer = mask2.MaskBuffer.Buffer;
					}
					else if (mask2.MaskImagePath != string.Empty)
					{
						ImageProcessManager.GetMono8ImageData(mask2.MaskImagePath, ref imageData2.Width, ref imageData2.Height, ref imageData2.ImageBuffer);
						imageData2.PixelFormat = PixelFormats.Gray8;
						imageData2.Size = imageData2.Width * imageData2.Height;
					}
					if (imageData2.Size != 0)
					{
						*(int*)(ptr + num2) = imageData2.Size;
						num2 += 4;
						*(int*)(ptr + num2) = imageData2.Width;
						num2 += 4;
						*(int*)(ptr + num2) = imageData2.Height;
						num2 += 4;
						*(int*)(ptr + num2) = 1;
						num2 += 4;
					}
					else
					{
						*(int*)(ptr + num2) = 0;
						num2 += 4;
					}
					if (imageData.ImageBuffer == null || imageData2.ImageBuffer == null)
					{
						throw new Exception(AppLocalizationService.GetLang("找不到图片"));
					}
					Marshal.Copy(imageData.ImageBuffer, 0, (IntPtr)(ptr + num2), imageData.Size);
					num2 += imageData.Size;
					if (imageData2.Size != 0)
					{
						Marshal.Copy(imageData2.ImageBuffer, 0, (IntPtr)(ptr + num2), imageData2.Size);
						num2 += imageData2.Size;
					}
				}
			}
			ExternalCallHelper.FreeGCHandle(hObject);
		}
		catch (Exception ex)
		{
			LogHelper.Info("Exception in color GeneralLabelTypeListBuffer" + ex.Message);
		}
		ImageBuffer = array;
		return num;
	}

	private void DeInit()
	{
		CurMask.Clear();
		foreach (LabelType label in LabelList)
		{
			foreach (LabelMask mask in label.MaskList)
			{
				mask.DeleteMaskImg();
			}
		}
		LabelList.Clear();
		imageView.Dispose();
		DataContext = null;
		imageView.DataContext = null;
	}

	private ObservableCollection<LabelType> GetModelInfos(ObservableCollection<LabelType> labeltypes)
	{
		if (Param == null)
		{
			return null;
		}
		try
		{
			byte[] array = ((IUserBytesData)Param)["GetModelImage"];
			if (array != null && array.Length != 0)
			{
				return GetModuleLabels(array, labeltypes);
			}
		}
		catch
		{
		}
		return null;
	}

	public unsafe ObservableCollection<LabelType> GetModuleLabels(byte[] data, ObservableCollection<LabelType> labeltypes)
	{
		ObservableCollection<LabelType> observableCollection = null;
		observableCollection = ((labeltypes != null) ? labeltypes : new ObservableCollection<LabelType>());
		try
		{
			byte* ptr = (byte*)ExternalCallHelper.Bytes2IntPtr(data, out var hObject).ToPointer();
			int num = 0;
			int num2 = *(int*)(ptr + num);
			num += 4;
			for (int i = 0; i < num2; i++)
			{
				byte[] array = new byte[256];
				Marshal.Copy((IntPtr)(ptr + num), array, 0, 256);
				num += 256;
				string name = StringUtil.UTF8GetString(array);
				LabelType labelType = null;
				if ((labelType = observableCollection.FirstOrDefault((LabelType x) => x.Name == name)) == null)
				{
					labelType = new LabelType();
					labelType.Owner = this;
					labelType.Name = name;
					if (Enumerable.Count(observableCollection) < 100)
					{
						observableCollection.Add(labelType);
					}
				}
				int num3 = *(int*)(ptr + num);
				num += 4;
				for (int num4 = 0; num4 < num3; num4++)
				{
					LabelMask labelMask = new LabelMask();
					labelMask.Image.ImageSize = *(int*)(ptr + num);
					num += 4;
					labelMask.Image.ImageWidth = *(int*)(ptr + num);
					num += 4;
					labelMask.Image.ImageHeight = *(int*)(ptr + num);
					num += 4;
					num += 4;
					labelMask.MaskBuffer.ImageSize = *(int*)(ptr + num);
					num += 4;
					if (labelMask.MaskBuffer.ImageSize != 0)
					{
						labelMask.MaskBuffer.ImageWidth = *(int*)(ptr + num);
						num += 4;
						labelMask.MaskBuffer.ImageHeight = *(int*)(ptr + num);
						num += 4;
						num += 4;
					}
					WriteableBitmap writeableBitmap = new WriteableBitmap(labelMask.Image.ImageWidth, labelMask.Image.ImageHeight, 96.0, 96.0, PixelFormats.Rgb24, null);
					writeableBitmap.WritePixels(new Int32Rect(0, 0, labelMask.Image.ImageWidth, labelMask.Image.ImageHeight), (IntPtr)(ptr + num), labelMask.Image.ImageSize, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8);
					labelMask.Thbnail.ImageSource = writeableBitmap.ToBitmapImage(80, 50);
					byte[] array2 = new byte[labelMask.Image.ImageSize];
					Marshal.Copy((IntPtr)(ptr + num), array2, 0, labelMask.Image.ImageSize);
					labelMask.Image.Buffer = array2;
					num += labelMask.Image.ImageSize;
					if (labelMask.MaskBuffer.ImageSize != 0)
					{
						byte[] array3 = new byte[labelMask.MaskBuffer.ImageSize];
						Marshal.Copy((IntPtr)(ptr + num), array3, 0, labelMask.MaskBuffer.ImageSize);
						labelMask.MaskBuffer.Buffer = array3;
						num += labelMask.MaskBuffer.ImageSize;
					}
					if (Enumerable.Count(labelType.MaskList) < 100)
					{
						labelMask.Owner = labelType;
						labelType.MaskList.Add(labelMask);
					}
				}
			}
			ExternalCallHelper.FreeGCHandle(hObject);
		}
		catch (Exception ex)
		{
			LogHelper.Info("Exception in color GetModuleLabels" + ex.Message);
		}
		return observableCollection;
	}

	private void exportModel_Click(object sender, RoutedEventArgs e)
	{
		if (Param != null)
		{
			AsyncExportModel();
		}
	}

	private async Task AsyncExportModel()
	{
		if (!(await AsyncCreateModel()))
		{
			return;
		}
		byte[] libData;
		try
		{
			libData = ((IUserBytesData)Param)["GetModelData"];
		}
		catch (Exception)
		{
			HikMessageBox.Show("导出模板文件失败", MessageIcons.Error, isTranslate: true);
			return;
		}
		bool failed = false;
		string msg = string.Empty;
		string path = ConfigurationHelper.INIGetDefaultOpenPath(Module?.ModuleDir);
		using (System.Windows.Forms.SaveFileDialog op = new System.Windows.Forms.SaveFileDialog())
		{
			op.Title = AppLocalizationService.GetLang("Save") + AppLocalizationService.GetLang("Lang_TemplateFile");
			op.Filter = AppLocalizationService.GetLang("Lang_TemplateFile") + "|*.bin";
			if (path != null)
			{
				op.InitialDirectory = path;
			}
			if (op.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}
			try
			{
				path = op.FileName;
			}
			catch (Exception ex2)
			{
				msg = ex2.Message;
				failed = true;
			}
		}
		if (failed)
		{
			HikMessageBox.Show("导出模板文件失败", msg, MessageIcons.Error, isTranslate: true);
		}
		else if (!string.IsNullOrEmpty(path) && EncryptionAndDecryptionHelper.WriteLibFile(path, libData))
		{
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), Module?.ModuleDir);
			HikMessageBox.Show("导出模板文件成功", MessageIcons.Ok, isTranslate: true);
		}
	}

	private void Grid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (sender is Grid { Tag: System.Windows.Controls.Button tag })
		{
			tag.Visibility = Visibility.Visible;
		}
	}

	private void Grid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (sender is Grid { Tag: System.Windows.Controls.Button tag })
		{
			tag.Visibility = Visibility.Hidden;
		}
	}

	private void ComboBox_Selected(object sender, SelectionChangedEventArgs e)
	{
		System.Windows.Controls.ComboBox comboBox = sender as System.Windows.Controls.ComboBox;
		if (comboBox.SelectedIndex == -1)
		{
			return;
		}
		LabelType labelType = comboBox.SelectedItem as LabelType;
		if (Enumerable.Count(labelType.MaskList) > 99)
		{
			HikMessageBox.Show("Lang_FileCountMax", MessageIcons.Error, isTranslate: true);
			return;
		}
		while (itemControl.SelectedItems.Count > 0)
		{
			LabelMask labelMask = itemControl.SelectedItems[0] as LabelMask;
			labelMask.Owner = labelType;
			labelType.MaskList.Add(labelMask);
			(itemControl.ItemsSource as ObservableCollection<LabelMask>).Remove(labelMask);
		}
		comboBox.SelectedIndex = -1;
		comboBox.IsEnabled = false;
	}

	private void itemControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		comboBoxSymbol.IsEnabled = (sender as System.Windows.Controls.ListView).SelectedIndex >= 0;
	}

	private void LabelListScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = (ScrollViewer)sender;
		if (scrollViewer != null)
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

	private void item_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			(sender as System.Windows.Controls.TextBox).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
		}
	}

	private void TextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		(sender as System.Windows.Controls.TextBox).SetValue(System.Windows.Controls.Primitives.TextBoxBase.IsReadOnlyProperty, true);
		(sender as System.Windows.Controls.TextBox).CaretIndex = 0;
	}

	private void Sensitivity_Selected(object sender, SelectionChangedEventArgs e)
	{
		System.Windows.Controls.ComboBox comboBox = sender as System.Windows.Controls.ComboBox;
		((IUserStringData)Param)["SensitivityMode"] = (comboBox.SelectedIndex + 1).ToString();
	}

	private void Feature_Selected(object sender, SelectionChangedEventArgs e)
	{
		System.Windows.Controls.ComboBox comboBox = sender as System.Windows.Controls.ComboBox;
		((IUserStringData)Param)["FeatureType"] = (comboBox.SelectedIndex + 1).ToString();
	}

	private void Intensity_Selected(object sender, SelectionChangedEventArgs e)
	{
		System.Windows.Controls.ComboBox comboBox = sender as System.Windows.Controls.ComboBox;
		((IUserStringData)Param)["Intensity"] = comboBox.SelectedIndex.ToString();
	}
}
