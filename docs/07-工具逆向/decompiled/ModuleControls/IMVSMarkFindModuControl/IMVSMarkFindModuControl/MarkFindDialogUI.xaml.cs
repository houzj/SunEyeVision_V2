using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using HikExternalCall.ImageProcess;
using HikExternalCall.PatCreate;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMarkFindModuControl;

public partial class MarkFindDialogUI : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public Window Owner;

	public MarkFindDialogData featurepatMatchDialogData;

	private HikCross Cross;

	public const string curVersion = "V3.0.0 build0927";

	private bool isInit = true;

	private bool ClickRoiFlag = true;

	private bool IsSetCrossByManual = false;

	private int historyIndex = -1;

	private List<string> history = new List<string>();

	public List<object> needSkipData = new List<object>();

	public byte[] ModelData;

	private ImageData _Image;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	public bool NeedBuild;

	private System.Windows.Controls.Image tempImage = new System.Windows.Controls.Image();

	private Byte2ImageSource MaskSourceConvertor = new Byte2ImageSource();

	private Assembly assembly = null;

	private const int COLOR_GREEN = 65280;

	private const string COLOR_GREEN_STRING = "#00ff00";

	private int _PanRectWidth = 10;

	private Rubber2 rubber;

	private const uint MaskSignColor = 1291786778u;

	private const uint HighLightMaskSignColor = 2583632410u;

	public bool IsChangingModel = false;

	public IParamsConfig OwnerModuleParam { get; set; }

	public IVmModule OwnerModule { get; set; }

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
				GetBackForewordEnable();
			}
		}
	}

	public Assembly Assembly
	{
		get
		{
			if (assembly == null)
			{
				string currentPath = CurrentPathHelper.Instance.GetCurrentPath();
				currentPath += "Module(sp)\\x64\\.CommonControl\\PatCreateCSharp.dll";
				try
				{
					assembly = Assembly.UnsafeLoadFrom(currentPath);
				}
				catch (Exception arg)
				{
					LogHelper.objLog.Error($"Get assembly error:{arg},assembly path={currentPath}");
				}
			}
			return assembly;
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

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public MarkFindDialogUI()
	{
		InitializeComponent();
		Init();
	}

	private void Init()
	{
		NeedBuild = true;
		HikImageView hikImageView = imageView;
		hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Remove(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView2.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
		featurepatMatchDialogData = new MarkFindDialogData();
		CollectionChangedEventManager.AddHandler(featurepatMatchDialogData.ItemsData, ItemsData_CollectionChanged);
		HikImageView hikImageView3 = imageView;
		hikImageView3.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Remove(hikImageView3.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
		HikImageView hikImageView4 = imageView;
		hikImageView4.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(hikImageView4.OnRoiCountChanged, new HikImageView.RoiCountChanged(imageView_OnRoiCountChanged));
		HikImageView hikImageView5 = imageView;
		hikImageView5.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Remove(hikImageView5.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
		HikImageView hikImageView6 = imageView;
		hikImageView6.OnRoiDeCountChanged = (HikImageView.RoiDeCountChanged)Delegate.Combine(hikImageView6.OnRoiDeCountChanged, new HikImageView.RoiDeCountChanged(imageView_OnRoiDeCountChange));
		HikImageView hikImageView7 = imageView;
		hikImageView7.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView7.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		HikImageView hikImageView8 = imageView;
		hikImageView8.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView8.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		imageView.MouseLeftButtonDown -= imageView_MouseLeftButtonDown;
		imageView.MouseLeftButtonDown += imageView_MouseLeftButtonDown;
		HikImageView hikImageView9 = imageView;
		hikImageView9.OnRoi_Click = (HikImageView.Roi_Click)Delegate.Remove(hikImageView9.OnRoi_Click, new HikImageView.Roi_Click(imageView_OnRoiClick));
		HikImageView hikImageView10 = imageView;
		hikImageView10.OnRoi_Click = (HikImageView.Roi_Click)Delegate.Combine(hikImageView10.OnRoi_Click, new HikImageView.Roi_Click(imageView_OnRoiClick));
		HikImageView hikImageView11 = imageView;
		hikImageView11.OnFreshExtendContent = (HikImageView.FreshExtendContent)Delegate.Remove(hikImageView11.OnFreshExtendContent, new HikImageView.FreshExtendContent(imageView_OnFreshExtendContent));
		HikImageView hikImageView12 = imageView;
		hikImageView12.OnFreshExtendContent = (HikImageView.FreshExtendContent)Delegate.Combine(hikImageView12.OnFreshExtendContent, new HikImageView.FreshExtendContent(imageView_OnFreshExtendContent));
		DataContext = featurepatMatchDialogData;
		imageView.DataContext = featurepatMatchDialogData;
		tempImage.MouseLeftButtonUp -= tempImage_PreviewMouseLeftButtonUp;
		tempImage.MouseLeftButtonUp += tempImage_PreviewMouseLeftButtonUp;
		tempImage.PreviewKeyUp -= tempImage_PreviewKeyUp;
		tempImage.PreviewKeyUp += tempImage_PreviewKeyUp;
		tempImage.MouseEnter -= tempImage_MouseEnter;
		tempImage.MouseEnter += tempImage_MouseEnter;
		tempImage.MouseMove -= tempImage_MouseMove;
		tempImage.MouseMove += tempImage_MouseMove;
		tempImage.MouseWheel -= tempImage_MouseWheel;
		tempImage.MouseWheel += tempImage_MouseWheel;
		tempImage.IsHitTestVisible = false;
		tempImage.Focusable = true;
		tempImage.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
		Loaded += MarkFindDialogUI_Loaded;
		if (DataContext != null)
		{
			(DataContext as MarkFindDialogData).MatchCommand -= HPFeaturepatMatchDialogUI_MatchCommand;
			(DataContext as MarkFindDialogData).MatchCommand += HPFeaturepatMatchDialogUI_MatchCommand;
		}
		imageView.AddAboveChild(tempImage);
		Panel.SetZIndex(tempImage, 100);
		if (Cross == null)
		{
			Cross = new HikCross();
			Binding binding = new Binding();
			binding.Mode = BindingMode.TwoWay;
			binding.Path = new PropertyPath("FixPointX");
			BindingExpressionBase bindingExpressionBase = Cross.SetBinding(HikCross.XProperty, binding);
			binding = new Binding();
			binding.Mode = BindingMode.TwoWay;
			binding.Path = new PropertyPath("FixPointY");
			BindingExpressionBase bindingExpressionBase2 = Cross.SetBinding(HikCross.YProperty, binding);
		}
		Cross.RegisterRoi(imageView);
		Cross.UnRegisterRoi();
		featurepatMatchDialogData.version = "V3.0.0 build0927";
		DataContextChanged -= MarkFindDialogUI_DataContextChanged;
		DataContextChanged += MarkFindDialogUI_DataContextChanged;
	}

	private void MarkFindDialogUI_Loaded(object sender, RoutedEventArgs e)
	{
		CheckToolTip();
	}

	private void MarkFindDialogUI_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue != null && e.OldValue is MarkFindDialogData markFindDialogData)
		{
			markFindDialogData.MatchCommand -= HPFeaturepatMatchDialogUI_MatchCommand;
		}
		if (e.NewValue != null && e.NewValue is MarkFindDialogData markFindDialogData2)
		{
			markFindDialogData2.MatchCommand -= HPFeaturepatMatchDialogUI_MatchCommand;
			markFindDialogData2.MatchCommand += HPFeaturepatMatchDialogUI_MatchCommand;
		}
	}

	protected void HPFeaturepatMatchDialogUI_MatchCommand()
	{
		MatchSelfExecute();
	}

	public void HideCurImageButton()
	{
		BtnCurImage.Visibility = Visibility.Collapsed;
	}

	private void tempImage_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Delete)
		{
			byte[] otherMask = featurepatMatchDialogData.otherMask;
			if (otherMask != null)
			{
				ReViewMaskArea(otherMask);
				tempImage.Tag = null;
				RecordHistory();
			}
		}
	}

	private void ReViewMaskArea(byte[] otherMask)
	{
		if (otherMask != null)
		{
			for (int i = 0; i < otherMask.Length; i++)
			{
				if (otherMask[i] > 10)
				{
					otherMask[i] = 0;
				}
			}
		}
		DrawMask();
	}

	private void tempImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		DrawMask();
	}

	private void imageView_OnRoiClick(object sender, MouseEventArgs e)
	{
		ClickRoiFlag = false;
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

	private void imageView_OnRoiCountChanged(object Roi, EventArgs e = null)
	{
		IsSetCrossByManual = true;
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
			return;
		}
		RecordHistory();
		Dispatcher.BeginInvoke((Action)(() =>
		{
			MatchSelfExecute();
		}));
	}

	protected void MatchSelfExecute()
	{
		ICatchCoreInfo obj = OwnerModule as ICatchCoreInfo;
		if (obj != null && obj.IsModelTrigger())
		{
			MatchFun();
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
			foreach (RoiParameter itemsDatum in featurepatMatchDialogData.ItemsData)
			{
				itemsDatum.IsSelected = false;
			}
		}
		byte[] otherMask = featurepatMatchDialogData.otherMask;
		if (otherMask == null || otherMask.Count() <= num)
		{
			return;
		}
		byte b = otherMask[num];
		if (b > 0 && b < 10)
		{
			ReFreshMask(otherMask);
			uint color = 2583632410u;
			setUnicomColor(num, otherMask, color, b);
			DrawMask();
			tempImage.Focus();
			if (!ClickRoiFlag)
			{
				foreach (RoiParameter itemsDatum2 in featurepatMatchDialogData.ItemsData)
				{
					itemsDatum2.IsSelected = false;
				}
			}
		}
		else if (b == 0)
		{
			ReFreshMask(otherMask);
		}
		ClickRoiFlag = true;
	}

	private void setUnicomColor(int index, byte[] otherMask, uint color, byte masknum)
	{
		int[] BorderArray = new int[1];
		if (index > 0 && index < otherMask.Length && IsSamePoint(otherMask[index], masknum))
		{
			SetHighLightColor(index, otherMask, color);
			BorderArray[0] = index;
			int Dindex = 1;
			while (Dindex > 0)
			{
				setUnicomColor(otherMask, color, masknum, ref BorderArray, ref Dindex);
			}
		}
	}

	private void setUnicomColor(byte[] otherMask, uint color, byte masknum, ref int[] BorderArray, ref int Dindex)
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

	private unsafe void imageView_OnPixelChange(int X, int Y)
	{
		string text = "--";
		string text2 = "--";
		if (X >= 0)
		{
			text = X.ToString();
		}
		if (Y >= 0)
		{
			text2 = Y.ToString();
		}
		xyVal.Content = "( " + text + " , " + text2 + " )";
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
		grayVal.Content = b.ToString();
	}

	private void ItemsData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		AutoSetFixPoint();
	}

	private void AutoSetFixPoint()
	{
		if (featurepatMatchDialogData == null)
		{
			return;
		}
		if (featurepatMatchDialogData.ItemsData.Count > 0)
		{
			if (IsSetCrossByManual)
			{
				return;
			}
			PointCollection pointCollection = new PointCollection();
			foreach (RoiParameter itemsDatum in featurepatMatchDialogData.ItemsData)
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
			featurepatMatchDialogData.FixPointX = pointCollection.Sum((System.Windows.Point r) => r.X) / (double)pointCollection.Count;
			featurepatMatchDialogData.FixPointY = pointCollection.Sum((System.Windows.Point r) => r.Y) / (double)pointCollection.Count;
		}
		else
		{
			IsSetCrossByManual = false;
			featurepatMatchDialogData.FixPointX = 0.0;
			featurepatMatchDialogData.FixPointY = 0.0;
		}
	}

	private void GetBackForewordEnable()
	{
		if (featurepatMatchDialogData != null)
		{
			featurepatMatchDialogData.BackEnable = history.Count > historyIndex && historyIndex > 0;
			featurepatMatchDialogData.ForwardEnable = history.Count > historyIndex + 1 && historyIndex > -2;
		}
	}

	private void RecordHistory()
	{
		try
		{
			if (featurepatMatchDialogData.ItemsData.Count != 0)
			{
				MarkFindDialogData markFindDialogData = new MarkFindDialogData
				{
					ItemsData = featurepatMatchDialogData.ItemsData,
					otherMask = featurepatMatchDialogData.otherMask
				};
				string text = Path.GetFullPath("./Temporary") + "\\FeaturepatMatch_" + (historyIndex + 1) + ".xml";
				((BaseNode)(object)markFindDialogData).Save(text);
				historyIndex++;
				history.Insert(historyIndex, text);
				history.RemoveRange(historyIndex + 1, history.Count - (historyIndex + 1));
				GetBackForewordEnable();
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"RecordHistoryError:{ex.Message}");
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public void ShowImage()
	{
		if (CurImage != null)
		{
			try
			{
				imageView.ImageDataSource = CurImage;
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error($"SetImageError:{ex.Message}");
			}
		}
	}

	public void Window_Closing(object sender, CancelEventArgs e)
	{
		e.Cancel = windowShadow.Visibility == Visibility.Visible;
		if (DataContext != null)
		{
			(DataContext as MarkFindDialogData).MatchCommand -= HPFeaturepatMatchDialogUI_MatchCommand;
		}
		DataContextChanged -= MarkFindDialogUI_DataContextChanged;
	}

	private void Button_Click_1(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Select current image button click.");
		try
		{
			GetCurImage();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
			LogHelper.objLog.Error($"Get curImage error:{ex.Message}");
			return;
		}
		if (CurImage != null && CurImage.ImageBuffer != null)
		{
			InitData();
			DoClear();
			needSkipData.Clear();
			InitGraphicPositionMask();
		}
		LogHelper.objLog.Info("Select current image button done.");
	}

	public void GetCurImage()
	{
		if (OwnerModule == null || OwnerModuleParam == null)
		{
			return;
		}
		string path = Path.GetDirectoryName(OwnerModuleParam.XmlPath) + $"\\{OwnerModule.ModuleTypeName}Display.xml";
		ParamRoot paramRoot = new ParamRoot(path);
		paramRoot.Owner = OwnerModule;
		if (paramRoot == null)
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

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Select other image buuton click.");
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
			ImageProcessManager.GetMono8ImageDataByFileDialog(ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer, ref path);
			if (imageData.ImageBuffer != null)
			{
				InitData();
				DoClear();
				CurImage = imageData;
				needSkipData.Clear();
				InitGraphicPositionMask();
			}
			if (File.Exists(path))
			{
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), OwnerModule?.ModuleDir);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"ChooseOtherImageError:{ex.Message}");
			HikMessageBox.Show(Owner, AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
		}
		LogHelper.objLog.Info("Select other image buuton click done.");
	}

	private void InitData()
	{
		NeedBuild = true;
		tempImage.Source = null;
		featurepatMatchDialogData.PointsData.Clear();
		featurepatMatchDialogData.NotifyPropertyChanged("EditOutlinePointsEnable");
		Dispatcher.Invoke(() =>
		{
			imageView.ClearShapes();
			imageView.ClearShapesE();
		});
	}

	private void DoClear()
	{
		if (featurepatMatchDialogData != null)
		{
			featurepatMatchDialogData.otherMask = null;
		}
		ClearMaskData();
	}

	private void ClearMaskData()
	{
		tempImage.Source = null;
		MaskSourceConvertor.Clear();
	}

	private void Match_Click(object sender, RoutedEventArgs e)
	{
		MatchFun();
	}

	private void MatchFun()
	{
		LogHelper.objLog.Info("Create model click.");
		if (Arrow != null)
		{
			Arrow.IsChecked = true;
		}
		ReFreshMask(featurepatMatchDialogData.otherMask);
		FeaturePatCreateByEraser();
		LogHelper.objLog.Info("Create model done.");
	}

	public void FeaturePatCreateByEraser()
	{
		LogHelper.objLog.Info("Use eraser.");
		int num = 0;
		featurepatMatchDialogData.PointsData.Clear();
		featurepatMatchDialogData.NotifyPropertyChanged("EditOutlinePointsEnable");
		num = ShowPoints(featurepatMatchDialogData, needPopTip: true);
		if (num != 0)
		{
			LogHelper.objLog.Error($"Show points fail, using eraser,errorcode={num}");
		}
		LogHelper.objLog.Info("Use eraser done.");
	}

	public int ShowPoints(MarkFindDialogData featurepatMatchData, bool needPopTip, bool needOkPopTip = true, bool needApply = false)
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
				num = -2;
				throw new Exception(AppLocalizationService.GetLang("Lang_NoImageErr"));
			}
			if ((featurepatMatchData.ItemsData == null || featurepatMatchData.ItemsData.Count == 0) && featurepatMatchData.otherMask == null)
			{
				num = -2;
				throw new Exception(AppLocalizationService.GetLang("Lang_NoROIRegion"));
			}
			featurepatMatchDialogData.CurImageData = CurImage;
			Type type = Assembly?.GetType("PatCreateCSharp.PatCreateManagerCSharp");
			PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			dynamic value = property.GetValue(null, null);
			ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> pointsData = null;
			num = value.CreateMarkFindPatE(featurepatMatchData, ref pointsData);
			if (num != 0)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_CreateModelError") + ":" + ErrorCodeInfo.GetErrorInfo(num));
			}
			DrawLinesAndPoints(featurepatMatchData.centerPoint, featurepatMatchData.outputLine);
			featurepatMatchData.PointsData = pointsData;
			MarkFindDialogData.RefreshModelImageData(featurepatMatchData);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"DrawPointsError{ex.Message}");
			if (needPopTip)
			{
				string errorInfo = ((num == -1) ? AppLocalizationService.GetLang("Lang_PatCreateInnerError") : ex.Message);
				CheckToolTip(errorInfo);
			}
		}
		Dispatcher.Invoke(() =>
		{
			windowShadow.Visibility = Visibility.Collapsed;
		});
		return num;
	}

	private void CheckToolTip(string errorInfo = null)
	{
		if (IsLoaded)
		{
			if (!string.IsNullOrEmpty(errorInfo))
			{
				HikMessageBox.Show(Owner, errorInfo, MessageIcons.Error);
			}
			else if (!string.IsNullOrEmpty(featurepatMatchDialogData.HolderMsg))
			{
				HikMessageBox.Show(Owner, featurepatMatchDialogData.HolderMsg, MessageIcons.Error);
				featurepatMatchDialogData.HolderMsg = null;
			}
		}
		else if (!string.IsNullOrEmpty(errorInfo))
		{
			featurepatMatchDialogData.HolderMsg = errorInfo;
		}
	}

	private void GetMaxMinValue(FILTER_POINT_I point, ref uint minX, ref uint minY, ref uint maxX, ref uint maxY)
	{
		if (point.x > maxX)
		{
			maxX = point.x;
		}
		if (point.x < minX)
		{
			minX = point.x;
		}
		if (point.y > maxY)
		{
			maxY = point.y;
		}
		if (point.y < minY)
		{
			minY = point.y;
		}
	}

	public void DrawLinesAndPoints(FILTER_POINT_F centerPoint, FILTER_LINE_I[] lines)
	{
		try
		{
			Dispatcher.Invoke(() =>
			{
				imageView.ClearShapes();
				imageView.ClearShapesE();
				imageView.SetPointWidthBase(12.0);
				imageView.CreatePoint(new System.Windows.Point(centerPoint.x, centerPoint.y), 65280, 1.0, "");
				uint minX = uint.MaxValue;
				uint maxX = 0u;
				uint minY = uint.MaxValue;
				uint maxY = 0u;
				for (int i = 0; i < lines.Length; i++)
				{
					imageView.CreateLineE(lines[i].point_start.x, lines[i].point_start.y, lines[i].point_end.x, lines[i].point_end.y, "#00ff00", "", needTips: false, needLabel: false, 1.0);
					GetMaxMinValue(lines[i].point_start, ref minX, ref minY, ref maxX, ref maxY);
					GetMaxMinValue(lines[i].point_end, ref minX, ref minY, ref maxX, ref maxY);
				}
				imageView.CreateLineE(minX, minY, minX, maxY, "#00ff00", "", needTips: false, needLabel: false, 1.0);
				imageView.CreateLineE(minX, maxY, maxX, maxY, "#00ff00", "", needTips: false, needLabel: false, 1.0);
				imageView.CreateLineE(maxX, maxY, maxX, minY, "#00ff00", "", needTips: false, needLabel: false, 1.0);
				imageView.CreateLineE(maxX, minY, minX, minY, "#00ff00", "", needTips: false, needLabel: false, 1.0);
			});
		}
		catch (Exception ex)
		{
			throw ex;
		}
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
		byte[] bytes = BitConverter.GetBytes(1291786778u);
		for (int i = 0; i < otherMask.Length; i++)
		{
			if (otherMask[i] > 10)
			{
				otherMask[i] -= 10;
			}
		}
	}

	private void fun2_Checked(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Eraser checked.");
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
		LogHelper.objLog.Info("Eraser checked done.");
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

	private void rubber_OnRubberFinish(object sender, object Data)
	{
		RecordHistory();
	}

	private void rubber_OnWipe(int[] indexs, List<System.Windows.Point> bagPoints)
	{
		LogHelper.objLog.Info("Eraser wipe.");
		if (featurepatMatchDialogData == null)
		{
			return;
		}
		byte[] bytes = BitConverter.GetBytes(1291786778u);
		if (featurepatMatchDialogData.otherMask == null)
		{
			featurepatMatchDialogData.otherMask = new byte[CurImage.Width * CurImage.Height];
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			featurepatMatchDialogData.otherMask[num5] = 1;
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
		imageView.Source = b2SConvertor.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
		DrawMask(new System.Drawing.Point(num, num2), num3 - num + 1, num4 - num2 + 1, isNormal: false);
		LogHelper.objLog.Info("Eraser wipe done.");
	}

	private void fun2_Unchecked(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Eraser unchecked.");
		if (rubber != null)
		{
			rubber.UnRegisterRoi(imageView);
		}
		tempImage.IsHitTestVisible = false;
		LogHelper.objLog.Info("Eraser unchecked done.");
	}

	private void clear_Click(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Delete click.");
		if (featurepatMatchDialogData != null)
		{
			if (featurepatMatchDialogData.ItemsData == null)
			{
				featurepatMatchDialogData.ItemsData = new ObservableCollection<object>();
			}
			DoClear();
			RecordHistory();
		}
		LogHelper.objLog.Info("Delete done.");
	}

	private void ChangeHistory(string path)
	{
		MarkFindDialogData markFindDialogData = new MarkFindDialogData();
		if (markFindDialogData != null)
		{
			((BaseNode)(object)markFindDialogData).loadXml(path, false);
			if (markFindDialogData.ItemsData != null)
			{
				needSkipData.AddRange(markFindDialogData.ItemsData);
			}
			featurepatMatchDialogData.ItemsData = markFindDialogData.ItemsData;
			featurepatMatchDialogData.otherMask = markFindDialogData.otherMask;
			FreshOtherMask();
		}
	}

	private void FreshOtherMask()
	{
		if (featurepatMatchDialogData != null)
		{
			if (featurepatMatchDialogData.otherMask != null)
			{
				DrawMask();
			}
			else
			{
				ClearMaskData();
			}
		}
	}

	private void DrawMask()
	{
		if (imageView.Source != null)
		{
			imageView.Source = b2SConvertor.BuiltImageSource(CurImage.ImageBuffer, CurImage.Width, CurImage.Height, CurImage.PixelFormat);
			DrawMask(new System.Drawing.Point(0, 0), (imageView.Source as WriteableBitmap).PixelWidth, (imageView.Source as WriteableBitmap).PixelHeight);
		}
	}

	private unsafe void DrawMask(System.Drawing.Point point, int sWidth, int sHeight, bool isNormal = true)
	{
		if (imageView.Source == null || featurepatMatchDialogData.otherMask == null)
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
		fixed (byte* otherMask = featurepatMatchDialogData.otherMask)
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

	private void BackStep_Click(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Back step.");
		if (history.Count > historyIndex && historyIndex > 0)
		{
			historyIndex--;
			string path = history[historyIndex];
			ChangeHistory(path);
		}
		GetBackForewordEnable();
		LogHelper.objLog.Info("Back step done.");
	}

	private void ForwardStep_Click(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Foreward step.");
		if (history.Count > historyIndex + 1 && historyIndex > -2)
		{
			historyIndex++;
			string path = history[historyIndex];
			ChangeHistory(path);
		}
		GetBackForewordEnable();
		LogHelper.objLog.Info("Foreward step done.");
	}

	private void Button_Click_OK(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Ok button click.");
		ReFreshMask(featurepatMatchDialogData.otherMask);
		int num = 0;
		if (ShowPoints(featurepatMatchDialogData, needPopTip: true, needOkPopTip: false, needApply: true) == 0)
		{
			Deinit();
			Owner.DialogResult = true;
			Owner.Close();
			LogHelper.objLog.Info("Ok button done.");
		}
	}

	public void RefreshByModelData()
	{
		featurepatMatchDialogData.NotifyPropertyChanged("ItemsData");
		featurepatMatchDialogData.NotifyPropertyChanged("FixPointX");
		featurepatMatchDialogData.NotifyPropertyChanged("FixPointY");
		featurepatMatchDialogData.NotifyPropertyChanged("Pyramidscale");
		featurepatMatchDialogData.NotifyPropertyChanged("RoughPyramidscale");
		featurepatMatchDialogData.NotifyPropertyChanged("FinePyramidscale");
		featurepatMatchDialogData.NotifyPropertyChanged("HighThreshold");
		featurepatMatchDialogData.NotifyPropertyChanged("LowThreshold");
		CurImage = featurepatMatchDialogData.CurImageData;
		ShowImage();
		FreshOtherMask();
	}

	private void Deinit()
	{
		Dispose();
		DataContext = null;
		imageView.DataContext = null;
		ModelData = null;
	}

	public void Dispose()
	{
		imageView.Dispose();
	}

	private void zoomOrgBtn_Click(object sender, RoutedEventArgs e)
	{
		LogHelper.objLog.Info("Zoom origin.");
		imageView.Init();
		LogHelper.objLog.Info("Zoom origin done.");
	}

	private void zoomFullBtn_Click(object sender, RoutedEventArgs e)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected Obj, but got Unknown
		LogHelper.objLog.Info("Zoom full.");
		FullScreen val = new FullScreen();
		viewGrid.Children.Remove(imageView);
		val.SetChild((UIElement)imageView);
		((Window)(object)val).ShowDialog();
		val.RemoveChild();
		viewGrid.Children.Add(imageView);
		LogHelper.objLog.Info("Zoom full done.");
	}

	private void Delete_Click(object sender, RoutedEventArgs e)
	{
		if (featurepatMatchDialogData.ItemsData.Contains((sender as FrameworkElement).Tag))
		{
			featurepatMatchDialogData.ItemsData.Remove((sender as FrameworkElement).Tag);
		}
	}

	private void CmbMarkType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is ComboBox)
		{
			LogHelper.objLog.Info($"Change markType, IsChangingMode = {IsChangingModel}.");
			if (!IsChangingModel)
			{
				InitGraphicPositionMask();
			}
			else
			{
				IsChangingModel = false;
			}
			SetMarkMode();
			if (isInit)
			{
				isInit = false;
			}
			LogHelper.objLog.Info("Change markType done.");
		}
	}

	public void InitGraphicPositionMask()
	{
		if (imageView != null && imageView.Source != null)
		{
			switch (cbxMarketType.SelectedIndex)
			{
			case 0:
				ClearOldMask();
				featurepatMatchDialogData.ItemsData.Add(HikCruciform.InitROI(imageView));
				break;
			case 1:
				ClearOldMask();
				featurepatMatchDialogData.ItemsData.Add(HikCoverHollow.InitROI(imageView));
				break;
			}
		}
	}

	protected void SetMarkMode()
	{
		switch (cbxMarketType.SelectedIndex)
		{
		case 0:
			cbRect.Visibility = Visibility.Collapsed;
			cbSolidCross.Visibility = Visibility.Visible;
			cbHollowCross.Visibility = Visibility.Visible;
			if (!isInit)
			{
				(DataContext as MarkFindDialogData)?.SetMarkTypeNoTrigger(2);
			}
			break;
		case 1:
			cbRect.Visibility = Visibility.Visible;
			cbSolidCross.Visibility = Visibility.Collapsed;
			cbHollowCross.Visibility = Visibility.Collapsed;
			(DataContext as MarkFindDialogData)?.SetMarkTypeNoTrigger(3);
			break;
		}
	}

	private void ClearOldMask()
	{
		if (featurepatMatchDialogData.otherMask != null)
		{
			featurepatMatchDialogData.otherMask = null;
			ClearMaskData();
		}
		if (featurepatMatchDialogData.ItemsData != null)
		{
			featurepatMatchDialogData.ItemsData.Clear();
			historyIndex = -1;
			history.Clear();
			RecordHistory();
			GetBackForewordEnable();
		}
	}

	private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is ComboBox)
		{
			if ((sender as ComboBox).SelectedIndex == 0)
			{
				row.Height = new GridLength(0.0, GridUnitType.Pixel);
			}
			else
			{
				row.Height = new GridLength(10.0, GridUnitType.Star);
			}
		}
	}
}
