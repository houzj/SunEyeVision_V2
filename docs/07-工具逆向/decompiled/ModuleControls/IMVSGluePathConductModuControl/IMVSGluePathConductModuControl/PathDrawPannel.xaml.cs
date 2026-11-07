using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSGluePathConductModuControl;

public partial class PathDrawPannel : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TabSourceProperty = DependencyProperty.Register("TabSource", typeof(Tab), typeof(PathDrawPannel), new PropertyMetadata(null));

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(PathDrawPannel), new PropertyMetadata(null));

	private IVmModule ownerModule = null;

	public static readonly DependencyProperty HasModelProperty = DependencyProperty.Register("HasModel", typeof(bool), typeof(PathDrawPannel), new PropertyMetadata(false));

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	public Tab TabSource
	{
		get
		{
			return (Tab)GetValue(TabSourceProperty);
		}
		set
		{
			SetValue(TabSourceProperty, value);
		}
	}

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
			if (ownerModule == null && ParentNode is NameValueItem nameValueItem && nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				ownerModule = algorithmTabRoot.Owner as IVmModule;
			}
			return ownerModule;
		}
	}

	public IParamsConfig OwnerModuleParam
	{
		get
		{
			IParamsConfig paramsConfig = OwnerModule?.Params;
			if (paramsConfig == null && ParentNode is NameValueItem nameValueItem && nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				paramsConfig = algorithmTabRoot.UserStringData;
			}
			return paramsConfig;
		}
	}

	public bool HasModel
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

	public PathDrawPannel()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			PathGuidData pathGuidData = new PathGuidData();
			PathDrawingGuide.GetModel(OwnerModuleParam, pathGuidData);
			if (pathGuidData.PointsData.Count != 0)
			{
				HasModel = true;
				RefreshPreviewPanel(pathGuidData);
			}
		};
	}

	private void BtnNewModel_Click(object sender, RoutedEventArgs e)
	{
		PopModelDialog();
	}

	private void BtnEditModel_Click(object sender, RoutedEventArgs e)
	{
		PopModelDialog(isEdit: true);
	}

	private void PopModelDialog(bool isEdit = false)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Error, isTranslate: true);
			return;
		}
		PathGuidData pathGuidData = new PathGuidData();
		PathDrawingGuide.GetModel(OwnerModuleParam, pathGuidData);
		PathDrawingGuide.GetModuleParam(TabSource);
		pathGuidData.ParamTab = TabSource.Clone() as Tab;
		PathDrawingGuide.GetModelParamsConfig(OwnerModuleParam, pathGuidData, TabSource);
		PathDrawingDialog pathDrawingDialog = new PathDrawingDialog(OwnerModule, OwnerModuleParam, TabSource, HasModel);
		pathDrawingDialog.Title = AppLocalizationService.GetLang("Tab_Model configuration");
		if (isEdit)
		{
			PathDrawingGuide.GetModel(OwnerModuleParam, pathDrawingDialog.pathGuidData);
			PathDrawingGuide.GetModelParamsConfig(OwnerModuleParam, pathDrawingDialog.pathGuidData, TabSource);
			pathDrawingDialog.OldParamValues = pathDrawingDialog.pathGuidData.ParamValues;
			pathDrawingDialog.CurImage = pathDrawingDialog.pathGuidData.CurImageData;
			pathDrawingDialog.UpdatePointROI();
			pathDrawingDialog.RecordHistory();
		}
		else
		{
			try
			{
				pathDrawingDialog.GetCurImage();
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				return;
			}
		}
		if (pathDrawingDialog.ShowDialog() == true)
		{
			PathDrawingGuide.CreateModelImageData(pathDrawingDialog.pathGuidData);
			RefreshPreviewPanel(pathDrawingDialog.pathGuidData);
			(ParentNode as IRunService)?.PreExecuteRunModel();
			return;
		}
		try
		{
			PathDrawingGuide.SetModelParams(OwnerModuleParam, pathGuidData);
			PathDrawingGuide.SetModelParam(OwnerModuleParam, pathGuidData);
			PathDrawingGuide.SetModelTranj(OwnerModuleParam, pathGuidData);
			PathDrawingGuide.SetModelEdgePoint(OwnerModuleParam, pathGuidData);
		}
		catch (Exception)
		{
		}
	}

	private void RefreshPreviewPanel(PathGuidData model)
	{
		if (model == null)
		{
			HasModel = false;
			imgModel.Source = new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/图像运行_图片.png", UriKind.RelativeOrAbsolute));
			return;
		}
		HasModel = true;
		try
		{
			ImageData modelImageData = model.ModelImageData;
			if (modelImageData == null)
			{
				PathDrawingGuide.CreateModelImageData(model);
				modelImageData = model.ModelImageData;
			}
			if (modelImageData != null)
			{
				imgModel.Source = b2SConvertor.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
	}

	private void BtnClear_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (OwnerModuleParam != null)
			{
				((IUserBytesData)OwnerModuleParam)["ClearModel"] = new byte[1];
				RefreshPreviewPanel(null);
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
			LogHelper.objLog.Error(ex.StackTrace);
		}
	}

	private void Import_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Filter = "xml files(*.xml)|*.xml";
		string text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		if (openFileDialog.ShowDialog() == true)
		{
			text = openFileDialog.FileName;
			if (LoadModelByPath(text))
			{
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), OwnerModule?.ModuleDir);
			}
		}
	}

	private bool LoadModelByPath(string path, bool needPopTip = true)
	{
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		PathGuidData pathGuidData = new PathGuidData();
		try
		{
			if (OwnerModuleParam != null)
			{
				try
				{
					((IUserBytesData)OwnerModuleParam)["ClearModel"] = new byte[1];
				}
				catch
				{
				}
			}
			byte[] array = null;
			try
			{
				array = EncryptionAndDecryptionHelper.GetLibFile(path);
				if (array == null)
				{
					return false;
				}
			}
			catch (VmException ex)
			{
				int errorCode = ex.errorCode;
				HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + " " + AppLocalizationService.GetLang(ErrorCodeInfo.GetErrorInfo(errorCode)), MessageIcons.Error);
				return false;
			}
			catch (Exception ex2)
			{
				string text = AppLocalizationService.GetLang("读取样本库失败") + " :" + AppLocalizationService.GetLang("读取文件失败");
				LogHelper.Error(text);
				if (ex2 is EndOfStreamException)
				{
					text = AppLocalizationService.GetLang("读取样本库失败") + " :" + AppLocalizationService.GetLang("文件格式异常") + "!";
				}
				HikMessageBox.Show(text, MessageIcons.Error, isTranslate: true);
				return false;
			}
			pathGuidData.loadXml(array);
			if (pathGuidData.CurImageData == null)
			{
				throw new Exception("ImageData is null.");
			}
			PathDrawingGuide.SetModelParam(OwnerModuleParam, pathGuidData);
			PathDrawingGuide.SetModelParams(OwnerModuleParam, pathGuidData);
			PathDrawingGuide.SetModelTranj(OwnerModuleParam, pathGuidData);
			PathDrawingGuide.SetModelEdgePoint(OwnerModuleParam, pathGuidData);
			RefreshPreviewPanel(pathGuidData);
			return true;
		}
		catch
		{
			if (needPopTip)
			{
				HikMessageBox.Show("Lang_LoadErrtoDecode", MessageIcons.Error, isTranslate: true);
			}
		}
		return false;
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		PathGuidData pathGuidData = new PathGuidData();
		PathDrawingGuide.GetModel(OwnerModuleParam, pathGuidData);
		PathDrawingGuide.GetModuleParam(TabSource);
		PathDrawingGuide.GetModelParamsConfig(OwnerModuleParam, pathGuidData, TabSource);
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
		saveFileDialog.Filter = "xml files(*.xml)|*.xml";
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
			pathGuidData.ParamTab = TabSource;
			pathGuidData.PrepareSDKImportData(OwnerModuleParam);
			byte[] bytes = pathGuidData.GetBytes();
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
			return;
		}
		HikMessageBox.Show("Lang_SaveSuccess", MessageIcons.Ok, isTranslate: true);
		ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), OwnerModule?.ModuleDir);
	}
}
