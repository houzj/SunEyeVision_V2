using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
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
using Hik.Xml;
using HikExternalCall.ImageProcess;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility;
using VMControls.BaseInterface;

namespace IMVSInspectModuControl;

public partial class LocationModelPage : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public byte[] ModelData;

	public List<object> needSkipData = new List<object>();

	public MVBI_BOX_F MatchBox = default;

	private ImageData _Image;

	private System.Windows.Controls.Image tempImage = new System.Windows.Controls.Image();

	private HikCross Cross;

	private bool IsSetCrossByManual = false;

	private bool ClickRoiFlag = true;

	private List<string> history = new List<string>();

	private int historyIndex = -1;

	private Rubber2 rubber;

	private Byte2ImageSource MaskSourceConvertor = new Byte2ImageSource();

	private int _PanRectWidth = 10;

	private FeaturepatMatchDialogData featurepatMatchDialogData;

	private bool _IsInit = false;

	private bool _IsHasCurImage = false;

	internal ResultOverviewPopupButton setBtn;

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

	public double TextFontSize => TrainDataMgr.Instance.IsEnglish ? 12 : 14;

	public event PropertyChangedEventHandler PropertyChanged;

	public LocationModelPage()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!_IsInit)
			{
				FreshOtherMask();
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
			CurImage = TrainDataMgr.Instance.CurImageData;
			needSkipData.Clear();
		}
	}

	private void SelectOtherImage_OnClick(object sender, RoutedEventArgs e)
	{
		string path = ConfigurationHelper.INIGetDefaultOpenPath(TrainDataMgr.Instance.Module?.ModuleDir);
		ImageData imageData = new ImageData();
		imageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			ImageProcessManager.GetMono8ImageDataByFileDialog(ref imageData.Width, ref imageData.Height, ref imageData.ImageBuffer, ref path);
			if (imageData.ImageBuffer != null)
			{
				if (ChangeImageWarning())
				{
					InitData();
					DoClear();
					CurImage = imageData;
					needSkipData.Clear();
				}
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), TrainDataMgr.Instance.Module?.ModuleDir);
			}
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
		TrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: true);
		try
		{
			TrainDataMgr.Instance.IsHasModel = false;
			Arrow.IsChecked = true;
			ReFreshMask(((FeaturepatMatchDataBase)featurepatMatchDialogData).otherMask);
			FeaturePatCreateByEraser();
			GetBoundRect(ref MatchBox);
			if (!SetFixPoint())
			{
				throw new Exception();
			}
			if (!SetModelData())
			{
				throw new Exception();
			}
			TrainDataMgr.Instance.IsHasModel = true;
		}
		catch (Exception ex)
		{
			LogHelper.moduleLog.Error($"Generate model failed.{Environment.NewLine}{ex.StackTrace}");
		}
		TrainDataMgr.Instance.OnSetWindowShadowEvent(bVisible: false);
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
		if (TrainDataMgr.Instance.Owner != null)
		{
			TrainDataMgr.Instance.Owner.xVal.Content = content;
			TrainDataMgr.Instance.Owner.yVal.Content = content2;
			TrainDataMgr.Instance.Owner.grayVal.Content = b.ToString();
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

	private void InitData()
	{
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
		try
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).PointsData.Clear();
			((FeaturepatMatchDataBase)featurepatMatchDialogData).NotifyPropertyChanged("EditOutlinePointsEnable");
			((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData = CurImage;
			featurepatMatchDialogData.CreateModel();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_OCRDefect_TrainModel"), ex.Message, MessageIcons.Error);
			LogHelper.moduleLog.Error($"FeaturePatCreateByEraser error: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
			throw ex;
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
			if (!TrainDataMgr.Instance.IsHasModel)
			{
				((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointX = 0.0;
				((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointY = 0.0;
			}
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
		if (!TrainDataMgr.Instance.IsHasModel)
		{
			return true;
		}
		DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ChangeImageTip", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
		if (dialogResult == DialogResult.Yes || dialogResult == DialogResult.SDKModel)
		{
			TrainDataMgr.Instance.OnClearAllModelEvent();
			if (TrainDataMgr.Instance.Owner != null)
			{
				((UIElement)(object)TrainDataMgr.Instance.Owner.gbBatchTrain).IsEnabled = false;
				TrainDataMgr.Instance.Owner.pageTrainRegion.imageView.ImageDataSource = null;
			}
			TrainDataMgr.Instance.Owner?.ClearDataByRoi(isClearRoi: true);
			TrainDataMgr.Instance.IsHasModel = false;
			return true;
		}
		return false;
	}

	private void Dispose()
	{
		imageView.Dispose();
	}

	private bool GetBoundRect(ref MVBI_BOX_F box)
	{
		box.center_x = 0f;
		box.center_y = 0f;
		box.width = 0f;
		box.height = 0f;
		box.angle = 0f;
		if (featurepatMatchDialogData != null && ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Count > 0)
		{
			PointCollection pointCollection = new PointCollection();
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			int count = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData.Count;
			foreach (RoiParameter itemsDatum in ((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData)
			{
				System.Windows.Point value = default;
				if (itemsDatum is HikCoverRoiParameter)
				{
					HikCoverRoiParameter hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
					value.X = hikCoverRoiParameter.OriginPoint.X + hikCoverRoiParameter.Width / 2.0;
					value.Y = hikCoverRoiParameter.OriginPoint.Y + hikCoverRoiParameter.Height / 2.0;
					if (hikCoverRoiParameter.OriginPoint.X + hikCoverRoiParameter.Width > num)
					{
						num = hikCoverRoiParameter.OriginPoint.X + hikCoverRoiParameter.Width;
					}
					if (hikCoverRoiParameter.OriginPoint.Y + hikCoverRoiParameter.Height > num2)
					{
						num2 = hikCoverRoiParameter.OriginPoint.Y + hikCoverRoiParameter.Height;
					}
					if (count == 1)
					{
						box.angle = (float)hikCoverRoiParameter.Angle;
					}
				}
				else if (itemsDatum is HikPolygonRoiParameter)
				{
					HikPolygonRoiParameter hikPolygonRoiParameter = itemsDatum as HikPolygonRoiParameter;
					double num5 = hikPolygonRoiParameter.PolygonPoints.Min((PolygonPoint r) => r.x);
					double num6 = hikPolygonRoiParameter.PolygonPoints.Min((PolygonPoint r) => r.y);
					double num7 = hikPolygonRoiParameter.PolygonPoints.Max((PolygonPoint r) => r.x);
					double num8 = hikPolygonRoiParameter.PolygonPoints.Max((PolygonPoint r) => r.y);
					value.X = (num5 + num7) / 2.0;
					value.Y = (num6 + num8) / 2.0;
					if (num7 > num)
					{
						num = num7;
					}
					if (num8 > num2)
					{
						num2 = num8;
					}
				}
				else if (itemsDatum is HikCoverCircleRoiParameter)
				{
					HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
					value.X = hikCoverCircleRoiParameter.CenterPoint.X;
					value.Y = hikCoverCircleRoiParameter.CenterPoint.Y;
					if (hikCoverCircleRoiParameter.CenterPoint.X + hikCoverCircleRoiParameter.Radius > num)
					{
						num = hikCoverCircleRoiParameter.CenterPoint.X + hikCoverCircleRoiParameter.Radius;
					}
					if (hikCoverCircleRoiParameter.CenterPoint.Y + hikCoverCircleRoiParameter.Radius > num2)
					{
						num2 = hikCoverCircleRoiParameter.CenterPoint.Y + hikCoverCircleRoiParameter.Radius;
					}
				}
				pointCollection.Add(value);
			}
			num3 = pointCollection.Sum((System.Windows.Point r) => r.X) / (double)pointCollection.Count;
			num4 = pointCollection.Sum((System.Windows.Point r) => r.Y) / (double)pointCollection.Count;
			box.center_x = (float)num3;
			box.center_y = (float)num4;
			box.width = (float)(num - num3) * 2f;
			box.height = (float)(num2 - num4) * 2f;
			return true;
		}
		return false;
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected Obj, but got Unknown
		IsHasCurImage = TrainDataMgr.Instance.CurImageData != null;
		featurepatMatchDialogData = new FeaturepatMatchDialogData();
		featurepatMatchDialogData = moduleTrainData.FeatureMatchData.CopyModel(false);
		CollectionChangedEventManager.AddHandler(((FeaturepatMatchDataBase)featurepatMatchDialogData).ItemsData, ItemsData_CollectionChanged);
		DataContext = featurepatMatchDialogData;
		imageView.DataContext = featurepatMatchDialogData;
		((FeaturepatMatchDataBase)featurepatMatchDialogData).version = FeaturepatMatchDataBase.curVersion;
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData != null)
		{
			CurImage = ((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData;
		}
		else
		{
			CurImage = TrainDataMgr.Instance.CurImageData;
		}
		if (moduleTrainData.MatchBox != null)
		{
			MatchBox.width = (float)moduleTrainData.MatchBox.Width;
			MatchBox.height = (float)moduleTrainData.MatchBox.Height;
			MatchBox.center_x = (float)moduleTrainData.MatchBox.CenterX;
			MatchBox.center_y = (float)moduleTrainData.MatchBox.CenterY;
			MatchBox.angle = (float)moduleTrainData.MatchBox.Angle;
		}
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		if (((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData == null)
		{
			((FeaturepatMatchDataBase)featurepatMatchDialogData).CurImageData = CurImage;
		}
		moduleTrainData.FeatureMatchData = featurepatMatchDialogData;
		if (MatchBox.width > 0f && MatchBox.height > 0f)
		{
			moduleTrainData.MatchBox = new HikCoverRoiParameter
			{
				Width = MatchBox.width,
				Height = MatchBox.height,
				CenterX = MatchBox.center_x,
				CenterY = MatchBox.center_y,
				Angle = MatchBox.angle
			};
		}
		Dispose();
		DataContext = null;
		imageView.DataContext = null;
		ModelData = null;
		CurImage = null;
	}

	private bool SetModelData()
	{
		try
		{
			((IUserBytesData)TrainDataMgr.Instance.Param)["AddMatchModelData"] = ((FeaturepatMatchDataBase)featurepatMatchDialogData).ModelData;
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
			return false;
		}
		return true;
	}

	private bool SetFixPoint()
	{
		try
		{
			((IUserBytesData)TrainDataMgr.Instance.Param)["SetFixPoint"] = TrainDataMgr.Instance.GetPointBuffer((float)((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointX, (float)((FeaturepatMatchDataBase)featurepatMatchDialogData).FixPointY);
		}
		catch
		{
			return false;
		}
		return true;
	}
}
