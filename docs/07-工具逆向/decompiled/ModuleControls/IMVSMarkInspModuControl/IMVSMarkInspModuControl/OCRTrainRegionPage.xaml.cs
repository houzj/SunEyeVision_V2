using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VMControls.BaseInterface;

namespace IMVSMarkInspModuControl;

public partial class OCRTrainRegionPage : UserControl, IComponentConnector
{
	private bool _selectbox = false;

	private bool _isRecoveryRoi = false;

	private HikCoverRoiParameter _curRoiParam = null;

	public static readonly DependencyProperty IsDrawRectProperty = DependencyProperty.Register("IsDrawRect", typeof(bool), typeof(OCRTrainRegionPage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectEnableProperty = DependencyProperty.Register("RectEnable", typeof(bool), typeof(OCRTrainRegionPage), new PropertyMetadata(false));

	public static readonly DependencyProperty RectRoisProperty = DependencyProperty.Register("RectRois", typeof(ObservableCollection<object>), typeof(OCRTrainRegionPage), new PropertyMetadata(null));

	public static readonly DependencyProperty SelectedBoxProperty = DependencyProperty.Register("SelectedBox", typeof(HikCoverRoiParameter), typeof(OCRTrainRegionPage), new PropertyMetadata((object)null));

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

	public OCRTrainRegionPage()
	{
		InitializeComponent();
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
		if (RectRois.Count > 1)
		{
			RectRois.RemoveAt(0);
		}
	}

	private void imageView_ROIClick(object sender, MouseEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement)
		{
			SelectBox(frameworkElement.DataContext as HikCoverRoiParameter);
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
		if (imageView.ImageDataSource == null)
		{
			return;
		}
		ModuleCommonMethodMgr.ImageTool.Clear();
		ImageSource imageSource = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(imageView.ImageDataSource.ImageBuffer, imageView.ImageDataSource.Width, imageView.ImageDataSource.Height, imageView.ImageDataSource.PixelFormat);
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

	private void RectROI_Click(object sender, RoutedEventArgs e)
	{
		IsDrawRect = true;
		imageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
	}

	private void imageView_DrawRoi_MouseUpChange(object sender, MouseEventArgs e)
	{
		imageView.UnPrepareRawRoi();
		imageView.ContextMenuVisible = false;
		IsDrawRect = false;
		RectEnable = true;
	}

	private void FullScreen_Click(object sender, RoutedEventArgs e)
	{
		if (OCRTrainDataMgr.Instance.IsStartTrain)
		{
			if (RectRois.Count == 0)
			{
				return;
			}
			imageView.UnPrepareRawRoi();
			IsDrawRect = false;
			DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ROIChangeWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult == DialogResult.Yes || dialogResult == DialogResult.SDKModel)
			{
				_curRoiParam = null;
				RectEnable = false;
				RectRois.Clear();
				SelectedBox = null;
				OCRTrainDataMgr.Instance.Owner?.ClearDataByRoi();
			}
		}
		else
		{
			imageView.UnPrepareRawRoi();
			IsDrawRect = false;
			RectEnable = false;
			if (RectRois.Count > 0)
			{
				RectRois.Clear();
				_curRoiParam = null;
				SelectedBox = null;
			}
		}
		imageView.ContextMenuVisible = false;
	}

	private void imageView_OnRoiParamChange(object data)
	{
		if (OCRTrainDataMgr.Instance.IsStartTrain && !_isRecoveryRoi)
		{
			DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_ROIChangeWarning", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult == DialogResult.Yes || dialogResult == DialogResult.SDKModel)
			{
				OCRTrainDataMgr.Instance.Owner?.ClearDataByRoi();
				return;
			}
			if (RectRois.Count > 0)
			{
				RectRois.Clear();
			}
			_isRecoveryRoi = true;
			if (_curRoiParam != null)
			{
				RectRois.Add(new HikCoverRoiParameter
				{
					Width = _curRoiParam.Width,
					Height = _curRoiParam.Height,
					Angle = _curRoiParam.Angle,
					CenterX = _curRoiParam.CenterX,
					CenterY = _curRoiParam.CenterY,
					ForceShowSelected = true
				});
			}
			else
			{
				RectEnable = false;
				_isRecoveryRoi = false;
			}
			SelectedBox = null;
			_selectbox = false;
		}
		else
		{
			_isRecoveryRoi = false;
		}
	}

	private void StartValueChange(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		imageView_OnRoiParamChange(sender);
	}

	public bool SetRoiData()
	{
		if (OCRTrainDataMgr.Instance.Param == null)
		{
			return false;
		}
		try
		{
			((IUserBytesData)OCRTrainDataMgr.Instance.Param)["SetTrainRoiData"] = GetRoiBuffer();
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			string text2 = AppLocalizationService.GetLang("Lang_SetROIError") + "\r\n(";
			text2 += AppLocalizationService.GetLang("errInfo:");
			text2 += text;
			text2 += ")";
			HikMessageBox.Show(text2, MessageIcons.Error);
			return false;
		}
		return true;
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		RectRois = new ObservableCollection<object>();
		imageView.ROITypeUI = ROITypeUIEnum.Transparent;
		HikImageView hikImageView = imageView;
		hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Combine(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
		HikImageView hikImageView2 = imageView;
		hikImageView2.OnDrawRoi_MouseUpChange = (HikImageView.DrawRoi_MouseUpChange)Delegate.Combine(hikImageView2.OnDrawRoi_MouseUpChange, new HikImageView.DrawRoi_MouseUpChange(imageView_DrawRoi_MouseUpChange));
		HikImageView hikImageView3 = imageView;
		hikImageView3.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView3.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
		_curRoiParam = moduleTrainData.TrainRoiParam;
		if (_curRoiParam != null)
		{
			_isRecoveryRoi = true;
			RectRois.Add(new HikCoverRoiParameter
			{
				Width = _curRoiParam.Width,
				Height = _curRoiParam.Height,
				Angle = _curRoiParam.Angle,
				CenterX = _curRoiParam.CenterX,
				CenterY = _curRoiParam.CenterY,
				ForceShowSelected = true
			});
			RectEnable = true;
		}
	}

	public void DeInit(ModuleTrainData moduleTrainData)
	{
		HikCoverRoiParameter trainRoiParam = null;
		if (RectRois.Count > 0)
		{
			trainRoiParam = RectRois[0] as HikCoverRoiParameter;
		}
		moduleTrainData.TrainRoiParam = trainRoiParam;
		if (imageView != null)
		{
			HikImageView hikImageView = imageView;
			hikImageView.OnPixelChange = (HikImageView.PixelChange)Delegate.Remove(hikImageView.OnPixelChange, new HikImageView.PixelChange(imageView_OnPixelChange));
			HikImageView hikImageView2 = imageView;
			hikImageView2.OnDrawRoi_MouseUpChange = (HikImageView.DrawRoi_MouseUpChange)Delegate.Remove(hikImageView2.OnDrawRoi_MouseUpChange, new HikImageView.DrawRoi_MouseUpChange(imageView_DrawRoi_MouseUpChange));
			HikImageView hikImageView3 = imageView;
			hikImageView3.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(hikImageView3.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
			imageView.Dispose();
			imageView.DataContext = null;
		}
		DataContext = null;
		GC.Collect();
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

	private byte[] GetRoiBuffer()
	{
		if (RectRois.Count > 0)
		{
			HikCoverRoiParameter hikCoverRoiParameter = new HikCoverRoiParameter();
			HikCoverRoiParameter hikCoverRoiParameter2 = RectRois[0] as HikCoverRoiParameter;
			hikCoverRoiParameter.CenterX = hikCoverRoiParameter2.CenterX;
			hikCoverRoiParameter.CenterY = hikCoverRoiParameter2.CenterY;
			hikCoverRoiParameter.Width = hikCoverRoiParameter2.Width;
			hikCoverRoiParameter.Height = hikCoverRoiParameter2.Height;
			hikCoverRoiParameter.Angle = hikCoverRoiParameter2.Angle;
			_curRoiParam = hikCoverRoiParameter;
		}
		else
		{
			_curRoiParam = null;
		}
		if (imageView.ImageDataSource != null)
		{
			return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(_curRoiParam, (float)imageView.ImageDataSource.Width, (float)imageView.ImageDataSource.Height);
		}
		return ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(_curRoiParam, 0f, 0f);
	}
}
