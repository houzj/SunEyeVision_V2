using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Apps.BaseData;
using Apps.ColorFun;
using Apps.Data;
using Apps.Data.Common;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.PcUI.Controls;
using Apps.XmlParser.ParamTab;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace IMVSImgStitchCalibModuControl;

public partial class ImgStitchPannel : UserControl, IComponentConnector
{
	private static int ENCustomWidth = 660;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(ImgStitchPannel), new PropertyMetadata(null));

	private IVmModule ownerModule = null;

	private IParamsConfig ownerModuleParam = null;

	public ImageStitchData imageStitchData = new ImageStitchData();

	public ImgStitchDialog ImgSthDlg = null;

	private ProgressMask progressMask;

	private Byte2ImageSource _byte2Image = new Byte2ImageSource();

	private const int PanelWidth_CN = 380;

	private const int PanelWidth_EN = 460;

	private const int PanelHeight = 320;

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public IVmModule OwnerModule
	{
		get
		{
			if (ownerModule == null)
			{
				try
				{
					if (ParentNode is NameValueItem nameValueItem && nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
					{
						ownerModule = algorithmTabRoot.Owner as IVmModule;
					}
				}
				catch
				{
				}
			}
			return ownerModule;
		}
	}

	public IParamsConfig OwnerModuleParam
	{
		get
		{
			if (ownerModuleParam == null)
			{
				ownerModuleParam = OwnerModule?.Params;
				if (ownerModuleParam == null && ParentNode is NameValueItem nameValueItem && nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
				{
					ownerModuleParam = algorithmTabRoot.UserStringData;
				}
			}
			return ownerModuleParam;
		}
	}

	public ImgStitchPannel()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected Obj, but got Unknown
		InitializeComponent();
		if (AppLocalizationService.GetLang("Language") == "English")
		{
			Width = ENCustomWidth;
		}
	}

	private void RemoveMask()
	{
		Dispatcher.Invoke(() =>
		{
			try
			{
				if (Window.GetWindow(this) is BaseWindow baseWindow)
				{
					baseWindow.IsEnabled = true;
				}
			}
			catch (Exception)
			{
			}
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
			try
			{
				if (Window.GetWindow(this) is BaseWindow baseWindow)
				{
					baseWindow.IsEnabled = false;
				}
			}
			catch (Exception)
			{
			}
		});
	}

	public void RefreshImageModel(bool isImport = false)
	{
		try
		{
			if (isImport)
			{
				SetModelData();
			}
			else
			{
				GetModelData();
			}
			UniformImageList.Children.Clear();
			if (!imageStitchData.HasModel)
			{
				return;
			}
			UniformImageList.Rows = imageStitchData.PanelRowsAndColumns;
			UniformImageList.Columns = imageStitchData.PanelRowsAndColumns;
			int num = 0;
			int num2 = 0;
			int num3 = imageStitchData.PanelRowsAndColumns * imageStitchData.PanelRowsAndColumns;
			int num4 = ((AppLocalizationService.GetLangType() == "zh-cn") ? 380 : 460);
			int num5 = 320;
			int borderWidth = num4 / imageStitchData.PanelRowsAndColumns;
			int borderHeight = num5 / imageStitchData.PanelRowsAndColumns;
			for (int i = 0; i < num3; i++)
			{
				if (num2 == imageStitchData.ItemsData.Count)
				{
					break;
				}
				if (num == imageStitchData.PanelRowsAndColumns)
				{
					num = 0;
				}
				num++;
				if (num <= imageStitchData.Columns)
				{
					ImageData imageDataA = (imageStitchData.ItemsData[num2] as StitchImageDataItem).ImageDataA;
					Size thumbnailImageSize = GetThumbnailImageSize(imageDataA, borderWidth, borderHeight);
					_byte2Image.Clear();
					bool flag = true;
					UniformImageList.Children.Add(new Border
					{
						Background = Brushes.LightGray,
						BorderBrush = Brushes.Green,
						BorderThickness = new Thickness(1.0),
						Margin = new Thickness(2.0),
						Child = new Image
						{
							Source = _byte2Image.BuiltImageSourceA((IImageData)imageDataA, (int)thumbnailImageSize.Width, (int)thumbnailImageSize.Height, ref flag, 0, 0, 0, 0, false)
						}
					});
					num2++;
				}
				else
				{
					UniformImageList.Children.Add(new Grid());
				}
			}
		}
		catch (Exception ex)
		{
			if (isImport)
			{
				throw ex;
			}
			int result = 0;
			string strMsg = (int.TryParse(ex.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			else if (ex is OutOfMemoryException || ex.InnerException is OutOfMemoryException)
			{
				strMsg = AppLocalizationService.GetLang("程序可用内存不足") + "," + AppLocalizationService.GetLang("模型加载失败");
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
		}
	}

	private Size GetThumbnailImageSize(ImageData image, int borderWidth, int borderHeight)
	{
		Size size = default;
		double num = (double)image.Width / (double)image.Height;
		double num2 = (double)borderWidth / (double)borderHeight;
		if (num > num2)
		{
			double height = (double)borderWidth / num;
			size = new Size(borderWidth, height);
		}
		else
		{
			double width = num * (double)borderHeight;
			size = new Size(width, borderHeight);
		}
		return size;
	}

	private void BtnNewModel_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error, isTranslate: true);
		}
		else if (!imageStitchData.HasModel || HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmClearModel"), MessageButtons.OkOrCancel, MessageIcons.Question) == DialogResult.Ok)
		{
			UniformImageList.Children.Clear();
			imageStitchData.ClearData(OwnerModuleParam, isItemsClear: true, OwnerModule);
			CreatModel.Instance.IsCreatingModel = true;
			ModelConfigDialog();
			CreatModel.Instance.IsCreatingModel = false;
		}
	}

	private void BtnEditModel_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error, isTranslate: true);
			return;
		}
		try
		{
			string[] array = ((IUserStringData)OwnerModuleParam)["ImageMatrix"].Split('X');
			if (array.Length < 2 || int.Parse(array[0]) != imageStitchData.RowsA || int.Parse(array[1]) != imageStitchData.ColumnsA)
			{
				throw new Exception("模型拼接方式不匹配");
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
			return;
		}
		EditModelAsync();
	}

	private async void EditModelAsync()
	{
		bool errorStatus = false;
		try
		{
			AddMask(AppLocalizationService.GetLang("正在加载模型"));
			errorStatus = await Task.Run(() => imageStitchData.SetImageParam(OwnerModuleParam) != 0 || imageStitchData.SetImageBuffer(OwnerModuleParam) != 0 || imageStitchData.SetFeatureCommand(OwnerModuleParam) != 0 || imageStitchData.GetFeatureData(OwnerModuleParam) != 0);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			int err = 0;
			string msg = (int.TryParse(ex2.Message, out err) ? ErrorCodeInfo.GetErrorInfo(err) : ex2.Message);
			VmException ex3;
			VmException vmException = (ex3 = ex2 as VmException);
			if (ex3 != null)
			{
				msg = ErrorCodeInfo.GetErrorInfo(vmException.errorCode);
			}
			HikMessageBox.Show(msg, MessageIcons.Error, isTranslate: true);
		}
		finally
		{
			RemoveMask();
			if (errorStatus && OwnerModule != null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ModelDataSetFail"), MessageIcons.Error, isTranslate: true);
			}
			else
			{
				CreatModel.Instance.IsCreatingModel = true;
				ModelConfigDialog();
				CreatModel.Instance.IsCreatingModel = false;
			}
		}
	}

	private void ModelConfigDialog()
	{
		BaseWindow baseWindow = new BaseWindow
		{
			ShowInTaskbar = false
		};
		baseWindow.Height = 680.0;
		baseWindow.Width = ((AppLocalizationService.GetLang("Language") == "English") ? 1215 : 1100);
		baseWindow.Title = AppLocalizationService.GetLang("Lang_ModelSet");
		baseWindow.FontSize = 14.0;
		baseWindow.FontWeight = FontWeights.Normal;
		baseWindow.MinButtonVisibility = Visibility.Collapsed;
		baseWindow.SetBinding(UIElement.IsEnabledProperty, new Binding
		{
			Source = imageStitchData,
			Path = new PropertyPath("IsConfigUIEnabled")
		});
		ColorThemesResourceDictionary.GetInstance(baseWindow);
		ImgSthDlg = new ImgStitchDialog(this);
		ImgSthDlg.Owner = baseWindow;
		ImgSthDlg.DataContext = imageStitchData;
		baseWindow.Content = ImgSthDlg;
		baseWindow.Closing += ImgSthDlg.ImageStitchDialogUI_Closing;
		try
		{
			((IUserStringData)OwnerModuleParam)["EditStatus"] = "1";
			baseWindow.ShowDialog();
			AddMask("正在更新模型图像");
			baseWindow.Closing -= ImgSthDlg.ImageStitchDialogUI_Closing;
			((IUserStringData)OwnerModuleParam)["EditStatus"] = "0";
			(ParentNode as IRunService)?.PreExecuteRunModel();
		}
		catch (Exception)
		{
		}
		finally
		{
			RefreshImageModel();
			ImgSthDlg = null;
			imageStitchData.IsGetingImage = false;
			RemoveMask();
		}
	}

	private void BtnClear_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error, isTranslate: true);
		}
		else if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.OkOrCancel, MessageIcons.Question) == DialogResult.Ok)
		{
			imageStitchData.ClearData(OwnerModuleParam, isItemsClear: true, OwnerModule);
			UniformImageList.Children.Clear();
		}
	}

	private void Import_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error, isTranslate: true);
		}
		else
		{
			if (imageStitchData.HasModel && HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmClearModel"), MessageButtons.OkOrCancel, MessageIcons.Question) != DialogResult.Ok)
			{
				return;
			}
			imageStitchData.ClearData(OwnerModuleParam, isItemsClear: true, OwnerModule);
			string text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
			openFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.xml";
			openFileDialog.FilterIndex = 2;
			openFileDialog.RestoreDirectory = false;
			if (text != null)
			{
				openFileDialog.InitialDirectory = text;
			}
			if (openFileDialog.ShowDialog() != true)
			{
				return;
			}
			try
			{
				text = openFileDialog.FileName;
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), OwnerModule?.ModuleDir);
				LoadInModelAsync(text);
			}
			catch (Exception ex)
			{
				int result = 0;
				string strMsg = (int.TryParse(ex.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				if (ex is VmException ex2)
				{
					strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
				}
				HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
			}
		}
	}

	private async void LoadInModelAsync(string path)
	{
		await Task.Run(() =>
		{
			try
			{
				byte[] libFile = EncryptionAndDecryptionHelper.GetLibFile(path);
				if (libFile != null)
				{
					AddMask(AppLocalizationService.GetLang("正在加载模型"));
					imageStitchData.loadXml(libFile);
					Dispatcher.Invoke(() =>
					{
						try
						{
							string[] array = ((IUserStringData)OwnerModuleParam)["ImageMatrix"].Split('X');
							if (array.Length < 2 || int.Parse(array[0]) != imageStitchData.RowsA || int.Parse(array[1]) != imageStitchData.ColumnsA)
							{
								imageStitchData.ClearData(OwnerModuleParam, isItemsClear: true, OwnerModule);
								throw new Exception("模型拼接方式不匹配");
							}
							RefreshImageModel(isImport: true);
							string text = null;
							string text2 = null;
							string value = imageStitchData.RowsA.ToString();
							string value2 = imageStitchData.ColumnsA.ToString();
							if (imageStitchData.ItemsData.Count > 0)
							{
								for (int i = 0; i < imageStitchData.ItemsData.Count; i++)
								{
									text = (imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA.Width.ToString();
									text2 = (imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA.Height.ToString();
									((IUserStringData)OwnerModuleParam)["ImageWidthList-" + i] = text;
									((IUserStringData)OwnerModuleParam)["ImageHeightList-" + i] = text2;
								}
							}
							((IUserBytesData)OwnerModuleParam)["ImportModel"] = imageStitchData.Model;
							((IUserStringData)OwnerModuleParam)["ModelRow"] = value;
							((IUserStringData)OwnerModuleParam)["ModelCol"] = value2;
						}
						catch (Exception ex7)
						{
							int result = 0;
							string strMsg = (int.TryParse(ex7.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex7.Message);
							if (ex7 is VmException ex8)
							{
								strMsg = ErrorCodeInfo.GetErrorInfo(ex8.errorCode);
							}
							HikMessageBox.Show(AppLocalizationService.GetLang("导入模型失败"), strMsg, MessageIcons.Error, isTranslate: true);
						}
					});
				}
			}
			catch (VmException ex)
			{
				VmException ex2 = ex;
				VmException ex3 = ex2;
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex3.errorCode), MessageIcons.Error, isTranslate: true);
				});
			}
			catch (Exception ex4)
			{
				Exception ex5 = ex4;
				Exception ex6 = ex5;
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(ex6.Message, MessageIcons.Error, isTranslate: true);
				});
			}
			finally
			{
				RemoveMask();
			}
		});
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error, isTranslate: true);
			return;
		}
		try
		{
			byte[] model = ((IUserBytesData)OwnerModuleParam)["ExportModel"];
			imageStitchData.Model = model;
			Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
			saveFileDialog.Filter = AppLocalizationService.GetLang("Lang_ModelFile") + "|*.xml";
			saveFileDialog.FilterIndex = 1;
			saveFileDialog.RestoreDirectory = true;
			if (saveFileDialog.ShowDialog() == true)
			{
				LoadOutModelAsync(saveFileDialog.FileName);
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("获取模型失败"), strMsg, MessageIcons.Error, isTranslate: true);
		}
	}

	private async void LoadOutModelAsync(string path)
	{
		await Task.Run(() =>
		{
			try
			{
				AddMask(AppLocalizationService.GetLang("正在导出模型"));
				imageStitchData.PrepareSDKImportData(OwnerModule);
				byte[] bytes = imageStitchData.GetBytes();
				EncryptionAndDecryptionHelper.WriteLibFile(path, bytes);
				Dispatcher.Invoke(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("导出模板文件成功"), MessageIcons.Ok, isTranslate: true);
				});
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				Dispatcher.Invoke(() =>
				{
					string strMsg = ex3.Message;
					if (ex3 is OutOfMemoryException)
					{
						strMsg = AppLocalizationService.GetLang("程序可用内存不足");
					}
					if (ex3 is VmException ex4)
					{
						strMsg = ErrorCodeInfo.GetErrorInfo(ex4.errorCode);
					}
					HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
				});
			}
			finally
			{
				RemoveMask();
			}
		});
	}

	public void GetModelData()
	{
		byte[] array = OwnerModule?.UserData["ImageStitchData"];
		if (array == null)
		{
			return;
		}
		imageStitchData = null;
		UniformImageList.Children.Clear();
		mainGrid.DataContext = null;
		imageStitchData = new ImageStitchData();
		imageStitchData.loadXml(array);
		for (int i = 0; i < imageStitchData.ItemsData.Count; i++)
		{
			if ((imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA != null)
			{
				byte[] imageBuffer = (imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA.ImageBuffer;
				(imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA.ImageBuffer = ((imageBuffer == null) ? OwnerModule.UserData["ImageStitchDataImage-" + i] : ImageSaveOpenCvHelper.BytesToImageData(imageBuffer));
			}
			if ((imageStitchData.ItemsData[i] as StitchImageDataItem).ActualImageDataA != null)
			{
				(imageStitchData.ItemsData[i] as StitchImageDataItem).ActualImageDataA.ImageBuffer = OwnerModule.UserData["ImageStitchActualImage-" + i];
			}
		}
		mainGrid.DataContext = imageStitchData;
	}

	public void SetModelData()
	{
		if (OwnerModule == null || !this.imageStitchData.HasModel)
		{
			return;
		}
		ImageStitchData imageStitchData = ImageStitchData.CopyWithoutBuffer(this.imageStitchData);
		for (int i = 0; i < imageStitchData.ItemsData.Count; i++)
		{
			if (this.imageStitchData.ItemsData.Count > i)
			{
				OwnerModule.UserData["ImageStitchDataImage-" + i] = (this.imageStitchData.ItemsData[i] as StitchImageDataItem).ImageDataA.Buffer;
				if (this.imageStitchData.ImageFixtureEnable)
				{
					OwnerModule.UserData["ImageStitchActualImage-" + i] = (this.imageStitchData.ItemsData[i] as StitchImageDataItem).ActualImageDataA?.Buffer;
				}
			}
		}
		string s = imageStitchData.ToXmlStringWithHead(IsShort: true);
		OwnerModule.UserData["ImageStitchData"] = Encoding.UTF8.GetBytes(s);
	}

	private void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			string s = ((IUserStringData)OwnerModuleParam)["ImageNum"];
			string[] array = ((IUserStringData)OwnerModuleParam)["ImageMatrix"].Split('X');
			int num = 0;
			if (array.Length > 1)
			{
				num = int.Parse(array[0]) * int.Parse(array[1]);
			}
			if (int.Parse(s) == num && num >= 2 && num <= 100)
			{
				mainGrid.IsEnabled = true;
				RefreshImageModel();
			}
			else
			{
				mainGrid.IsEnabled = false;
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			else if (ex is OutOfMemoryException || ex.InnerException is OutOfMemoryException)
			{
				strMsg = AppLocalizationService.GetLang("程序可用内存不足") + "," + AppLocalizationService.GetLang("模型加载失败");
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
		}
		finally
		{
			imageStitchData.OnPropertyChanged("HasModel");
			mainGrid.DataContext = imageStitchData;
		}
	}

	private void UserControl_Unloaded(object sender, RoutedEventArgs e)
	{
		imageStitchData = null;
		UniformImageList.Children.Clear();
		mainGrid.DataContext = null;
	}
}
