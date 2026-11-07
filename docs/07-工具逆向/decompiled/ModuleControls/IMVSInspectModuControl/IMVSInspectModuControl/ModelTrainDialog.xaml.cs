using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using VM.Controls;
using VM.Core;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSInspectModuControl;

public partial class ModelTrainDialog : BaseWindow, INotifyPropertyChanged, IComponentConnector
{
	private ProgressDialogContext _progressContext = null;

	private ProgressDialog _progressDialog = null;

	private bool _isCloseDialog = true;

	private ModelType _curModelType = ModelType.Create;

	private object _parentNode;

	private ButtonSelecter _roiSelecter;

	private EnumerationG imageEnumeration;

	private VmIO roiIO = null;

	private VmIO maskIO = null;

	public static readonly DependencyProperty CurTrainStepProperty = DependencyProperty.Register("CurTrainStep", typeof(ModelTrainStep), typeof(ModelTrainDialog), new PropertyMetadata((object)(ModelTrainStep)0));

	internal GuideButton gbLocationModel;

	internal GuideButton gbTrainRegion;

	internal GuideButton gbBatchTrain;

	public ModelTrainStep CurTrainStep
	{
		get
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			return (ModelTrainStep)GetValue(CurTrainStepProperty);
		}
		set
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			SetValue(CurTrainStepProperty, value);
		}
	}

	public double LastStepWidth => TrainDataMgr.Instance.IsEnglish ? 90 : 66;

	public ModelType CurModelType
	{
		get
		{
			return _curModelType;
		}
		set
		{
			if (_curModelType != value)
			{
				_curModelType = value;
				OnPropertyChanged("CurModelType");
				OnPropertyChanged("FirstRowHeight");
			}
		}
	}

	public object ParentNode
	{
		get
		{
			return _parentNode;
		}
		set
		{
			if (_parentNode != value)
			{
				_parentNode = value;
				ROISelecter.SetParentNode(_parentNode);
				ROISelecter.InniterBusiness();
				imageEnumeration = null;
				OnPropertyChanged("ImageEnumeration");
			}
		}
	}

	public ButtonSelecter ROISelecter
	{
		get
		{
			if (_roiSelecter == null)
			{
				_roiSelecter = new ButtonSelecter();
				_roiSelecter.SubscribeInPopWindowEnableA = false;
				_roiSelecter.DisplayName = "Region";
				_roiSelecter.Description = "Region";
				_roiSelecter.DropOpenSetter = new Apps.XmlParser.ParamTab.Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontCombinationTreeOperation",
					OperationParams = "ROIBOX"
				};
				_roiSelecter.DropOpenSetter.SetParentNode(_roiSelecter);
				_roiSelecter.Triggers = new ObservableCollection<object>();
				Apps.XmlParser.ParamTab.Trigger trigger = new Apps.XmlParser.ParamTab.Trigger
				{
					Property = "CurValue"
				};
				Apps.XmlParser.ParamTab.Setter setter = new Apps.XmlParser.ParamTab.Setter
				{
					OperationName = "SetCombinationSourceOperation",
					OperationParams = "InputROI"
				};
				setter.CreatOperateInstance();
				trigger.Setters = new ObservableCollection<object>();
				trigger.Setters.Add(setter);
				setter.SetParentNode(trigger);
				_roiSelecter.Triggers.Add(trigger);
				trigger.SetParentNode(_roiSelecter);
				_roiSelecter.Initers = new ObservableCollection<object>();
				Apps.XmlParser.ParamTab.Setter setter2 = new Apps.XmlParser.ParamTab.Setter
				{
					TargetName = "CurValue",
					OperationName = "GetSelectedCombinationOperation",
					OperationParams = "InputROI"
				};
				setter2.CreatOperateInstance();
				_roiSelecter.Initers.Add(setter2);
				setter2.SetParentNode(_roiSelecter);
			}
			return _roiSelecter;
		}
	}

	public EnumerationG ImageEnumeration
	{
		get
		{
			if (imageEnumeration == null)
			{
				imageEnumeration = new EnumerationG();
				imageEnumeration.DisplayName = "Mask Image";
				imageEnumeration.Description = "Mask Image";
				imageEnumeration.SetParentNode(_parentNode);
				imageEnumeration.Triggers = new ObservableCollection<object>();
				Apps.XmlParser.ParamTab.Trigger trigger = new Apps.XmlParser.ParamTab.Trigger
				{
					Property = "CurValue"
				};
				Apps.XmlParser.ParamTab.Setter setter = new Apps.XmlParser.ParamTab.Setter
				{
					OperationName = "SetCombinationSourceOperation",
					OperationParams = "InputMask"
				};
				setter.CreatOperateInstance();
				trigger.Setters = new ObservableCollection<object>();
				trigger.Setters.Add(setter);
				setter.SetParentNode(trigger);
				imageEnumeration.Triggers.Add(trigger);
				trigger.SetParentNode(imageEnumeration);
				imageEnumeration.Initers = new ObservableCollection<object>();
				Apps.XmlParser.ParamTab.Setter setter2 = new Apps.XmlParser.ParamTab.Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontParamItemsOperation",
					OperationParams = "IMAGE"
				};
				setter2.CreatOperateInstance();
				imageEnumeration.Initers.Add(setter2);
				setter2.SetParentNode(imageEnumeration);
				Apps.XmlParser.ParamTab.Setter setter3 = new Apps.XmlParser.ParamTab.Setter
				{
					TargetName = "CurValue",
					OperationName = "GetSelectedCombinationOperation",
					OperationParams = "InputMask"
				};
				setter3.CreatOperateInstance();
				imageEnumeration.Initers.Add(setter3);
				setter3.SetParentNode(imageEnumeration);
			}
			imageEnumeration.InniterBusiness();
			return imageEnumeration;
		}
	}

	public double FirstRowHeight => (CurModelType == ModelType.Create) ? 56 : 44;

	public ModelTrainDialog()
	{
		InitializeComponent();
		int num = 0;
		try
		{
			string s = ((IUserStringData)TrainDataMgr.Instance.Param)["RoiInheritType"];
			num = int.Parse(s);
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			LogHelper.moduleLog.Error(text);
		}
		if (num == 0)
		{
			region.IsChecked = true;
		}
		else
		{
			image.IsChecked = true;
		}
		roiIO = TrainDataMgr.Instance.Module.Inputs.OfType<VmIO>().SingleOrDefault((VmIO p) => p.Name == "InputROI");
		if (roiIO != null)
		{
			roiIO.RelatedIOChanged += RelatedIOChanged;
		}
		maskIO = TrainDataMgr.Instance.Module.Inputs.OfType<VmIO>().SingleOrDefault((VmIO p) => p.Name == "InputMask");
		if (maskIO != null)
		{
			maskIO.RelatedIOChanged += RelatedIOChanged;
		}
	}

	private void NextStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		ModelTrainStep curTrainStep = CurTrainStep;
		if ((int)curTrainStep != 0)
		{
			if ((int)curTrainStep == 1)
			{
				if (!TrainDataMgr.Instance.IsStartTrain && pageTrainRegion.imageView.Source != null)
				{
					pageTrainRegion.SetRoiData();
				}
				((UIElement)(object)gbBatchTrain).IsEnabled = true;
				gbBatchTrain.IsCurrentStep = true;
				gbTrainRegion.IsCurrentStep = false;
				CurTrainStep = (ModelTrainStep)2;
				pageBatchTrain.NeedUpdateDisplay();
			}
			return;
		}
		SetSkipHpMatch();
		((UIElement)(object)gbTrainRegion).IsEnabled = true;
		gbTrainRegion.IsCurrentStep = true;
		gbLocationModel.IsCurrentStep = false;
		CurTrainStep = (ModelTrainStep)1;
		if (pageLocationModel.CurImage != null && pageTrainRegion.imageView.ImageDataSource == null)
		{
			pageTrainRegion.imageView.ImageDataSource = pageLocationModel.CurImage;
			pageTrainRegion.imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(pageLocationModel.CurImage.ImageBuffer, pageLocationModel.CurImage.Width, pageLocationModel.CurImage.Height, pageLocationModel.CurImage.PixelFormat);
			pageTrainRegion.InitRoi();
		}
		if (pageLocationModel.MatchBox.width > 0f && pageLocationModel.MatchBox.height > 0f && TrainDataMgr.Instance.IsHasModel)
		{
			pageTrainRegion.DrawMatchBox(pageLocationModel.MatchBox);
		}
		if (pageTrainRegion.imageView.ImageDataSource == null)
		{
			btnNextStep.IsEnabled = false;
			((UIElement)(object)gbBatchTrain).IsEnabled = false;
		}
	}

	private void LastStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Invalid comparison between Unknown and I4
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		ModelTrainStep curTrainStep = CurTrainStep;
		if ((int)curTrainStep != 1)
		{
			if ((int)curTrainStep == 2)
			{
				gbTrainRegion.IsCurrentStep = true;
				gbBatchTrain.IsCurrentStep = false;
				CurTrainStep = (ModelTrainStep)1;
			}
		}
		else
		{
			gbLocationModel.IsCurrentStep = true;
			gbTrainRegion.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)0;
			btnNextStep.IsEnabled = true;
		}
	}

	private void TrainFinish_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void GuideButton_OnClick(object sender, RoutedEventArgs e)
	{
		GuideButton val = (GuideButton)((sender is GuideButton) ? sender : null);
		if (val != null)
		{
			ModifyCurStep(val);
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!_isCloseDialog)
		{
			e.Cancel = true;
			return;
		}
		DeInit();
		DialogResult = true;
		base.OnClosing(e);
	}

	private void InheritMode_Checked(object sender, RoutedEventArgs e)
	{
		int num = 0;
		string name = (sender as FrameworkElement).Name;
		if (!(name == "region"))
		{
			if (name == "image")
			{
				num = 1;
			}
		}
		else
		{
			num = 0;
		}
		try
		{
			((IUserStringData)TrainDataMgr.Instance.Param)["RoiInheritType"] = num.ToString();
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(text, MessageIcons.Error);
		}
	}

	private void RelatedIOChanged(object sender, EventArgs e)
	{
		string value = "";
		if (e is ValueEventArgs { Value: VmIO value2 })
		{
			value = string.Format("{0};{1}", value2.Root.ID.ToString(), string.Join(";", from p in value2.GetAllLeafIO()
				select p.Name));
		}
		try
		{
			((IUserStringData)TrainDataMgr.Instance.Param)["RoiInheritIO"] = value;
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(text, MessageIcons.Error);
		}
	}

	public void ClearDataByRoi(bool isClearRoi = false)
	{
		pageBatchTrain.ClearTrainData();
		((UIElement)(object)gbBatchTrain).IsEnabled = false;
		if (isClearRoi)
		{
			pageTrainRegion.ClearRoi();
		}
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		TrainDataMgr.Instance.SetWindowShadowEvent += SetWindowShadow;
		if (CurModelType == ModelType.Create)
		{
			CurTrainStep = (ModelTrainStep)0;
		}
		else
		{
			CurTrainStep = (ModelTrainStep)2;
		}
		if (moduleTrainData == null)
		{
			return;
		}
		((UIElement)(object)gbBatchTrain).IsEnabled = ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData != null && moduleTrainData.IsBatchTrain;
		TrainDataMgr.Instance.IsStartTrain = false;
		TrainDataMgr.Instance.IsHasModel = false;
		if (CurModelType == ModelType.Create)
		{
			pageLocationModel.Init(moduleTrainData);
			pageTrainRegion.Init(moduleTrainData);
			if ((((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData != null && ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData.Count > 0) || moduleTrainData._matchModelData != null)
			{
				TrainDataMgr.Instance.IsHasModel = true;
			}
		}
		pageBatchTrain.Init(moduleTrainData);
	}

	public void ShowProgressDialog(string msg)
	{
		Dispatcher.Invoke(() =>
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Expected Obj, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Expected Obj, but got Unknown
			_isCloseDialog = false;
			windowShadow.Visibility = Visibility.Visible;
			_progressContext = new ProgressDialogContext(msg);
			_progressContext.Percent = 10;
			_progressDialog = new ProgressDialog();
			((FrameworkElement)(object)_progressDialog).DataContext = _progressContext;
			((Window)(object)_progressDialog).Activate();
			((Window)(object)_progressDialog).Topmost = true;
			((Window)(object)_progressDialog).Show();
		});
	}

	public void CloseProgressDialog()
	{
		Dispatcher.Invoke(() =>
		{
			if (_progressDialog != null)
			{
				_progressContext.Percent = 100;
				((Window)(object)_progressDialog).Close();
				_progressDialog = null;
				_progressContext = null;
			}
			if (windowShadow.Visibility != Visibility.Collapsed)
			{
				windowShadow.Visibility = Visibility.Collapsed;
			}
			if (!_isCloseDialog)
			{
				_isCloseDialog = true;
			}
		});
	}

	public void SetProgressValue(int value)
	{
		Dispatcher.Invoke(() =>
		{
			if (_progressContext != null)
			{
				_progressContext.Percent = value;
			}
		});
	}

	private void DeInit()
	{
		TrainDataMgr.Instance.SetWindowShadowEvent -= SetWindowShadow;
		if (roiIO != null)
		{
			roiIO.RelatedIOChanged -= RelatedIOChanged;
		}
		if (maskIO != null)
		{
			maskIO.RelatedIOChanged -= RelatedIOChanged;
		}
		TrainDataMgr.Instance.TrainData = new ModuleTrainData();
		TrainDataMgr.Instance.TrainData.IsSetTrainRegion = ((UIElement)(object)gbTrainRegion).IsEnabled;
		TrainDataMgr.Instance.TrainData.IsBatchTrain = ((UIElement)(object)gbBatchTrain).IsEnabled;
		if (CurModelType == ModelType.Create)
		{
			pageLocationModel.DeInit(TrainDataMgr.Instance.TrainData);
			pageTrainRegion.DeInit(TrainDataMgr.Instance.TrainData);
		}
		pageBatchTrain.DeInit(TrainDataMgr.Instance.TrainData);
		DataContext = null;
	}

	private void SetWindowShadow(bool bVisible)
	{
		Dispatcher.Invoke(() =>
		{
			if (bVisible)
			{
				windowShadow.Visibility = Visibility.Visible;
				_isCloseDialog = false;
			}
			else
			{
				windowShadow.Visibility = Visibility.Collapsed;
				_isCloseDialog = true;
			}
		});
	}

	private void ModifyCurStep(GuideButton guideButton)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected I4, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected I4, but got Unknown
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		if (guideButton.ModelTrainStep == CurTrainStep)
		{
			return;
		}
		ModelTrainStep curTrainStep = CurTrainStep;
		switch ((int)curTrainStep)
		{
		case 0:
			SetSkipHpMatch();
			gbLocationModel.IsCurrentStep = false;
			break;
		case 1:
			gbTrainRegion.IsCurrentStep = false;
			break;
		case 2:
			gbBatchTrain.IsCurrentStep = false;
			break;
		}
		guideButton.IsCurrentStep = true;
		CurTrainStep = guideButton.ModelTrainStep;
		ModelTrainStep curTrainStep2 = CurTrainStep;
		switch ((int)curTrainStep2)
		{
		case 0:
			btnNextStep.IsEnabled = true;
			break;
		case 1:
			if (pageLocationModel.CurImage != null && pageTrainRegion.imageView.ImageDataSource == null)
			{
				pageTrainRegion.imageView.ImageDataSource = pageLocationModel.CurImage;
				pageTrainRegion.imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(pageLocationModel.CurImage.ImageBuffer, pageLocationModel.CurImage.Width, pageLocationModel.CurImage.Height, pageLocationModel.CurImage.PixelFormat);
				pageTrainRegion.InitRoi();
			}
			if (pageLocationModel.MatchBox.width > 0f && pageLocationModel.MatchBox.height > 0f && TrainDataMgr.Instance.IsHasModel)
			{
				pageTrainRegion.DrawMatchBox(pageLocationModel.MatchBox);
			}
			if (pageTrainRegion.imageView.ImageDataSource == null)
			{
				btnNextStep.IsEnabled = false;
				((UIElement)(object)gbBatchTrain).IsEnabled = false;
			}
			break;
		case 2:
			if (pageLocationModel.CurImage != null && pageTrainRegion.imageView.ImageDataSource == null)
			{
				pageTrainRegion.imageView.ImageDataSource = pageLocationModel.CurImage;
				pageTrainRegion.imageView.Source = ModuleCommonMethodMgr.ImageTool.BuiltImageSource(pageLocationModel.CurImage.ImageBuffer, pageLocationModel.CurImage.Width, pageLocationModel.CurImage.Height, pageLocationModel.CurImage.PixelFormat);
				pageTrainRegion.InitRoi();
			}
			if (!TrainDataMgr.Instance.IsStartTrain)
			{
				pageTrainRegion.SetRoiData();
				pageBatchTrain.NeedUpdateDisplay();
			}
			break;
		}
	}

	private void SetSkipHpMatch()
	{
		if (TrainDataMgr.Instance.Param == null)
		{
			return;
		}
		try
		{
			((IUserStringData)TrainDataMgr.Instance.Param)["SkipHpMatch"] = (TrainDataMgr.Instance.IsHasModel ? "False" : "True");
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			LogHelper.moduleLog.Error($"SetSkipHpMatch error, {text}");
		}
	}
}
