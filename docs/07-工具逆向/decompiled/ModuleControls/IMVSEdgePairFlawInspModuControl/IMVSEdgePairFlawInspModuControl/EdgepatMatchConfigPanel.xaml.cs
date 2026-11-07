using System;
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
using Hik.Xml;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSEdgePairFlawInspModuControl;

public partial class EdgepatMatchConfigPanel : Control, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty TabSourceProperty = DependencyProperty.Register("TabSource", typeof(Tab), typeof(EdgepatMatchConfigPanel), new PropertyMetadata(null));

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(EdgepatMatchConfigPanel), new PropertyMetadata(null));

	private IVmModule ownerModule = null;

	private Button editButton;

	private Image imageView;

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

	public EdgepatMatchConfigPanel()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		editButton = Template.FindName("btnEditTemplate", this) as Button;
		imageView = Template.FindName("imageView", this) as Image;
		Loaded += (object s, RoutedEventArgs e) =>
		{
			EdgepatMatchData edgepatMatchData = new EdgepatMatchData();
			Panel_EdgePairpatMatch.GetModel(OwnerModuleParam, edgepatMatchData);
			if (edgepatMatchData.PointsData.Count != 0 && edgepatMatchData.ModelData != null)
			{
				editButton.IsEnabled = true;
				RefreshPreviewPanel(edgepatMatchData);
			}
		};
		Init();
	}

	private void Init()
	{
	}

	private void Build_Click(object sender, RoutedEventArgs e)
	{
		PopModelDialog();
	}

	private void RefreshPreviewPanel(EdgepatMatchData model)
	{
		if (model == null)
		{
			editButton.IsEnabled = false;
			imageView.Source = new BitmapImage(new Uri("/VisionMaster;component/Resources/pic_边缘缺陷检测.png", UriKind.RelativeOrAbsolute));
			return;
		}
		editButton.IsEnabled = true;
		try
		{
			ImageData modelImageData = model.ModelImageData;
			if (modelImageData == null)
			{
				Panel_EdgePairpatMatch.CreateModelImageData(model);
				modelImageData = model.ModelImageData;
			}
			if (modelImageData != null)
			{
				imageView.Source = b2SConvertor.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
			}
		}
		catch (Exception ex)
		{
			int result = 0;
			string message = (int.TryParse(ex.Message, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			if (ex is VmException ex2)
			{
				message = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			LogHelper.Error(message);
		}
	}

	private void btnEditTemplate_Click(object sender, RoutedEventArgs e)
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
		EdgepatMatchData model = new EdgepatMatchData();
		Panel_EdgePairpatMatch.GetModel(OwnerModuleParam, model);
		TabSource.GetItemsValueUnit();
		model.ParamTab = TabSource.Clone() as Tab;
		EdgepatMatchDialogWindow edgepatMatchDialogWindow = new EdgepatMatchDialogWindow(TabSource);
		edgepatMatchDialogWindow.OwnerModule = OwnerModule;
		edgepatMatchDialogWindow.OwnerModuleParam = OwnerModuleParam;
		if (isEdit)
		{
			Panel_EdgePairpatMatch.GetModel(OwnerModuleParam, edgepatMatchDialogWindow.edgepatMatchData);
			edgepatMatchDialogWindow.CurImage = edgepatMatchDialogWindow.edgepatMatchData.CurImageData;
		}
		else
		{
			try
			{
				edgepatMatchDialogWindow.GetCurImage();
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				return;
			}
		}
		CreatModel.Instance.IsCreatingModel = true;
		if (edgepatMatchDialogWindow.ShowDialog() == true)
		{
			RefreshPreviewPanel(edgepatMatchDialogWindow.edgepatMatchData);
			(ParentNode as IRunService)?.PreExecuteRunModel();
		}
		else
		{
			SetModelParams(model);
			CreatModel.Instance.IsCreatingModel = false;
			if (!Panel_EdgePairpatMatch.CreateModel(OwnerModuleParam, ref model))
			{
				try
				{
					((IUserStringData)OwnerModuleParam)["ClearModelData"] = "";
				}
				catch (Exception)
				{
				}
				LogHelper.Error("模型撤销错误:建模失败");
				return;
			}
		}
		CreatModel.Instance.IsCreatingModel = false;
	}

	private void LoadModule_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerModule is IVMRun { IsRunning: not false })
		{
			HikMessageBox.Show("Lang_PlzLoopStop", MessageIcons.Error, isTranslate: true);
			return;
		}
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Filter = "(*.epmxml) | *.epmxml";
		string text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		if (text != null)
		{
			openFileDialog.InitialDirectory = text;
		}
		if (openFileDialog.ShowDialog() == true)
		{
			text = openFileDialog.FileName;
			LoadModelByPath(text);
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), OwnerModule?.ModuleDir);
		}
	}

	private void LoadModelByPath(string path, bool needPopTip = true)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}
		EdgepatMatchData model = new EdgepatMatchData();
		try
		{
			byte[] libFile = EncryptionAndDecryptionHelper.GetLibFile(path);
			if (libFile != null)
			{
				((BaseNode)(object)model).loadXml(libFile, false);
				SetModelParams(model);
				if (model.CurImageData == null)
				{
					throw new Exception("ImageData is null.");
				}
				if (Panel_EdgePairpatMatch.CreateModel(OwnerModuleParam, ref model, needPopTip, isLoad: true))
				{
					RefreshPreviewPanel(model);
				}
			}
		}
		catch (VmException ex)
		{
			if (needPopTip)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageButtons.Normal, MessageIcons.Error);
			}
		}
		catch (Exception)
		{
			if (needPopTip)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadErrtoDecode"), MessageIcons.Error);
			}
		}
	}

	private void SetModelParams(EdgepatMatchData modelData)
	{
		if (modelData.ParamTab == null)
		{
			return;
		}
		foreach (BaseNode item in modelData.ParamTab.Collection)
		{
			try
			{
				if (item is NameValueItem { Name: not null } nameValueItem && OwnerModuleParam != null)
				{
					((IUserStringData)OwnerModuleParam)[nameValueItem.Name] = nameValueItem.Value;
					if (TabSource.FindBaseNodeByName(nameValueItem.Name) is NameValueItem nameValueItem2)
					{
						nameValueItem2.GetParamUnit();
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue && IsLoaded)
		{
			LoadModelByPath(Panel_EdgePairpatMatch.GetSdkImportModelPath(OwnerModuleParam), needPopTip: false);
		}
	}
}
