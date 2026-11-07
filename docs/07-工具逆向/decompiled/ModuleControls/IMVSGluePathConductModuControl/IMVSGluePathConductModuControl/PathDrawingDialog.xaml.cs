using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.Localization;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Apps.XmlUI;
using HikExternalCall.ImageProcess;
using HikPcUI.ImageView;
using VM.Controls;
using VM.Core;
using VM.ModuleControl;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSGluePathConductModuControl;

public partial class PathDrawingDialog : BaseWindow, IComponentConnector, IStyleConnector
{
	public PathGuidData pathGuidData = new PathGuidData();

	private List<string> history = new List<string>();

	private int historyIndex = -1;

	internal string OldParamValues;

	private object _ParentDataSource;

	private ImageData _Image;

	public static readonly DependencyProperty HasModelProperty = DependencyProperty.Register("ModelHasBeenCreated", typeof(bool), typeof(PathDrawingDialog), new PropertyMetadata(false));

	private bool hasParamValueChanged = false;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	private List<object> paths = new List<object>();

	private object selectedPath = new List<object>();

	private bool ignoreDelete = false;

	internal ParamListView paramList;

	public IVmModule OwnerModule { get; set; }

	public IParamsConfig OwnerModuleParam { get; set; }

	public bool BackEnable => history.Count > historyIndex && historyIndex > 0;

	public bool ForwardEnable => history.Count > historyIndex + 1 && historyIndex > -2;

