using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using HikExternalCall.ImageProcess;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.Core;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSEdgeFlawInspModuControl;

public partial class EdgepatMatchDialogWindow : BaseWindow, IComponentConnector
{
	private static int ENWindowWidth = 900;

	private static int ENParamListWidth = 350;

	private static int ENLeftTextWidth = 170;

	public EdgepatMatchData edgepatMatchData = new EdgepatMatchData();

	public bool needRecoverModel = false;

	private List<string> history = new List<string>();

	private int historyIndex = -1;

	private ImageData _Image;

	private object _ParentDataSource;

	public static readonly DependencyProperty LeftTextWidthProperty = DependencyProperty.Register("LeftTextWidth", typeof(int), typeof(EdgepatMatchDialogWindow), new PropertyMetadata(120));

	public static readonly DependencyProperty ParamListWidthProperty = DependencyProperty.Register("ParamListWidth", typeof(int), typeof(EdgepatMatchDialogWindow), new PropertyMetadata(270));

	private System.Windows.Controls.Image maskImage = new System.Windows.Controls.Image();

	private Rubber2 rubber;

	private int _PanRectWidth = 25;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	private const uint MaskSignColor = 1291786778u;

	private const uint HighLightMaskSignColor = 2583632410u;

	private Byte2ImageSource MaskSourceConvertor = new Byte2ImageSource();

	internal ResultOverviewPopupButton setBtn;

	public IVmModule OwnerModule { get; set; }

