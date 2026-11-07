using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;
using Apps.ColorFun;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Hik.Util.Model;
using Hik.Xml;
using HikExternalCall.PatCreate;
using HikPcUI.ImageView;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSMarkFindModuControl;

public partial class MarkFindConfigPanal : UserControl, IComponentConnector
{
	public static readonly DependencyProperty HasModelProperty = DependencyProperty.Register("HasModel", typeof(bool), typeof(MarkFindConfigPanal), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure, HasModelPropertyChanged));

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(MarkFindConfigPanal), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure, TotalPropertyChanged));

	private AlgorithmTabRoot ownerRoot = null;

	public MarkFindDialogData CurModel = null;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

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

	public IParamsConfig OwnerModuleParam { get; protected set; }

	public IVmModule OwnerModule { get; protected set; }

	private static void HasModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as MarkFindConfigPanal).ModelImageVisiableChanged(e.NewValue);
	}

	private void ModelImageVisiableChanged(object value)
	{
		if (value is bool flag)
		{
			if (flag)
			{
				modelImageWarn.Visibility = Visibility.Collapsed;
			}
			else
			{
				modelImageWarn.Visibility = Visibility.Visible;
			}
		}
	}

	private static void TotalPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as MarkFindConfigPanal).ChangeModule(e.NewValue);
	}

	private void ChangeModule(object newParent)
	{
		if (newParent is NameValueItem && ((newParent is NameValueItem nameValueItem) ? nameValueItem.GetTopBaseNode() : null) is AlgorithmTabRoot algorithmTabRoot)
		{
			OwnerModule = algorithmTabRoot.Owner as IVmModule;
			OwnerModuleParam = algorithmTabRoot.UserStringData;
			ownerRoot = algorithmTabRoot;
		}
	}

	public MarkFindConfigPanal()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			GetModelItem();
			RefreshPreviewPanel();
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			SaveModelItem();
		};
	}

	private bool IsByteEquals(byte[] source, byte[] target)
	{
		if (source == null && target == null)
		{
			return true;
		}
		if (source == null || target == null)
		{
			return false;
		}
		if (source.Length != target.Length)
		{
			return false;
		}
		return Encoding.UTF8.GetString(source) == Encoding.UTF8.GetString(target);
	}

	protected void SaveModelItem()
	{
		LogHelper.objLog.Info("Save userdata start.");
		string text = "SolModelFile_0";
		if (CurModel == null)
		{
			LogHelper.objLog.Info("CurModel is empty, clear userdata.");
			string key = text + "_BasicData";
			OwnerModule.UserData[key] = null;
			key = text + "_CurImageData";
			OwnerModule.UserData[key] = null;
			key = text + "_OtherMask";
			OwnerModule.UserData[key] = null;
			key = text + "_PointsData";
			OwnerModule.UserData[key] = null;
			key = text + "_CenterPoint";
			OwnerModule.UserData[key] = null;
			key = text + "_ResultLines";
			OwnerModule.UserData[key] = null;
			LogHelper.objLog.Info("Clear userdata done.");
			return;
		}
		try
		{
			string key2 = text + "_BasicData";
			MarkFindDialogData markFindDialogData = CurModel.DeepCopyModelData(excludeBigData: true);
			byte[] bytes = Encoding.UTF8.GetBytes(((BaseNode)(object)markFindDialogData).ToXmlStringWithHead(true));
			if (bytes != null)
			{
				LogHelper.objLog.Info($"Want to save basic data.Length={bytes.Length}");
				byte[] target = OwnerModule.UserData[key2];
				if (!IsByteEquals(bytes, target))
				{
					LogHelper.objLog.Info("Save basic data start.");
					OwnerModule.UserData[key2] = bytes;
					LogHelper.objLog.Info("Save basic data end.");
				}
				else
				{
					LogHelper.objLog.Info("Basic data is same,skip.");
				}
			}
			else
			{
				OwnerModule.UserData[key2] = null;
			}
			if (CurModel.CurImageData.ImageBuffer != null)
			{
				LogHelper.objLog.Info($"Want to save image data.Length={CurModel.CurImageData.ImageBuffer.Length}");
				key2 = text + "_CurImageData";
				byte[] target2 = OwnerModule.UserData[key2];
				if (!IsByteEquals(CurModel.CurImageData.ImageBuffer, target2))
				{
					LogHelper.objLog.Info("Save image data start.");
					OwnerModule.UserData[key2] = CurModel.CurImageData.ImageBuffer;
					LogHelper.objLog.Info("Save image data end.");
				}
				else
				{
					LogHelper.objLog.Info("Image data is same,skip.");
				}
			}
			else
			{
				key2 = text + "_CurImageData";
				OwnerModule.UserData[key2] = null;
			}
			if (CurModel.otherMask != null)
			{
				LogHelper.objLog.Info($"Want to save otherMask data.Length={CurModel.otherMask.Length}");
				key2 = text + "_OtherMask";
				byte[] target3 = OwnerModule.UserData[key2];
				if (!IsByteEquals(CurModel.otherMask, target3))
				{
					LogHelper.objLog.Info("Save otherMask data start.");
					OwnerModule.UserData[key2] = CurModel.otherMask;
					LogHelper.objLog.Info("Save otherMask data end.");
				}
				else
				{
					LogHelper.objLog.Info("OtherMask data is same,skip.");
				}
			}
			else
			{
				key2 = text + "_OtherMask";
				OwnerModule.UserData[key2] = null;
			}
			byte[] array = SerializationUnit.SerializeObject(CurModel.PointsData);
			if (array != null)
			{
				LogHelper.objLog.Info($"Want to save points data.Length={array.Length}");
				key2 = text + "_PointsData";
				byte[] target4 = OwnerModule.UserData[key2];
				if (!IsByteEquals(array, target4))
				{
					LogHelper.objLog.Info("Save points data start.");
					OwnerModule.UserData[key2] = array;
					LogHelper.objLog.Info("Save points data end.");
				}
				else
				{
					LogHelper.objLog.Info("Points data is same,skip.");
				}
			}
			else
			{
				key2 = text + "_PointsData";
				OwnerModule.UserData[key2] = null;
			}
			byte[] array2 = SerializationUnit.SerializeObject(CurModel.centerPoint);
			if (array2 != null)
			{
				LogHelper.objLog.Info($"Want to save centerpoint data.Length={array2.Length}");
				key2 = text + "_CenterPoint";
				byte[] target5 = OwnerModule.UserData[key2];
				if (!IsByteEquals(array2, target5))
				{
					LogHelper.objLog.Info("Save centerpoint data start.");
					OwnerModule.UserData[key2] = array2;
					LogHelper.objLog.Info("Save centerpoint data end.");
				}
				else
				{
					LogHelper.objLog.Info("Centerpoint data is same,skip.");
				}
			}
			else
			{
				key2 = text + "_CenterPoint";
				OwnerModule.UserData[key2] = null;
			}
			byte[] array3 = SerializationUnit.SerializeObject(CurModel.outputLine);
			if (array3 != null)
			{
				LogHelper.objLog.Info($"Want to save data.Length={array3.Length}");
				key2 = text + "_ResultLines";
				byte[] target6 = OwnerModule.UserData[key2];
				if (!IsByteEquals(array3, target6))
				{
					LogHelper.objLog.Info("Save resultlines data start.");
					OwnerModule.UserData[key2] = array3;
					LogHelper.objLog.Info("Save resultlines data end.");
				}
				else
				{
					LogHelper.objLog.Info("Resultlines data is same,skip.");
				}
			}
			else
			{
				key2 = text + "_ResultLines";
				OwnerModule.UserData[key2] = null;
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"SetUserDataError:{ex.Message}");
		}
		LogHelper.objLog.Info("Save userdata end.");
	}

	protected void GetModelItem()
	{
		LogHelper.objLog.Info("Get userdata start.");
		if (OwnerModuleParam == null || OwnerModule == null)
		{
			LogHelper.objLog.Error("Owenr empty.");
			return;
		}
		string text = "SolModelFile_0";
		string key = text + "_BasicData";
		try
		{
			LogHelper.objLog.Info("Get basic data start.");
			byte[] array = OwnerModule.UserData[key];
			if (array != null)
			{
				LogHelper.objLog.Info($"Basic data length={array.Length}.");
			}
			LogHelper.objLog.Info("Get basic data end.");
			if (array == null)
			{
				return;
			}
			MarkFindDialogData markFindDialogData = new MarkFindDialogData();
			((BaseNode)(object)markFindDialogData).loadXml(array, false);
			if (markFindDialogData == null)
			{
				LogHelper.objLog.Error("Model data empty.");
				return;
			}
			LogHelper.objLog.Info("Get image data start.");
			string key2 = text + "_CurImageData";
			markFindDialogData.CurImageData.ImageBuffer = OwnerModule.UserData[key2];
			if (markFindDialogData.CurImageData.ImageBuffer != null)
			{
				LogHelper.objLog.Info($"Image data length={markFindDialogData.CurImageData.ImageBuffer.Length}.");
			}
			LogHelper.objLog.Info("Get image data end.");
			LogHelper.objLog.Info("Get othermask data start.");
			string key3 = text + "_OtherMask";
			markFindDialogData.otherMask = OwnerModule.UserData[key3];
			if (markFindDialogData.otherMask != null)
			{
				LogHelper.objLog.Info($"Othermask data length={markFindDialogData.otherMask.Length}.");
			}
			LogHelper.objLog.Info("Get othermask data end.");
			LogHelper.objLog.Info("Get points data start.");
			string key4 = text + "_PointsData";
			byte[] array2 = OwnerModule.UserData[key4];
			markFindDialogData.PointsData = (ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>)SerializationUnit.DeserializeObject(array2);
			if (array2 != null)
			{
				LogHelper.objLog.Info($"Points data length={array2.Length}.");
			}
			LogHelper.objLog.Info("Get points data end.");
			LogHelper.objLog.Info("Get centerpoint data start.");
			string key5 = text + "_CenterPoint";
			byte[] array3 = OwnerModule.UserData[key5];
			markFindDialogData.centerPoint = (FILTER_POINT_F)SerializationUnit.DeserializeObject(array3);
			if (array3 != null)
			{
				LogHelper.objLog.Info($"CenterPoint data length={array3.Length}.");
			}
			LogHelper.objLog.Info("Get centerpoint data end.");
			LogHelper.objLog.Info("Get resultlines data start.");
			string key6 = text + "_ResultLines";
			byte[] array4 = OwnerModule.UserData[key6];
			markFindDialogData.outputLine = (FILTER_LINE_I[])SerializationUnit.DeserializeObject(array4);
			if (array4 != null)
			{
				LogHelper.objLog.Info($"Resultlines data length={array4.Length}.");
			}
			LogHelper.objLog.Info("Get resultlines data end.");
			bool needBuildModelData = false;
			if (markFindDialogData.PointsData == null)
			{
				markFindDialogData.PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
				needBuildModelData = true;
			}
			LoadModel(markFindDialogData, needBuildModelData);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"GetUserDataError:{ex.Message}");
		}
	}

	private void RefreshPreviewPanel()
	{
		if (CurModel == null)
		{
			HasModel = false;
			return;
		}
		HasModel = true;
		try
		{
			ImageData modelImageData = CurModel.ModelImageData;
			if (modelImageData == null)
			{
				MarkFindDialogData.RefreshModelImageData(CurModel);
			}
			if (modelImageData != null)
			{
				modelImage.Source = b2SConvertor.BuiltImageSource(modelImageData.ImageBuffer, modelImageData.Width, modelImageData.Height, modelImageData.PixelFormat);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"RefreshPanelError:{ex.Message}");
		}
	}

	private void BtnNewModel_Click(object sender, RoutedEventArgs e)
	{
		AddModel();
	}

	private void AddModel()
	{
		LogHelper.objLog.Info("Create model config window start.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		LogHelper.objLog.Info("Create model start.");
		BaseWindow baseWindow = new BaseWindow
		{
			ShowInTaskbar = false,
			Height = 534.0,
			Width = 860.0,
			Title = AppLocalizationService.GetLang("Tab_Model configuration"),
			FontSize = 14.0,
			FontWeight = FontWeights.Normal,
			MinButtonVisibility = Visibility.Collapsed
		};
		ColorThemesResourceDictionary.GetInstance(baseWindow);
		MarkFindDialogUI markFindDialogUI = (MarkFindDialogUI)(baseWindow.Content = new MarkFindDialogUI
		{
			Owner = baseWindow,
			OwnerModule = OwnerModule,
			OwnerModuleParam = OwnerModuleParam
		});
		if (markFindDialogUI.OwnerModule == null)
		{
			markFindDialogUI.HideCurImageButton();
		}
		baseWindow.Closing += markFindDialogUI.Window_Closing;
		baseWindow.Deactivated += commomConfigWindow_Deactivated;
		LogHelper.objLog.Info("Create a new model.");
		try
		{
			markFindDialogUI.GetCurImage();
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
			LogHelper.objLog.Error($"GetCurImageErr:{ex.Message}.");
			return;
		}
		if (baseWindow.ShowDialog() == true)
		{
			MarkFindDialogData featurepatMatchDialogData = markFindDialogUI.featurepatMatchDialogData;
			if (AddModelToModule(featurepatMatchDialogData))
			{
				CurModel = featurepatMatchDialogData;
				RefreshPreviewPanel();
			}
			(ParentNode as IRunService)?.PreExecuteRunModel();
		}
		baseWindow.Closing -= markFindDialogUI.Window_Closing;
		baseWindow.Deactivated -= commomConfigWindow_Deactivated;
		LogHelper.objLog.Info("Create model end.");
	}

	private void UpdateRunParam()
	{
		if (ownerRoot == null)
		{
			return;
		}
		foreach (Tab tab4 in ownerRoot.Tabs)
		{
			object obj = tab4.FindBaseNodeByName("Expand");
			if (obj != null && obj is Expand expand)
			{
				if (!expand.CurValue)
				{
					return;
				}
				break;
			}
		}
		bool flag = false;
		MarkFindDialogData curModel = CurModel;
		flag = curModel == null || curModel.FeatureMatchType == 1;
		foreach (Tab tab5 in ownerRoot.Tabs)
		{
			object obj2 = tab5.FindBaseNodeByName("RangScaleX");
			if (obj2 != null && obj2 is NameValueItem nameValueItem)
			{
				nameValueItem.CustomVisibleA = flag;
				break;
			}
		}
		foreach (Tab tab6 in ownerRoot.Tabs)
		{
			object obj3 = tab6.FindBaseNodeByName("RangScaleY");
			if (obj3 != null && obj3 is NameValueItem nameValueItem2)
			{
				nameValueItem2.CustomVisibleA = flag;
				break;
			}
		}
	}

	private bool AddModelToModule(MarkFindDialogData itemData)
	{
		try
		{
			((IUserBytesData)OwnerModuleParam)["ClearCandidate"] = Encoding.UTF8.GetBytes("1");
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"ClearAllModelsError:{ex.Message}");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
			return false;
		}
		try
		{
			((IUserStringData)OwnerModuleParam)["FeatureMatchType"] = itemData.FeatureMatchType.ToString();
		}
		catch (Exception ex2)
		{
			LogHelper.objLog.Error($"GetOrSetFeatureMatchTypeError:{ex2.Message}");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
			return false;
		}
		LogHelper.objLog.Info($"Want to add matchmodel data,data Length={itemData.ModelData.Length}.");
		try
		{
			((IUserBytesData)OwnerModuleParam)["AddSpareMatchModel"] = itemData.ModelData;
		}
		catch (Exception ex3)
		{
			LogHelper.objLog.Error($"AddSpareMatchModelError:{ex3.Message}");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
			try
			{
				LogHelper.objLog.Info("Try clear model.");
				((IUserBytesData)OwnerModuleParam)["ClearCandidate"] = Encoding.UTF8.GetBytes("1");
			}
			catch (Exception ex4)
			{
				LogHelper.objLog.Error($"Clear fail:{ex4.Message}");
			}
			return false;
		}
		LogHelper.objLog.Info($"Want to add markmodel data,data Length={itemData.MarkModelData.Length}.");
		try
		{
			((IUserBytesData)OwnerModuleParam)["AddSpareMarkModel"] = itemData.MarkModelData;
		}
		catch (Exception ex5)
		{
			LogHelper.objLog.Error($"AddSpareMarkModelError:{ex5.Message}");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
			try
			{
				LogHelper.objLog.Info("Try clear model.");
				((IUserBytesData)OwnerModuleParam)["ClearCandidate"] = Encoding.UTF8.GetBytes("1");
			}
			catch (Exception ex6)
			{
				LogHelper.objLog.Error($"Clear fail:{ex6.Message}");
			}
			return false;
		}
		return true;
	}

	private void commomConfigWindow_Deactivated(object sender, EventArgs e)
	{
		if (sender is BaseWindow { Content: MarkFindDialogUI content })
		{
			content.imageView.CancelRoiDraw();
		}
	}

	private void BtnEditModel_Click(object sender, RoutedEventArgs e)
	{
		EditModel();
	}

	protected void EditModel()
	{
		LogHelper.objLog.Info("Create model config window strat.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		LogHelper.objLog.Info("Edit model start.");
		BaseWindow baseWindow = new BaseWindow
		{
			ShowInTaskbar = false,
			Height = 534.0,
			Width = 860.0,
			Title = AppLocalizationService.GetLang("Lang_ModelSet"),
			FontSize = 14.0,
			FontWeight = FontWeights.Normal,
			MinButtonVisibility = Visibility.Collapsed
		};
		ColorThemesResourceDictionary.GetInstance(baseWindow);
		MarkFindDialogUI markFindDialogUI = (MarkFindDialogUI)(baseWindow.Content = new MarkFindDialogUI
		{
			Owner = baseWindow,
			OwnerModule = OwnerModule,
			OwnerModuleParam = OwnerModuleParam,
			IsChangingModel = true
		});
		if (markFindDialogUI.OwnerModule == null)
		{
			markFindDialogUI.HideCurImageButton();
		}
		baseWindow.Closing += markFindDialogUI.Window_Closing;
		baseWindow.Deactivated += commomConfigWindow_Deactivated;
		if (CurModel == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoEdit"), MessageIcons.Error);
			LogHelper.objLog.Error("Selected model is empty, edit fail.");
			return;
		}
		MarkFindDialogData curModel = CurModel;
		markFindDialogUI.needSkipData.AddRange(curModel.ItemsData);
		if (curModel == null)
		{
			LogHelper.objLog.Error("CurModelData type error, edit fail.");
			return;
		}
		markFindDialogUI.featurepatMatchDialogData = curModel;
		markFindDialogUI.imageView.DataContext = curModel;
		markFindDialogUI.DataContext = curModel;
		MarkFindDialogData curModel2 = curModel.DeepCopyModelData();
		markFindDialogUI.RefreshByModelData();
		markFindDialogUI.DrawLinesAndPoints(curModel.centerPoint, curModel.outputLine);
		if (baseWindow.ShowDialog() == true)
		{
			LogHelper.objLog.Info($"Close model config window. Want to change model data.");
			try
			{
				((IUserBytesData)OwnerModuleParam)["ClearCandidate"] = Encoding.UTF8.GetBytes("1");
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error($"ClearAllModelsError:{ex.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoEdit"), MessageIcons.Error);
				CurModel = curModel2;
				return;
			}
			try
			{
				((IUserStringData)OwnerModuleParam)["FeatureMatchType"] = CurModel.FeatureMatchType.ToString();
			}
			catch (Exception ex2)
			{
				LogHelper.objLog.Error($"GetOrSetFeatureMatchTypeError:{ex2.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
				CurModel = curModel2;
				return;
			}
			LogHelper.objLog.Info($"Want to add matchmodel data,data Length={CurModel.ModelData.Length}.");
			try
			{
				((IUserBytesData)OwnerModuleParam)["AddSpareMatchModel"] = CurModel.ModelData;
			}
			catch (Exception ex3)
			{
				LogHelper.objLog.Error($"AddSpareMatchModelError:{ex3.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
				CurModel = curModel2;
				return;
			}
			LogHelper.objLog.Info($"Want to add markmodel data,data Length={CurModel.MarkModelData.Length}.");
			try
			{
				((IUserBytesData)OwnerModuleParam)["AddSpareMarkModel"] = CurModel.MarkModelData;
			}
			catch (Exception ex4)
			{
				LogHelper.objLog.Error($"AddSpareMatchModelError:{ex4.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
				CurModel = curModel2;
				return;
			}
			RefreshPreviewPanel();
			(ParentNode as IRunService)?.PreExecuteRunModel();
			LogHelper.objLog.Info("Edit model succeed.");
		}
		else
		{
			CurModel = curModel2;
			RefreshPreviewPanel();
			LogHelper.objLog.Info("Abandon, roll back model data.");
		}
		baseWindow.Closing -= markFindDialogUI.Window_Closing;
		baseWindow.Deactivated -= commomConfigWindow_Deactivated;
		LogHelper.objLog.Info("Create model config window end.");
	}

	private void Import_Click(object sender, RoutedEventArgs e)
	{
		ImportModel();
	}

	protected void ImportModel()
	{
		LogHelper.objLog.Info("Load model strat.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "(*.mfxml)|*.mfxml"
		};
		string text = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(openFileDialog, text);
		LogHelper.objLog.Info($"Get initpath:{text}.");
		if (openFileDialog.ShowDialog() == true)
		{
			string fileName = openFileDialog.FileName;
			LogHelper.objLog.Info($"Want to load file:{fileName}.");
			MarkFindDialogData markFindDialogData = new MarkFindDialogData();
			LogHelper.objLog.Info("Load xml start.");
			try
			{
				using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					if (fileStream.Length > 1073741824)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LengthError"), MessageIcons.Error);
						return;
					}
				}
				byte[] libFile = EncryptionAndDecryptionHelper.GetLibFile(fileName);
				if (libFile == null)
				{
					return;
				}
				XmlDocument xmlDocument = new XmlDocument();
				using (MemoryStream memoryStream = new MemoryStream(libFile))
				{
					memoryStream.Flush();
					memoryStream.Position = 0L;
					xmlDocument.Load(memoryStream);
				}
				XmlNode xmlNode = xmlDocument.SelectSingleNode("Apps.XmlParser.ParamTab.MarkFindDialogData");
				if (xmlNode == null)
				{
					throw new Exception("Node MarkFindDialogData not found");
				}
				((BaseNode)(object)markFindDialogData).loadXmlByString(xmlNode.OuterXml, false);
				if (markFindDialogData.CurImageData == null)
				{
					throw new Exception("ImageDataEmpty");
				}
				LoadModel(markFindDialogData);
			}
			catch (VmException ex)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageButtons.Normal, MessageIcons.Error);
			}
			catch (Exception ex2)
			{
				LogHelper.objLog.Error($"LoadModuleError:{ex2.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadErrtoDecode"), MessageIcons.Error);
			}
			if (File.Exists(openFileDialog.FileName))
			{
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(openFileDialog.FileName), OwnerModule?.ModuleDir);
			}
			LogHelper.objLog.Info("Load xml end.");
		}
		LogHelper.objLog.Info("Load model end.");
	}

	protected void LoadModel(MarkFindDialogData itemData, bool needBuildModelData = true)
	{
		if (needBuildModelData)
		{
			LogHelper.objLog.Info("Rebuild model");
			int outLinePoints = itemData.GetOutLinePoints();
			if (outLinePoints != 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CPRebuidErr"), MessageIcons.Warning);
				LogHelper.objLog.Error($"GetOutLinePointsError:ret={outLinePoints}");
			}
			else if (AddModelToModule(itemData))
			{
				CurModel = itemData;
				RefreshPreviewPanel();
				LogHelper.objLog.Info("Add model succeed.");
			}
			else
			{
				LogHelper.objLog.Info("Add model fail.");
			}
		}
		else
		{
			LogHelper.objLog.Info("Add model directly.");
			CurModel = itemData;
			RefreshPreviewPanel();
			LogHelper.objLog.Info("Add model done.");
		}
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		ExportModel();
	}

	protected void ExportModel()
	{
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Expected Obj, but got Unknown
		LogHelper.objLog.Info("Export model start.");
		if (CurModel.ItemsData == null || CurModel.ItemsData.Count != 1 || CurModel.PointsData == null || CurModel.PointsData.Count == 0)
		{
			LogHelper.objLog.Error($"Create model error, export fail.");
			return;
		}
		if (CurModel.ModelData == null || CurModel.MarkModelData == null)
		{
			LogHelper.objLog.Error($"Model data or mark data empty, try rebuild.");
			int outLinePoints = CurModel.GetOutLinePoints();
			if (outLinePoints != 0)
			{
				LogHelper.objLog.Error($"GetOutLinePointsError:ret={outLinePoints}");
				return;
			}
			LogHelper.objLog.Error($"Rebuild done.");
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "xml files(*.mfxml)|*.mfxml"
		};
		string path = ConfigurationHelper.INIGetDefaultOpenPath(OwnerModule?.ModuleDir);
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(saveFileDialog, path);
		LogHelper.objLog.Info($"Want to export file:{saveFileDialog.FileName}.");
		if (saveFileDialog.ShowDialog() == true)
		{
			try
			{
				SDKMarkFindUIData sDKMarkFindUIData = new SDKMarkFindUIData();
				MarkFindDialogData markFindDialogData = CurModel.DeepCopyModelData(excludeBigData: true);
				((SDKFeatureMatchUIData)sDKMarkFindUIData).BasicData_ByteArray = Encoding.UTF8.GetBytes(((BaseNode)(object)markFindDialogData).ToXmlStringWithHead(true));
				((SDKFeatureMatchUIData)sDKMarkFindUIData).CurImageData_ByteArray = CurModel.CurImageData.ImageBuffer;
				((SDKFeatureMatchUIData)sDKMarkFindUIData).OtherMask_ByteArray = CurModel.otherMask;
				((SDKFeatureMatchUIData)sDKMarkFindUIData).PointsData_ByteArray = SerializationUnit.SerializeObject(CurModel.PointsData);
				sDKMarkFindUIData.CenterPoint_ByteArray = SerializationUnit.SerializeObject(CurModel.centerPoint);
				sDKMarkFindUIData.ResultLines_ByteArray = SerializationUnit.SerializeObject(CurModel.outputLine);
				SDKFeatureMatchData sDKImportData = new SDKFeatureMatchData
				{
					ModuleModelData = new SDKMarkFindModelData
					{
						AddSpareMatchModel_ByteArray = CurModel.ModelData,
						AddSpareMarkModel_ByteArray = CurModel.MarkModelData,
						FeatureMatchType = CurModel.FeatureMatchType.ToString()
					},
					UserInterfaceData = (SDKFeatureMatchUIData)(object)sDKMarkFindUIData
				};
				CurModel.SDKImportData = sDKImportData;
				string s = ((BaseNode)(object)CurModel).ToXmlStringWithHead(false);
				EncryptionAndDecryptionHelper.WriteLibFile(saveFileDialog.FileName, Encoding.UTF8.GetBytes(s));
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
				if (File.Exists(saveFileDialog.FileName))
				{
					ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), OwnerModule?.ModuleDir);
				}
			}
			catch (VmException ex)
			{
				int errorCode = ex.errorCode;
				HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + " " + AppLocalizationService.GetLang(ErrorCodeInfo.GetErrorInfo(errorCode)), MessageIcons.Error);
			}
			catch (Exception ex2)
			{
				LogHelper.objLog.Error($"ExportError:{ex2.Message}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Savefailed"), MessageIcons.Error);
			}
		}
		LogHelper.objLog.Info("Export model end.");
	}

	private void BtnClear_Click(object sender, RoutedEventArgs e)
	{
		DeleteModel();
	}

	protected void DeleteModel()
	{
		LogHelper.objLog.Info("Delete model start.");
		if (GetIsBusy())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PlzLoopStop"), MessageIcons.Error);
			return;
		}
		if (CurModel == null)
		{
			LogHelper.objLog.Info("Current model empty. No need to delete.");
			return;
		}
		try
		{
			((IUserBytesData)OwnerModuleParam)["ClearCandidate"] = Encoding.UTF8.GetBytes("1");
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"ClearAllModelsError:{ex.Message}");
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
			return;
		}
		LogHelper.objLog.Info("Try to delete userdata.");
		try
		{
			string text = "SolModelFile_0";
			OwnerModule.UserData[text] = null;
			string key = text + "_CurImageData";
			OwnerModule.UserData[key] = null;
			string key2 = text + "_OtherMask";
			OwnerModule.UserData[key2] = null;
			string key3 = text + "_PointsData";
			OwnerModule.UserData[key3] = null;
			string key4 = text + "_CenterPoint";
			OwnerModule.UserData[key4] = null;
			string key5 = text + "_ResultLines";
			OwnerModule.UserData[key5] = null;
		}
		catch (Exception ex2)
		{
			LogHelper.objLog.Error($"ClearUserDataError:{ex2.Message}");
		}
		LogHelper.objLog.Info("Try to delete userdata done.");
		CurModel = null;
		RefreshPreviewPanel();
	}

	private bool GetIsBusy()
	{
		if (OwnerModule == null || !(OwnerModule is IVMRun))
		{
			return false;
		}
		return (OwnerModule as IVMRun).IsRunning;
	}
}