	public object ParentDataSource
	{
		get
		{
			return _ParentDataSource;
		}
		set
		{
			if (_ParentDataSource != value)
			{
				_ParentDataSource = value;
				OnPropertyChanged("ParentDataSource");
			}
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
				pathGuidData.CurImageData = value;
				imageView.ImageDataSource = value;
				if (IsLoaded)
				{
					historyIndex = -1;
					RecordHistory();
				}
			}
		}
	}

	public bool ModelHasBeenCreated
	{
		get
		{
			return (bool)GetValue(HasModelProperty);
		}
		set
		{
			SetValue(HasModelProperty, value);
		}
	}

	public List<object> Paths
	{
		get
		{
			return paths;
		}
		private set
		{
			if (paths != value)
			{
				paths = value;
				OnPropertyChanged("Paths");
			}
		}
	}

	public object SelectedPath
	{
		get
		{
			return selectedPath;
		}
		set
		{
			if (selectedPath != value)
			{
				selectedPath = value;
				imageView.SelectionService.SelectItem(selectedPath);
				OnPropertyChanged("SelectedPath");
			}
		}
	}

	public PathDrawingDialog(IVmModule module, IParamsConfig paramsConfig, object parentDataSource, bool hasModel)
	{
		OwnerModule = module;
		OwnerModuleParam = paramsConfig;
		ModelHasBeenCreated = hasModel;
		ParentDataSource = parentDataSource;
		pathGuidData.ParamTab = (Tab)parentDataSource;
		InitializeComponent();
		Init();
	}

	public void Init()
	{
		InitSubParamVisibility();
		HikImageView hikImageView = imageView;
		hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView2.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
		HikImageView hikImageView3 = imageView;
		hikImageView3.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Combine(hikImageView3.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
		HikImageView hikImageView4 = imageView;
		hikImageView4.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView4.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		HikImageView hikImageView5 = imageView;
		hikImageView5.OnSelectedBaseROIChange = (HikImageView.SelectedBaseROIChange)Delegate.Combine(hikImageView5.OnSelectedBaseROIChange, new HikImageView.SelectedBaseROIChange(SelectedBaseROIChange));
		HikImageView hikImageView6 = imageView;
		hikImageView6.OnFreshExtendContent = (HikImageView.FreshExtendContent)Delegate.Combine(hikImageView6.OnFreshExtendContent, new HikImageView.FreshExtendContent(imageView_OnFreshExtendContent));
		imageView.PastCopyEnable = false;
		HikPath.IsShowArrow = true;
		mainGrid.DataContext = pathGuidData;
		foreach (object item in (ParentDataSource as Tab).Collection)
		{
			if (item is NameValueItem nameValueItem)
			{
				nameValueItem.OnCurValueChanged -= OnParamValueChanged;
				nameValueItem.OnCurValueChanged += OnParamValueChanged;
			}
		}
		Paths = pathGuidData.ItemsData.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
	}

	private void OnParamValueChanged(object sender, EventArgs e)
	{
		hasParamValueChanged = true;
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		if (imageView.Source != null)
		{
			imageView.Source = b2SConvertor.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
		}
	}

	private void InitSubParamVisibility()
	{
		if (OwnerModuleParam == null || !(ParentDataSource is Tab) || (ParentDataSource as Tab).CategorysA.Count <= 0)
		{
			return;
		}
		ObservableCollection<object> observableCollection = ((ParentDataSource as Tab).CategorysA[0] as Apps.XmlParser.ParamTab.Category)?.ItemsA;
		string text = ((IUserStringData)OwnerModuleParam)["ModPointMode"];
		if (!(text == "0"))
		{
			return;
		}
		foreach (object item in observableCollection)
		{
			if (item is NameValueItem nameValueItem && nameValueItem.NameA != "ModMaxPointNum" && nameValueItem.NameA != "ModPointOffsetLen")
			{
				nameValueItem.CustomVisibleA = false;
			}
		}
	}

	public override void Close()
	{
		Deinit();
		base.Close();
	}

	private void Deinit()
	{
		if (ParentDataSource is Tab && (ParentDataSource as Tab).CategorysA.Count > 0)
		{
			ObservableCollection<object> observableCollection = ((ParentDataSource as Tab).CategorysA[0] as Apps.XmlParser.ParamTab.Category)?.ItemsA;
			foreach (object item in observableCollection)
			{
				if (item is NameValueItem nameValueItem)
				{
					nameValueItem.OnCurValueChanged -= OnParamValueChanged;
					nameValueItem.CustomVisibleA = true;
				}
			}
		}
		HikPath.IsShowArrow = false;
		imageView.Dispose();
		DataContext = null;
		imageView.DataContext = null;
	}

	private void SelectedBaseROIChange(object sender, object Data)
	{
		if (Data == null || !Paths.Contains(Data))
		{
			((UIElement)(object)paramList).Visibility = Visibility.Collapsed;
			return;
		}
		((UIElement)(object)paramList).Visibility = Visibility.Visible;
		int num = Paths.IndexOf(Data);
		if (OwnerModule != null && num >= 0)
		{
			if (selectedPath != Data)
			{
				selectedPath = Data;
				OnPropertyChanged("SelectedPath");
			}
			if (OwnerModuleParam != null)
			{
				((IUserStringData)OwnerModuleParam)["SelectedPathIndex"] = num.ToString();
			}
			(ParentDataSource as Tab)?.GetItemsValueUnit();
		}
		if (!(Data is HikPolylineRoiParameter hikPolylineRoiParameter))
		{
			return;
		}
		uint num2 = 1u;
		foreach (PolygonPoint pathPoint in hikPolylineRoiParameter.PathPoints)
		{
			pathPoint.Index = num2;
			num2++;
		}
	}

	private void imageView_OnRoiParamChange(object Date)
	{
		if (!IsLoaded)
		{
			return;
		}
		if (Date is HikIndexPointRoiParameter hikIndexPointRoiParameter)
		{
			hikIndexPointRoiParameter.IsMoved = true;
			if (ModelHasBeenCreated)
			{
				UpdateEdgePoints();
				PathDrawingGuide.SetModelEdgePoint(OwnerModuleParam, pathGuidData);
			}
		}
		else if (Date is HikPolylineRoiParameter || Date is HikCircleArcRoiParameter)
		{
			ModelHasBeenCreated = false;
		}
		RecordHistory();
	}

	private void UpdateEdgePoints()
	{
		ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> observableCollection = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
		int i;
		for (i = 0; i < pathGuidData.PointsData.Count; i++)
		{
			MVBPATMATCH_EDGEPOINT_OUT item = default;
			if (pathGuidData.ItemsData.FirstOrDefault((object p) => p is HikIndexPointRoiParameter hikIndexPointRoiParameter2 && hikIndexPointRoiParameter2.Index == i) is HikIndexPointRoiParameter hikIndexPointRoiParameter)
			{
				item.x = (float)hikIndexPointRoiParameter.CenterPoint.X;
				item.y = (float)hikIndexPointRoiParameter.CenterPoint.Y;
				item.score_flag = (hikIndexPointRoiParameter.IsMoved ? 2 : 0);
			}
			else
			{
				item.score_flag = 1;
			}
			observableCollection.Add(item);
		}
		pathGuidData.PointsData = observableCollection;
	}

	private void imageView_OnRoiCountChanged(object Roi, EventArgs e = null)
	{
		Arrow.IsChecked = true;
		Paths = pathGuidData.ItemsData.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
		UpdataTrajName();
	}

	private void imageView_OnRoiDeCountChange(object Roi)
	{
		if (ignoreDelete)
		{
			return;
		}
		int num = Paths.IndexOf(Roi);
		if (OwnerModule != null && num >= 0 && OwnerModuleParam != null)
		{
			((IUserStringData)OwnerModuleParam)["DeletePath"] = num.ToString();
		}
		if (Roi == SelectedPath)
		{
			SelectedPath = null;
			((UIElement)(object)paramList).Visibility = Visibility.Collapsed;
		}
		Paths = pathGuidData.ItemsData.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
		if (Roi is HikIndexPointRoiParameter)
		{
			if (ModelHasBeenCreated)
			{
				UpdateEdgePoints();
				PathDrawingGuide.SetModelEdgePoint(OwnerModuleParam, pathGuidData);
			}
		}
		else if (Roi is HikPolylineRoiParameter || Roi is HikCircleArcRoiParameter)
		{
			ModelHasBeenCreated = false;
		}
		RecordHistory();
		UpdataTrajName();
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
		xVal.Content = content;
		yVal.Content = content2;
		grayVal.Content = b.ToString();
	}

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		if (Paths.Count >= 10)
		{
			HikMessageBox.Show("Lang_ParamSet", "Lang_PathNumTooLong", MessageIcons.Warning, isTranslate: true);
			(sender as RadioButton).IsChecked = false;
			return;
		}
		string name = (sender as FrameworkElement).Name;
		if (!(name == "hikPath"))
		{
			if (name == "hikCoverCircle")
			{
				imageView.PrepareRawRoi(RoiUIType.HikCircleArcRoi);
				ModelHasBeenCreated = false;
			}
			else
			{
				imageView.UnPrepareRawRoi();
			}
		}
		else
		{
			imageView.PrepareRawRoi(RoiUIType.HikPath);
			ModelHasBeenCreated = false;
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		imageView.UnPrepareRawRoi();
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

	private void Match_Click(object sender, RoutedEventArgs e)
	{
		ModelHasBeenCreated = false;
		hikPath.IsChecked = false;
		hikCoverCircle.IsChecked = false;
		if (!PathDrawingGuide.CreateModel(OwnerModuleParam, ref pathGuidData, popError: true))
		{
			return;
		}
		if (Paths != null && selectedPath != null)
		{
			int num = Paths.IndexOf(selectedPath);
			if (num >= 0)
			{
				if (OwnerModuleParam != null)
				{
					((IUserStringData)OwnerModuleParam)["SelectedPathIndex"] = num.ToString();
				}
				(ParentDataSource as Tab)?.GetItemsValueUnit();
			}
		}
		UpdatePointROI();
		OldParamValues = pathGuidData.ParamValues;
		hasParamValueChanged = false;
		ModelHasBeenCreated = true;
	}

	private void UpdataTrajName()
	{
		for (int i = 0; i < Paths.Count; i++)
		{
			if (Paths[i] is HikPolylineRoiParameter)
			{
				(Paths[i] as HikPolylineRoiParameter).Name = AppLocalizationService.GetLang("Lang_Path") + (i + 1);
			}
			else if (Paths[i] is HikCircleArcRoiParameter)
			{
				(Paths[i] as HikCircleArcRoiParameter).Name = AppLocalizationService.GetLang("Lang_Path") + (i + 1);
			}
		}
	}

	public void UpdatePointROI()
	{
		HikImageView hikImageView = imageView;
		hikImageView.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Remove(hikImageView.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView2.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		HikImageView hikImageView3 = imageView;
		hikImageView3.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Remove(hikImageView3.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (object itemsDatum in pathGuidData.ItemsData)
		{
			if (itemsDatum is HikPolylineRoiParameter || itemsDatum is HikCircleArcRoiParameter)
			{
				observableCollection.Add(itemsDatum);
			}
		}
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in pathGuidData.PointsData)
		{
			if (pointsDatum.score_flag != 1)
			{
				System.Windows.Point centerPoint = new System.Windows.Point(pointsDatum.x, pointsDatum.y);
				HikIndexPointRoiParameter item = new HikIndexPointRoiParameter
				{
					CenterPoint = centerPoint,
					Index = pathGuidData.PointsData.IndexOf(pointsDatum),
					IsMoved = (pointsDatum.score_flag == 2)
				};
				observableCollection.Add(item);
			}
		}
		pathGuidData.ItemsData = observableCollection;
		HikImageView hikImageView4 = imageView;
		hikImageView4.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView4.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
		HikImageView hikImageView5 = imageView;
		hikImageView5.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView5.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		HikImageView hikImageView6 = imageView;
		hikImageView6.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Combine(hikImageView6.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
		OnPropertyChanged("SelectedPath");
	}

	private void Clear_Click(object sender, RoutedEventArgs e)
	{
		if (pathGuidData.ItemsData == null)
		{
			pathGuidData.ItemsData = new ObservableCollection<object>();
		}
		if (pathGuidData.ItemsData.Count != 0)
		{
			RecordHistory(isClear: true);
			DoClear();
		}
	}

	private void DoClear()
	{
		((IUserStringData)OwnerModuleParam)["ClearPath"] = 0.ToString();
		ModelHasBeenCreated = false;
		pathGuidData.ItemsData.Clear();
		pathGuidData.PointsData.Clear();
		Paths = new List<object>();
		((UIElement)(object)paramList).Visibility = Visibility.Collapsed;
	}

	private void BackStep_Click(object sender, RoutedEventArgs e)
	{
		if (history.Count > historyIndex && historyIndex > 0)
		{
			historyIndex--;
			string path = history[historyIndex];
			ChangeHistory(path);
			PathDrawingGuide.SetModelParams(OwnerModuleParam, pathGuidData);
		}
		OnPropertyChanged("BackEnable");
		OnPropertyChanged("ForwardEnable");
	}

	private void ForwardStep_Click(object sender, RoutedEventArgs e)
	{
		if (history.Count > historyIndex + 1 && historyIndex > -2)
		{
			historyIndex++;
			string path = history[historyIndex];
			ChangeHistory(path);
			PathDrawingGuide.SetModelParams(OwnerModuleParam, pathGuidData);
		}
		OnPropertyChanged("BackEnable");
		OnPropertyChanged("ForwardEnable");
	}

	private void ChangeHistory(string path)
	{
		PathGuidData pathGuidData = new PathGuidData();
		if (pathGuidData != null)
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
			HikImageView hikImageView2 = imageView;
			hikImageView2.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Remove(hikImageView2.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
			HikImageView hikImageView3 = imageView;
			hikImageView3.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Remove(hikImageView3.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
			pathGuidData.loadXml(path);
			this.pathGuidData.ItemsData = pathGuidData.ItemsData;
			this.pathGuidData.ParamValues = pathGuidData.ParamValues;
			Paths = this.pathGuidData.ItemsData.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
			UpdataTrajName();
			ModelHasBeenCreated = pathGuidData.HasModel;
			if (ModelHasBeenCreated)
			{
				UpdateEdgePoints();
				PathDrawingGuide.SetModelEdgePoint(OwnerModuleParam, this.pathGuidData);
			}
			HikImageView hikImageView4 = imageView;
			hikImageView4.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView4.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
			HikImageView hikImageView5 = imageView;
			hikImageView5.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView5.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
			HikImageView hikImageView6 = imageView;
			hikImageView6.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Combine(hikImageView6.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
		}
	}

	public void RecordHistory(bool isClear = false)
	{
		try
		{
			PathDrawingGuide.GetModelParamsConfig(OwnerModuleParam, this.pathGuidData, (Tab)ParentDataSource);
			if (isClear)
			{
				PathGuidData pathGuidData = new PathGuidData();
				string fileName = history[historyIndex];
				pathGuidData.loadXml(fileName);
				pathGuidData.ItemsData = this.pathGuidData.ItemsData;
				pathGuidData.HasModel = ModelHasBeenCreated;
				pathGuidData.ParamValues = this.pathGuidData.ParamValues;
				pathGuidData.Save(fileName);
			}
			PathGuidData pathGuidData2 = new PathGuidData
			{
				ItemsData = (isClear ? new ObservableCollection<object>() : this.pathGuidData.ItemsData),
				HasModel = (!isClear && ModelHasBeenCreated),
				ParamValues = (isClear ? null : this.pathGuidData.ParamValues)
			};
			string text = Path.GetFullPath("./Temporary") + "\\DrawingPathModel_" + (historyIndex + 1) + ".xml";
			pathGuidData2.Save(text);
			historyIndex++;
			history.Insert(historyIndex, text);
			history.RemoveRange(historyIndex + 1, history.Count - (historyIndex + 1));
			OnPropertyChanged("BackEnable");
			OnPropertyChanged("ForwardEnable");
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error, isTranslate: true);
		}
	}

	private void Finish_OnClick(object sender, RoutedEventArgs e)
	{
		if (!ModelHasBeenCreated)
		{
			ModelHasBeenCreated = PathDrawingGuide.CreateModel(OwnerModuleParam, ref pathGuidData, popError: true);
			if (!ModelHasBeenCreated)
			{
				return;
			}
		}
		if (CompareAlgorithmTab() || PathDrawingGuide.CreateModel(OwnerModuleParam, ref pathGuidData, popError: true))
		{
			DialogResult = true;
			Close();
		}
	}

	private bool CompareAlgorithmTab()
	{
		PathDrawingGuide.GetModelParamsConfig(OwnerModuleParam, pathGuidData, (Tab)ParentDataSource);
		if (OldParamValues == pathGuidData.ParamValues)
		{
			return true;
		}
		return false;
	}

	private void CurImgButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			GetCurImage();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public void GetCurImage()
	{
		if (OwnerModule == null)
		{
			CurImgBtn.Visibility = Visibility.Collapsed;
		}
		else
		{
			if (!(VmSolution.Instance[(OwnerModule as IVmFullPath).FullPath + ".[Display]"] is ParamRoot paramRoot))
			{
				return;
			}
			IList<object> displayList = paramRoot.GetDisplayList();
			foreach (Apps.XmlParser.Object item in displayList)
			{
				if (item.TypeA == "image" || item.TypeA == "pixelinfoimage")
				{
					ImageData imageData = (ImageData)item.GetValue(uint.MaxValue);
					if (imageData != null && imageData.ImageBuffer == null)
					{
						imageData.ImageBuffer = Byte2ImageSource.GetImageBufferByFileName(imageData.MemoryAddress, imageData.Width, imageData.Height, imageData.PixelFormat);
						imageData = ((imageData.ImageBuffer == null) ? null : imageData);
					}
					if (imageData != null && imageData.PixelFormat != PixelFormats.Gray8)
					{
						throw new Exception(AppLocalizationService.GetLang("Lang_UnsupportFormatWarning"));
					}
					CurImage = imageData;
					DoClear();
					break;
				}
			}
		}
	}

	private void OtherImgButton_Click(object sender, RoutedEventArgs e)
	{
		string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			ImageProcessManager.GetMono8ImageDataByFileDialog(ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer, ref path);
			if (imageData.ImageBuffer != null)
			{
				DoClear();
				CurImage = imageData;
			}
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), OwnerModule?.ModuleDir);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner, AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
		}
	}

	private void Up_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: var dataContext } && Paths.Contains(dataContext) && Paths.IndexOf(dataContext) > 0)
		{
			ignoreDelete = true;
			if (OwnerModuleParam != null)
			{
				((IUserStringData)OwnerModuleParam)["ChangeOrder"] = Paths.IndexOf(dataContext) + "-0";
			}
			object item = Paths[Paths.IndexOf(dataContext) - 1];
			int index = pathGuidData.ItemsData.IndexOf(item);
			pathGuidData.ItemsData.Remove(dataContext);
			pathGuidData.ItemsData.Insert(index, dataContext);
			Paths = pathGuidData.ItemsData.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
			ignoreDelete = false;
		}
	}

	private void Down_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: var dataContext } && Paths.Contains(dataContext) && Paths.IndexOf(dataContext) < Paths.Count - 1)
		{
			ignoreDelete = true;
			if (OwnerModuleParam != null)
			{
				((IUserStringData)OwnerModuleParam)["ChangeOrder"] = Paths.IndexOf(dataContext) + "-1";
			}
			object item = Paths[Paths.IndexOf(dataContext) + 1];
			int index = pathGuidData.ItemsData.IndexOf(dataContext);
			pathGuidData.ItemsData.Remove(item);
			pathGuidData.ItemsData.Insert(index, item);
			Paths = pathGuidData.ItemsData.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
			ignoreDelete = false;
		}
	}

	private void InitFixtureInfo_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (OwnerModuleParam != null)
			{
				((IUserStringData)OwnerModuleParam)["InitFixtureInfo"] = "";
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ReferenceChange"), MessageButtons.Normal, MessageIcons.Ok);
			}
		}
		catch (Exception)
		{
		}
	}
}