	public IParamsConfig OwnerModuleParam { get; set; }

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
				edgepatMatchData.CurImageData = value;
				imageView.ImageDataSource = value;
				historyIndex = -1;
				RecordHistory();
			}
		}
	}

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

	public bool BackEnable => history.Count > historyIndex && historyIndex > 0;

	public bool ForwardEnable => history.Count > historyIndex + 1 && historyIndex > -2;

	public int LeftTextWidth
	{
		get
		{
			return (int)GetValue(LeftTextWidthProperty);
		}
		set
		{
			SetValue(LeftTextWidthProperty, value);
		}
	}

	public int ParamListWidth
	{
		get
		{
			return (int)GetValue(ParamListWidthProperty);
		}
		set
		{
			SetValue(ParamListWidthProperty, value);
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
			_PanRectWidth = value;
			OnPropertyChanged("PanRectWidth");
		}
	}

	public EdgepatMatchDialogWindow(object parentDataSource)
	{
		ParentDataSource = parentDataSource;
		InitializeComponent();
		Init();
	}

	private void Init()
	{
		if (AppLocalizationService.GetLang("Language") == "English")
		{
			Width = ENWindowWidth;
			ParamListWidth = ENParamListWidth;
			LeftTextWidth = ENLeftTextWidth;
		}
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
		DataContext = edgepatMatchData;
		InitMaskImage();
		imageView.AddAboveChild(maskImage);
		Panel.SetZIndex(maskImage, 100);
	}

	private void imageView_OnFreshExtendContent(object sender, object Tag)
	{
		DrawMask();
	}

	public override void Close()
	{
		Deinit();
		base.Close();
	}

	private void Deinit()
	{
		imageView.Dispose();
		DataContext = null;
		imageView.DataContext = null;
		dataGrid.DataContext = null;
	}

	private void SelectedBaseROIChange(object sender, object Data)
	{
		HikPolylineRoiParameter hikPolylineRoiParameter = Data as HikPolylineRoiParameter;
		if (hikPolylineRoiParameter != null)
		{
			uint num = 1u;
			foreach (PolygonPoint pathPoint in hikPolylineRoiParameter.PathPoints)
			{
				pathPoint.Index = num;
				num++;
			}
		}
		dataGrid.DataContext = hikPolylineRoiParameter;
	}

	private void myControl_Loaded(object sender, RoutedEventArgs e)
	{
	}

	private void imageView_OnRoiParamChange(object Date)
	{
		if (IsLoaded)
		{
			RecordHistory();
		}
	}

	private void imageView_OnRoiDeCountChange(object Roi)
	{
		RecordHistory();
	}

	private void imageView_OnRoiCountChanged(object Roi, EventArgs e = null)
	{
		if (edgepatMatchData.ItemsData.Count > 10)
		{
			HikMessageBox.Show("Lang_ParamSet", "Lang_PathNumTooLong", MessageIcons.Warning, isTranslate: true);
			int index = edgepatMatchData.ItemsData.Count - 1;
			edgepatMatchData.ItemsData.RemoveAt(index);
		}
		Arrow.IsChecked = true;
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

	private void RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		string name = (sender as FrameworkElement).Name;
		if (name == "hikPath")
		{
			if (edgepatMatchData.ItemsData.Count < 10)
			{
				imageView.PrepareRawRoi(RoiUIType.HikPath);
				return;
			}
			HikMessageBox.Show("Lang_ParamSet", "Lang_PathNumTooLong", MessageIcons.Warning, isTranslate: true);
			(sender as RadioButton).IsChecked = false;
		}
		else
		{
			imageView.UnPrepareRawRoi();
		}
	}

	private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
	{
		imageView.UnPrepareRawRoi();
	}

	private void OK_Click(object sender, RoutedEventArgs e)
	{
		if (Panel_EdgepatMatch.CreateModel(OwnerModuleParam, ref edgepatMatchData, popError: true))
		{
			DialogResult = true;
			Close();
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

	private void Button_Click(object sender, RoutedEventArgs e)
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

	private void Button_Click_1(object sender, RoutedEventArgs e)
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

	private void Match_Click(object sender, RoutedEventArgs e)
	{
		hikPath.IsChecked = false;
		if (Panel_EdgepatMatch.CreateModel(OwnerModuleParam, ref edgepatMatchData, popError: true))
		{
		}
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if ((edgepatMatchData.ModelData == null || edgepatMatchData.PointsData == null || edgepatMatchData.PointsData.Count == 0) && !Panel_EdgepatMatch.CreateModel(OwnerModuleParam, ref edgepatMatchData, popError: true))
		{
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
		saveFileDialog.Filter = " xml files(*.emxml)|*.emxml";
		saveFileDialog.RestoreDirectory = true;
		string text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		if (text != null)
		{
			saveFileDialog.InitialDirectory = text;
		}
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			edgepatMatchData.ParamTab = ParentDataSource as Tab;
			edgepatMatchData.CurImageData = CurImage;
			edgepatMatchData.FixInitBuffer = ((IUserBytesData)OwnerModuleParam)["FixInit"];
			edgepatMatchData.PrepareSDKImportData();
			byte[] bytes = ((BaseNode)(object)edgepatMatchData).GetBytes();
			EncryptionAndDecryptionHelper.WriteLibFile(saveFileDialog.FileName, bytes);
		}
		catch (Exception ex)
		{
			string strMsg = ex.Message;
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
		}
		HikMessageBox.Show("Lang_SaveSuccess", MessageIcons.Ok, isTranslate: true);
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), OwnerModule?.ModuleDir);
	}

	private void clear_Click(object sender, RoutedEventArgs e)
	{
		if (edgepatMatchData.ItemsData == null)
		{
			edgepatMatchData.ItemsData = new ObservableCollection<object>();
		}
		if (edgepatMatchData.ItemsData.Count != 0 || maskImage.Source != null)
		{
			DoClear();
			RecordHistory();
			dataGrid.DataContext = null;
		}
	}

	private void DoClear()
	{
		edgepatMatchData.ItemsData.Clear();
		edgepatMatchData.PointsData.Clear();
		edgepatMatchData.ShapesData.Clear();
		ClearMaskData();
	}

	private void BackStep_Click(object sender, RoutedEventArgs e)
	{
		if (history.Count > historyIndex && historyIndex > 0)
		{
			historyIndex--;
			string path = history[historyIndex];
			ChangeHistory(path);
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
		}
		OnPropertyChanged("BackEnable");
		OnPropertyChanged("ForwardEnable");
	}

	private void ChangeHistory(string path)
	{
		EdgepatMatchData edgepatMatchData = new EdgepatMatchData();
		if (edgepatMatchData != null)
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
			((BaseNode)(object)edgepatMatchData).loadXml(path, false);
			this.edgepatMatchData.ItemsData = edgepatMatchData.ItemsData;
			this.edgepatMatchData.otherMask = edgepatMatchData.otherMask;
			this.edgepatMatchData.PointsData = edgepatMatchData.PointsData;
			this.edgepatMatchData.ShapesData = edgepatMatchData.ShapesData;
			FreshOtherMask();
			HikImageView hikImageView2 = imageView;
			hikImageView2.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView2.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		}
	}

	private void RecordHistory()
	{
		try
		{
			EdgepatMatchData edgepatMatchData = new EdgepatMatchData
			{
				ItemsData = this.edgepatMatchData.ItemsData,
				otherMask = this.edgepatMatchData.otherMask
			};
			string text = Path.GetFullPath("./Temporary") + "\\EdgepatMatch_" + (historyIndex + 1) + ".xml";
			((BaseNode)(object)edgepatMatchData).Save(text);
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

	private void Import_Click(object sender, RoutedEventArgs e)
	{
		if (CurImage == null)
		{
			return;
		}
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Filter = "文件|*.xml";
		try
		{
			if (openFileDialog.ShowDialog() == true)
			{
				HikPolylineRoiParameter hikPolylineRoiParameter = new HikPolylineRoiParameter();
				hikPolylineRoiParameter.loadXml(openFileDialog.FileName);
				imageView.RoiList.Add(hikPolylineRoiParameter);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error, isTranslate: true);
		}
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		if (!(dataGrid.DataContext is HikPolylineRoiParameter hikPolylineRoiParameter))
		{
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
		saveFileDialog.Filter = "文件|*.xml";
		try
		{
			if (saveFileDialog.ShowDialog() == true)
			{
				string fileName = saveFileDialog.FileName.Trim();
				hikPolylineRoiParameter.Save(fileName);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error, isTranslate: true);
		}
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

	private void DeleteMaskArea(byte[] otherMask)
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

	private void maskImage_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Delete)
		{
			byte[] otherMask = edgepatMatchData.otherMask;
			if (otherMask != null)
			{
				DeleteMaskArea(otherMask);
				maskImage.Tag = null;
				RecordHistory();
			}
		}
	}

	private void ClearMaskData()
	{
		maskImage.Source = null;
		MaskSourceConvertor.Clear();
		edgepatMatchData.otherMask = null;
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
		if (imageView.Source == null || edgepatMatchData.otherMask == null)
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
		maskImage.Source = writeableBitmap;
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
		fixed (byte* otherMask = edgepatMatchData.otherMask)
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
						if (otherMask[num8] != 1)
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

	private void FreshOtherMask()
	{
		if (edgepatMatchData != null)
		{
			if (edgepatMatchData.otherMask != null)
			{
				DrawMask();
			}
			else
			{
				ClearMaskData();
			}
		}
	}

	private void maskImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void maskImage_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void maskImage_MouseMove(object sender, MouseEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void maskImage_MouseEnter(object sender, MouseEventArgs e)
	{
		imageView.SourceImage.RaiseEvent(e);
	}

	private void InitMaskImage()
	{
		maskImage.MouseLeftButtonUp += maskImage_PreviewMouseLeftButtonUp;
		maskImage.PreviewKeyUp += maskImage_PreviewKeyUp;
		maskImage.MouseEnter += maskImage_MouseEnter;
		maskImage.MouseMove += maskImage_MouseMove;
		maskImage.MouseWheel += maskImage_MouseWheel;
		maskImage.IsHitTestVisible = false;
		maskImage.Focusable = true;
		maskImage.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
		FreshOtherMask();
	}

	private void rubber_OnRubberFinish(object sender, object Data)
	{
		RecordHistory();
	}

	private void rubber_OnWipe(int[] indexs, List<System.Windows.Point> bagPoints)
	{
		if (edgepatMatchData == null)
		{
			return;
		}
		byte[] bytes = BitConverter.GetBytes(1291786778u);
		if (edgepatMatchData.otherMask == null)
		{
			edgepatMatchData.otherMask = new byte[CurImage.Width * CurImage.Height];
			for (int i = 0; i < edgepatMatchData.otherMask.Length; i++)
			{
				edgepatMatchData.otherMask[i] = 1;
			}
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (int num5 in indexs)
		{
			edgepatMatchData.otherMask[num5] = 0;
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
	}

	private void ckbMask_Checked(object sender, RoutedEventArgs e)
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
		maskImage.IsHitTestVisible = true;
	}

	private void ckbMask_Unchecked(object sender, RoutedEventArgs e)
	{
		if (rubber != null)
		{
			rubber.UnRegisterRoi(imageView);
		}
		maskImage.IsHitTestVisible = false;
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
