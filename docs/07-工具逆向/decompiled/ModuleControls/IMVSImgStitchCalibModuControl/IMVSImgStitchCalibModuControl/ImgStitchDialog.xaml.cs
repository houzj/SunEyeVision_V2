using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.PcUI.Controls;
using Apps.XmlParser.ParamTab;
using HikExternalCall.ImageProcess;
using HikPcUI.ImageView;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;

namespace IMVSImgStitchCalibModuControl;

public partial class ImgStitchDialog : UserControl, IComponentConnector
{
	private static int ENLeftTextWidth = 170;

	private static int ENBtnReplaceWidth = 200;

	private static int ENBtnFeatureWidth = 110;

	private const int ImageUniformWidth = 750;

	private const int ImageUniformHeight = 540;

	public Window Owner;

	public ImgStitchPannel OwnerModulePanel;

	private int imageNum = 0;

	private int scale = 100;

	private const int IMAGE_PROPERTY_COUNT = 4;

	private const int IMAGE_MIN_SIZE = 64;

	private IVmProcedure ownerProcedure;

	public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(int), typeof(ImgStitchDialog), new PropertyMetadata(100));

	public static readonly DependencyProperty LeftTextWidthProperty = DependencyProperty.Register("LeftTextWidth", typeof(int), typeof(ImgStitchDialog), new PropertyMetadata(89));

	public static readonly DependencyProperty IsShowLargeImageProperty = DependencyProperty.Register("IsShowLargeImage", typeof(bool), typeof(ImgStitchDialog), new PropertyMetadata(false));

	public static readonly DependencyProperty IsActualImageProperty = DependencyProperty.Register("IsActualImage", typeof(bool), typeof(ImgStitchDialog), new PropertyMetadata(false));

	private ProgressMask progressMask;

	internal IVmProcedure OwnerProcedure
	{
		get
		{
			if (ownerProcedure == null)
			{
				while (ownerProcedure == null)
				{
					IVmModuleSet vmModuleSet = OwnerModulePanel.OwnerModule?.Root;
					if (vmModuleSet == null)
					{
						break;
					}
					if (vmModuleSet is IVmProcedure)
					{
						ownerProcedure = vmModuleSet as IVmProcedure;
					}
				}
			}
			return ownerProcedure;
		}
	}

	public int Zoom
	{
		get
		{
			return (int)GetValue(ZoomProperty);
		}
		set
		{
			SetValue(ZoomProperty, value);
		}
	}

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

	public bool IsShowLargeImage
	{
		get
		{
			return (bool)GetValue(IsShowLargeImageProperty);
		}
		set
		{
			SetValue(IsShowLargeImageProperty, value);
		}
	}

	public bool IsActualImage
	{
		get
		{
			return (bool)GetValue(IsActualImageProperty);
		}
		set
		{
			SetValue(IsActualImageProperty, value);
		}
	}

	public ImgStitchDialog(ImgStitchPannel ownerModulePanel = null)
	{
		InitializeComponent();
		OwnerModulePanel = ownerModulePanel ?? new ImgStitchPannel();
		if (AppLocalizationService.GetLang("Language") == "English")
		{
			LeftTextWidth = ENLeftTextWidth;
			BtnReplace.Width = ENBtnReplaceWidth;
			BtnFeature.Width = ENBtnFeatureWidth;
		}
	}

	private void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			LargeImage.Owner = this;
			if ((OwnerModulePanel?.ParentNode as NameValueItem)?.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				if (!OwnerModulePanel.imageStitchData.HasModel && algorithmTabRoot.FindBaseNodeByName("ImageMatrix") is NumberMultiply numberMultiply)
				{
					OwnerModulePanel.imageStitchData.RowsA = numberMultiply.LeftValueA;
					OwnerModulePanel.imageStitchData.ColumnsA = numberMultiply.RightValueA;
				}
				SetImageUniform();
			}
			if (OwnerModulePanel?.OwnerModule == null)
			{
				BtnCurImg.Visibility = Visibility.Collapsed;
				BtnRun.Visibility = Visibility.Collapsed;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
	}

	private void SetImageUniform()
	{
		if (OwnerModulePanel.imageStitchData.RowsA > OwnerModulePanel.imageStitchData.ColumnsA)
		{
			double actualWidth = UniformImage.ActualWidth;
			UniformImage.Width = actualWidth * ((double)OwnerModulePanel.imageStitchData.ColumnsA / (double)OwnerModulePanel.imageStitchData.RowsA);
			UniformImage.Height = UniformImage.ActualHeight;
			UniformImage.HorizontalAlignment = HorizontalAlignment.Left;
			UniformImage.VerticalAlignment = VerticalAlignment.Top;
		}
		else
		{
			double actualHeight = UniformImage.ActualHeight;
			UniformImage.Height = actualHeight * ((double)OwnerModulePanel.imageStitchData.RowsA / (double)OwnerModulePanel.imageStitchData.ColumnsA);
			UniformImage.Width = UniformImage.ActualWidth;
			UniformImage.HorizontalAlignment = HorizontalAlignment.Left;
			UniformImage.VerticalAlignment = VerticalAlignment.Top;
		}
		int displayImageWidth = 750 / OwnerModulePanel.imageStitchData.PanelRowsAndColumns;
		int displayImageHeight = 540 / OwnerModulePanel.imageStitchData.PanelRowsAndColumns;
		for (int i = 0; i < OwnerModulePanel.imageStitchData.ImageNumber; i++)
		{
			if (OwnerModulePanel.imageStitchData.ItemsData.Count <= i)
			{
				OwnerModulePanel.imageStitchData.ItemsData.Add(new StitchImageDataItem());
			}
			StitchImageView stitchImageView = new StitchImageView(this)
			{
				Index = i + 1,
				DataContext = OwnerModulePanel.imageStitchData.ItemsData[i],
				DisplayImageWidth = displayImageWidth,
				DisplayImageHeight = displayImageHeight
			};
			Binding binding = new Binding("IsActualImage")
			{
				Source = this
			};
			stitchImageView.SetBinding(StitchImageView.IsActualImageProperty, binding);
			Binding binding2 = new Binding("ImageDataA");
			stitchImageView.SetBinding(StitchImageView.CalibImageProperty, binding2);
			Binding binding3 = new Binding("ActualImageDataA");
			stitchImageView.SetBinding(StitchImageView.ActualImageProperty, binding3);
			stitchImageView.ShowImage(ignorePop: true);
			UniformImage.Children.Add(stitchImageView);
		}
		if (OwnerModulePanel.imageStitchData.PanelRowsAndColumns <= 2)
		{
			BtnZoomOut.IsEnabled = false;
		}
		BtnZoomIn.IsEnabled = false;
		OwnerModulePanel.imageStitchData.OnPropertyChanged("SuccessRate");
	}

	private void CurImg_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModulePanel.imageStitchData.IsGetingImage)
		{
			if (OwnerModulePanel?.OwnerModule?.FrontModules != null)
			{
				foreach (object frontModule in OwnerModulePanel.OwnerModule.FrontModules)
				{
					if (frontModule is IVmModule vmModule)
					{
						vmModule.SubscribeCount--;
					}
				}
			}
			OwnerModulePanel.imageStitchData.IsGetingImage = false;
			BtnRun.IsEnabled = false;
			if (OwnerProcedure is IVmModule vmModule2)
			{
				vmModule2.ModuleResultCallBackArrived -= InputImageChanged;
			}
			return;
		}
		if (OwnerModulePanel.imageStitchData.IsStitchImageDataNotNull)
		{
			if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmClearCurImages"), MessageButtons.OkOrCancel, MessageIcons.Question) != DialogResult.Ok)
			{
				return;
			}
			ClearImage(DeleteOperationType.OnlyStitchImage);
		}
		if (OwnerProcedure is IVmModule vmModule3)
		{
			vmModule3.ModuleResultCallBackArrived -= InputImageChanged;
			vmModule3.ModuleResultCallBackArrived += InputImageChanged;
		}
		if (OwnerModulePanel?.OwnerModule?.FrontModules != null)
		{
			foreach (object frontModule2 in OwnerModulePanel.OwnerModule.FrontModules)
			{
				if (frontModule2 is IVmModule vmModule4)
				{
					vmModule4.SubscribeCount++;
				}
			}
		}
		imageNum = 0;
		OwnerModulePanel.imageStitchData.IsGetingImage = true;
		BtnRun.IsEnabled = true;
	}

	public void InputImageChanged(object sender, EventArgs e)
	{
		if (!OwnerModulePanel.imageStitchData.IsGetingImage || !(e is ValueEventArgs { Value: var value }) || !(value is ImvsSdkDefine.IMVS_MODULE_WORK_STAUS { nWorkStatus: 0u } iMVS_MODULE_WORK_STAUS))
		{
			return;
		}
		IList vmIOByType = OwnerModulePanel.OwnerModule.GetVmIOByType("Inputs", "IMAGE");
		foreach (IVmFrameBase item in vmIOByType)
		{
			if (!(item is IVmIO iVmIO))
			{
				continue;
			}
			ImageData image = iVmIO.GetCombinationValueByType(iMVS_MODULE_WORK_STAUS.nExecuteCount) as ImageData;
			if (image != null && image.ImageBuffer == null)
			{
				image.ImageBuffer = Byte2ImageSource.GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, image.PixelFormat);
				image = ((image.ImageBuffer == null) ? null : image);
			}
			if (image == null)
			{
				continue;
			}
			if (image.Width < 64 || image.Height < 64)
			{
				if (OwnerModulePanel.imageStitchData.IsGetingImage && OwnerModulePanel?.OwnerModule?.FrontModules != null)
				{
					foreach (object frontModule in OwnerModulePanel.OwnerModule.FrontModules)
					{
						if (frontModule is IVmModule vmModule)
						{
							vmModule.SubscribeCount--;
						}
					}
				}
				OwnerModulePanel.imageStitchData.IsGetingImage = false;
				if (OwnerProcedure is IVmModule vmModule2)
				{
					vmModule2.ModuleResultCallBackArrived -= InputImageChanged;
				}
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SmallSizeImage"), MessageIcons.Error);
				});
				break;
			}
			UniformImage.Dispatcher.Invoke(() =>
			{
				(OwnerModulePanel.imageStitchData.ItemsData[imageNum] as StitchImageDataItem).ImageDataA = image;
				(UniformImage.Children[imageNum] as StitchImageView)?.ShowImage();
			});
			OwnerModulePanel.imageStitchData.SelectedItemA = OwnerModulePanel.imageStitchData.ItemsData[imageNum] as StitchImageDataItem;
			imageNum++;
			if (imageNum == OwnerModulePanel.imageStitchData.ImageNumber)
			{
				if (OwnerModulePanel.imageStitchData.IsGetingImage && OwnerModulePanel?.OwnerModule?.FrontModules != null)
				{
					foreach (object frontModule2 in OwnerModulePanel.OwnerModule.FrontModules)
					{
						if (frontModule2 is IVmModule vmModule3)
						{
							vmModule3.SubscribeCount--;
						}
					}
				}
				OwnerModulePanel.imageStitchData.IsGetingImage = false;
				if (OwnerProcedure is IVmModule vmModule4)
				{
					vmModule4.ModuleResultCallBackArrived -= InputImageChanged;
				}
				break;
			}
			Dispatcher.Invoke(() =>
			{
				BtnRun.IsEnabled = true;
			});
		}
	}

	private void RemoveMask()
	{
		Dispatcher.Invoke(() =>
		{
			mainGrid.Children.Remove(progressMask);
			progressMask = null;
		});
	}

	private void AddMask(string tips)
	{
		Dispatcher.Invoke(() =>
		{
			progressMask = new ProgressMask();
			progressMask.Message = tips;
			progressMask.IsShowProgressBar = false;
			progressMask.SetValue(Grid.RowSpanProperty, 2);
			mainGrid.Children.Add(progressMask);
		});
	}

	private void OtherImg_Click(object sender, RoutedEventArgs e)
	{
		if ((!IsActualImage && OwnerModulePanel.imageStitchData.IsStitchImageDataNotNull) || (IsActualImage && OwnerModulePanel.imageStitchData.IsFixtureImageDataNotNull))
		{
			if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmClearOtherImages"), MessageButtons.OkOrCancel, MessageIcons.Question) != DialogResult.Ok)
			{
				return;
			}
			ClearImage((!IsActualImage) ? DeleteOperationType.OnlyStitchImage : DeleteOperationType.OnlyFixtureImage);
		}
		string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModulePanel.OwnerModule?.ModuleDir);
		string[] mono8ImagePathByFileDialog = ImageProcessManager.GetMono8ImagePathByFileDialog(ref path);
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), OwnerModulePanel.OwnerModule?.ModuleDir);
		OwnerModulePanel.imageStitchData.IsConfigUIEnabled = false;
		LoadImageAsync(mono8ImagePathByFileDialog, IsActualImage);
	}

	private async void LoadImageAsync(string[] filenames, bool isActual)
	{
		await Task.Run(() =>
		{
			StringBuilder sb = null;
			try
			{
				AddMask("正在加载图片");
				if (filenames != null)
				{
					for (int i = 0; i < OwnerModulePanel.imageStitchData.ItemsData.Count; i++)
					{
						if (i == filenames.Length)
						{
							break;
						}
						ImageData tempImage = ImageData.GetImageDataFromFile(filenames[i]);
						if (tempImage.ImageBuffer == null)
						{
							Dispatcher.Invoke(() =>
							{
								HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImagePathError"), MessageIcons.Error);
							});
							return;
						}
						if (tempImage.Width < 64 || tempImage.Height < 64)
						{
							if (sb == null)
							{
								sb = new StringBuilder();
							}
							int num = i + 1;
							sb.Append(num.ToString()).Append(",");
						}
						Dispatcher.Invoke(() =>
						{
							if (isActual)
							{
								(OwnerModulePanel.imageStitchData.ItemsData[i] as StitchImageDataItem).ActualImageDataA = tempImage;
							}
							else
							{
								(OwnerModulePanel.imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA = tempImage;
							}
							(UniformImage.Children[i] as StitchImageView).ShowImage();
						});
					}
					if (OwnerModulePanel.imageStitchData.ItemsData.Count > 0)
					{
						OwnerModulePanel.imageStitchData.SelectedItemA = OwnerModulePanel.imageStitchData.ItemsData[0] as StitchImageDataItem;
					}
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang(ex3.Message), MessageIcons.Error);
				});
			}
			finally
			{
				Dispatcher.Invoke(() =>
				{
					OwnerModulePanel.imageStitchData.IsConfigUIEnabled = true;
					RemoveMask();
					if (sb != null && sb.Length > 0)
					{
						sb.Remove(sb.Length - 1, 1);
						string strMsg = AppLocalizationService.GetLang("ImageData") + " :" + sb.ToString() + AppLocalizationService.GetLang("Lang_TooSmall") + AppLocalizationService.GetLang("Lang_SmallSizeImage");
						HikMessageBox.Show(strMsg, MessageIcons.Error);
					}
				});
			}
		});
	}

	private void BtnCreate_Click(object sender, RoutedEventArgs e)
	{
		switch (OwnerModulePanel.imageStitchData.CheckImages())
		{
		case -1:
			HikMessageBox.Show(AppLocalizationService.GetLang("图像不足"), MessageIcons.Error, isTranslate: true);
			return;
		case -3:
			HikMessageBox.Show(AppLocalizationService.GetLang("图像状态异常"), MessageIcons.Error, isTranslate: true);
			return;
		}
		if (OwnerModulePanel.imageStitchData.CreateModel(OwnerModulePanel.OwnerModuleParam))
		{
			OwnerModulePanel.SetModelData();
			HikMessageBox.Show(AppLocalizationService.GetLang("创建模型成功"), MessageIcons.Ok, isTranslate: true);
		}
	}

	private void BtnPreview_Click(object sender, RoutedEventArgs e)
	{
		switch (OwnerModulePanel.imageStitchData.CheckImages())
		{
		case -1:
			HikMessageBox.Show(AppLocalizationService.GetLang("图像不足"), MessageIcons.Error, isTranslate: true);
			break;
		case -3:
			HikMessageBox.Show(AppLocalizationService.GetLang("图像状态异常"), MessageIcons.Error, isTranslate: true);
			break;
		default:
			PreviewStitchImageAsync();
			break;
		}
	}

	private async Task PreviewStitchImageAsync()
	{
		OwnerModulePanel.imageStitchData.IsConfigUIEnabled = false;
		AddMask("正在加载图片");
		string errorMsg = "";
		bool result = await Task.Run(() => OwnerModulePanel.imageStitchData.PreView(OwnerModulePanel.OwnerModuleParam, out errorMsg));
		RemoveMask();
		if (result)
		{
			try
			{
				BaseWindow window = new BaseWindow
				{
					ShowInTaskbar = false
				};
				window.Height = 450.0;
				window.Width = 600.0;
				window.Title = AppLocalizationService.GetLang("拼接预览");
				window.FontSize = 14.0;
				window.FontWeight = FontWeights.Normal;
				window.MinButtonVisibility = Visibility.Collapsed;
				ImageData image = OwnerModulePanel.imageStitchData.PreviewImageA;
				StitchImageView view = new StitchImageView
				{
					DisplayImageWidth = image.Width,
					DisplayImageHeight = image.Height
				};
				view.ImageDataSource = image;
				view.IsOnlyShowImage = true;
				window.Content = view;
				window.ShowDialog();
				window.Content = null;
				view.Dispose();
				OwnerModulePanel.imageStitchData.PreviewImageA = null;
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				string msg = ex2.Message;
				if (ex2 is OutOfMemoryException)
				{
					msg = AppLocalizationService.GetLang("程序可用内存不足");
				}
				HikMessageBox.Show(msg, MessageIcons.Error, isTranslate: true);
			}
		}
		else
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("预览失败"), errorMsg, MessageIcons.Error, isTranslate: true);
		}
		OwnerModulePanel.imageStitchData.IsConfigUIEnabled = true;
	}

	private void BtnFeature_Click(object sender, RoutedEventArgs e)
	{
		switch (OwnerModulePanel.imageStitchData.CheckImages())
		{
		case -1:
			HikMessageBox.Show(AppLocalizationService.GetLang("图像不足"), MessageIcons.Error, isTranslate: true);
			break;
		case -2:
			HikMessageBox.Show(AppLocalizationService.GetLang("图像格式不统一"), MessageIcons.Error, isTranslate: true);
			break;
		default:
			OwnerModulePanel.imageStitchData.ExtractFeatureAsync(OwnerModulePanel.OwnerModuleParam);
			break;
		}
	}

	public void ChangeLargeImageStatus(int index)
	{
		if (IsShowLargeImage)
		{
			LargeImage.Index = 0;
			LargeImage.DataContext = null;
			LargeImage.DeleteImage();
			UniformImage.Visibility = Visibility.Visible;
			LargeImage.Visibility = Visibility.Collapsed;
			ZoomUniform.Visibility = Visibility.Visible;
			IsShowLargeImage = false;
			(UniformImage.Children[index - 1] as StitchImageView)?.ShowImage(ignorePop: true);
		}
		else
		{
			LargeImage.Index = index;
			LargeImage.DataContext = OwnerModulePanel.imageStitchData.SelectedItemA;
			UniformImage.Visibility = Visibility.Collapsed;
			LargeImage.Visibility = Visibility.Visible;
			ZoomUniform.Visibility = Visibility.Collapsed;
			LargeImage.ShowImage(ignorePop: true);
			LargeImage.DrawPoints(OwnerModulePanel.imageStitchData.SelectedItemA);
			IsShowLargeImage = true;
		}
	}

	private void BtnImageClear_Click(object sender, RoutedEventArgs e)
	{
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.OkOrCancel, MessageIcons.Question) == DialogResult.Ok)
		{
			ClearImage((!IsActualImage) ? DeleteOperationType.OnlyStitchImage : DeleteOperationType.OnlyFixtureImage);
		}
	}

	private void ClearImage(DeleteOperationType type = DeleteOperationType.All)
	{
		OwnerModulePanel.imageStitchData.ClearData(OwnerModulePanel.OwnerModuleParam, isItemsClear: false, null, type);
		foreach (StitchImageView child in UniformImage.Children)
		{
			child.DeleteImage();
		}
		LargeImage.DeleteImage();
	}

	private void tbnEnable_Unchecked(object sender, RoutedEventArgs e)
	{
		rbnStitch.IsChecked = true;
	}

	private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
	{
		Zoom += 10;
		ChangeSize();
	}

	private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
	{
		Zoom -= 10;
		ChangeSize();
	}

	private void Btn11_Click(object sender, RoutedEventArgs e)
	{
		Zoom = 100;
		ChangeSize();
	}

	private void ChangeSize()
	{
		double actualWidth = UniformImage.ActualWidth;
		double actualHeight = UniformImage.ActualHeight;
		double num = (double)Zoom / (double)scale;
		actualWidth *= num;
		actualHeight *= num;
		UniformImage.Width = actualWidth;
		UniformImage.Height = actualHeight;
		foreach (FrameworkElement child in UniformImage.Children)
		{
			child.Width *= num;
			child.Height *= num;
		}
		scale = Zoom;
		if (scale >= OwnerModulePanel.imageStitchData.PanelRowsAndColumns * 100 / 2)
		{
			BtnZoomOut.IsEnabled = false;
		}
		else
		{
			BtnZoomOut.IsEnabled = true;
		}
		if (scale > 100)
		{
			BtnZoomIn.IsEnabled = true;
		}
		else
		{
			BtnZoomIn.IsEnabled = false;
		}
	}

	private void BtnReplace_Click(object sender, RoutedEventArgs e)
	{
		OwnerModulePanel.imageStitchData.ReplaceAllImageParam();
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ReplaceCompleted"), MessageIcons.Ok, isTranslate: true);
	}

	private void BtnRun_Click(object sender, RoutedEventArgs e)
	{
		BtnRun.IsEnabled = false;
		if (OwnerProcedure is VmProcedure vmProcedure)
		{
			try
			{
				vmProcedure.Run(null, bIsWait: false);
			}
			catch (VmException ex)
			{
				string strMsg = vmProcedure.FullName + ":" + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
				HikMessageBox.Show(strMsg, MessageIcons.Error);
			}
		}
	}

	public void ImageStitchDialogUI_Closing(object sender, CancelEventArgs e)
	{
		if (OwnerProcedure is IVmModule vmModule)
		{
			vmModule.ModuleResultCallBackArrived -= InputImageChanged;
		}
		foreach (StitchImageView child in UniformImage.Children)
		{
			child.Dispose();
		}
		LargeImage.image = null;
	}

	private void togBtn_Checked(object sender, RoutedEventArgs e)
	{
		IsActualImage = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
