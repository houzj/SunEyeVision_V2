using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace IMVSMarkInspModuVAControl;

public partial class OCRModelTrainDialog : BaseWindow, INotifyPropertyChanged, IComponentConnector
{
	private ProgressDialogContext _progressContext = null;

	private ProgressDialog _progressDialog = null;

	private bool _isCloseDialog = true;

	public static readonly DependencyProperty CurTrainStepProperty = DependencyProperty.Register("CurTrainStep", typeof(ModelTrainStep), typeof(OCRModelTrainDialog), new PropertyMetadata((object)(ModelTrainStep)0));

	public static readonly DependencyProperty IsFinishLocationModelProperty = DependencyProperty.Register("IsFinishLocationModel", typeof(bool), typeof(OCRModelTrainDialog), new PropertyMetadata(false));

	internal GuideButton gbLocationModel;

	internal GuideButton gbDetectTarget;

	internal GuideButton gbFinePositioning;

	internal GuideButton gbAddMask;

	internal GuideButton gbStatisticsTrain;

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

	public bool IsFinishLocationModel
	{
		get
		{
			return (bool)GetValue(IsFinishLocationModelProperty);
		}
		set
		{
			SetValue(IsFinishLocationModelProperty, value);
		}
	}

	public double LastStepWidth => OCRTrainDataMgr.Instance.IsEnglish ? 90 : 66;

	public bool NextEnable
	{
		get
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Invalid comparison between Unknown and I4
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Invalid comparison between Unknown and I4
			if ((int)CurTrainStep == 0)
			{
				if (!pageLocationModel.IsGenerateModel)
				{
					return false;
				}
			}
			else if ((int)CurTrainStep == 2 && !pageTrainTarget.IsGetFeatureModel)
			{
				return false;
			}
			return true;
		}
	}

	public OCRModelTrainDialog()
	{
		InitializeComponent();
	}

	private void NextStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected I4, but got Unknown
		OCRTrainDataMgr.Instance.IsNoRecord = true;
		ModelTrainStep curTrainStep = CurTrainStep;
		switch ((int)curTrainStep)
		{
		case 0:
			((UIElement)(object)gbDetectTarget).IsEnabled = true;
			gbDetectTarget.IsCurrentStep = true;
			gbLocationModel.IsCurrentStep = false;
			if (pageLocationModel.CurModelType == RoughModelType.Create)
			{
				pageTrainTarget.CurImage = pageLocationModel.CreateImage;
			}
			else
			{
				pageTrainTarget.CurImage = pageLocationModel.InheritImage;
			}
			CurTrainStep = (ModelTrainStep)1;
			pageTrainTarget.UpdateInfo();
			break;
		case 1:
			pageTrainTarget.SaveInfo();
			((UIElement)(object)gbFinePositioning).IsEnabled = true;
			gbFinePositioning.IsCurrentStep = true;
			gbDetectTarget.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)2;
			pageTrainTarget.UpdateInfo();
			UpdateNextStep();
			break;
		case 2:
			pageTrainTarget.SaveInfo();
			((UIElement)(object)gbAddMask).IsEnabled = true;
			gbAddMask.IsCurrentStep = true;
			gbFinePositioning.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)3;
			pageTrainTarget.UpdateInfo();
			break;
		case 3:
			pageTrainTarget.SaveInfo();
			pageStatisticsTrain.ModelProcess(bDelete: true);
			if (pageStatisticsTrain.ImageList.Count > 0)
			{
				pageStatisticsTrain.NextImageProcess();
			}
			((UIElement)(object)gbStatisticsTrain).IsEnabled = true;
			gbStatisticsTrain.IsCurrentStep = true;
			gbAddMask.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)4;
			break;
		}
		OCRTrainDataMgr.Instance.IsNoRecord = false;
	}

	private void LastStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected I4, but got Unknown
		OCRTrainDataMgr.Instance.IsNoRecord = true;
		ModelTrainStep curTrainStep = CurTrainStep;
		switch (curTrainStep - 1)
		{
		case 0:
			pageTrainTarget.SaveInfo();
			gbLocationModel.IsCurrentStep = true;
			gbDetectTarget.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)0;
			break;
		case 1:
			pageTrainTarget.SaveInfo();
			gbDetectTarget.IsCurrentStep = true;
			gbFinePositioning.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)1;
			pageTrainTarget.UpdateInfo();
			UpdateNextStep();
			break;
		case 2:
			pageTrainTarget.SaveInfo();
			gbFinePositioning.IsCurrentStep = true;
			gbAddMask.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)2;
			pageTrainTarget.UpdateInfo();
			break;
		case 3:
			gbAddMask.IsCurrentStep = true;
			gbStatisticsTrain.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)3;
			pageTrainTarget.UpdateInfo();
			break;
		}
		OCRTrainDataMgr.Instance.IsNoRecord = false;
	}

	private void TrainFinish_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)["TrainStop"] = "";
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("Lang_StopTrainErr") + "\r\n(";
			text += AppLocalizationService.GetLang("errInfo:");
			text += ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			text += ")";
			LogHelper.moduleLog.Error(text);
			HikMessageBox.Show(text, MessageIcons.Error);
		}
		DialogResult = true;
		Close();
	}

	private void GuideButton_OnClick(object sender, RoutedEventArgs e)
	{
		GuideButton val = (GuideButton)((sender is GuideButton) ? sender : null);
		if (val != null)
		{
			OCRTrainDataMgr.Instance.IsNoRecord = true;
			ModifyCurStep(val);
			OCRTrainDataMgr.Instance.IsNoRecord = false;
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

	public void ClearAllData()
	{
		pageStatisticsTrain.ClearData();
		pageTrainTarget.ClearData();
		OCRTrainDataMgr.Instance.OnClearModelEvent();
		OCRTrainDataMgr.Instance.OnEmptyModelEvent();
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		OCRTrainDataMgr.Instance.SetWindowShadowEvent += SetWindowShadow;
		OCRTrainDataMgr.Instance.ShowProgressWindowEvent += ShowProgressWindow;
		OCRTrainDataMgr.Instance.CloseProgressWindowEvent += CloseProgressWindow;
		CurTrainStep = (ModelTrainStep)0;
		pageLocationModel.Init(moduleTrainData);
		if (pageLocationModel.CurModelType == RoughModelType.Create)
		{
			pageTrainTarget.CurImage = pageLocationModel.CreateImage;
		}
		else
		{
			pageTrainTarget.CurImage = pageLocationModel.InheritImage;
		}
		pageTrainTarget.Init(moduleTrainData);
		pageStatisticsTrain.Init(moduleTrainData);
	}

	public void UpdateNextStep()
	{
		OnPropertyChanged("NextEnable");
	}

	private void DeInit()
	{
		OCRTrainDataMgr.Instance.SetWindowShadowEvent -= SetWindowShadow;
		OCRTrainDataMgr.Instance.ShowProgressWindowEvent -= ShowProgressWindow;
		OCRTrainDataMgr.Instance.CloseProgressWindowEvent -= CloseProgressWindow;
		OCRTrainDataMgr.Instance.TrainData = new ModuleTrainData();
		pageLocationModel.DeInit(OCRTrainDataMgr.Instance.TrainData);
		pageTrainTarget.DeInit(OCRTrainDataMgr.Instance.TrainData);
		pageStatisticsTrain.DeInit(OCRTrainDataMgr.Instance.TrainData);
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

	private void ShowProgressWindow()
	{
		Dispatcher.Invoke(() =>
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Expected Obj, but got Unknown
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Expected Obj, but got Unknown
			_isCloseDialog = false;
			_progressContext = new ProgressDialogContext("正在加载图片");
			_progressContext.IsShowProgressBar = false;
			_progressDialog = new ProgressDialog();
			((FrameworkElement)(object)_progressDialog).DataContext = _progressContext;
			((Window)(object)_progressDialog).Activate();
			((Window)(object)_progressDialog).Topmost = true;
			((Window)(object)_progressDialog).Show();
		});
	}

	private void CloseProgressWindow()
	{
		Dispatcher.Invoke(() =>
		{
			if (_progressDialog != null)
			{
				((Window)(object)_progressDialog).Close();
				_progressDialog = null;
				_progressContext = null;
			}
			if (!_isCloseDialog)
			{
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
		//IL_003a: Expected I4, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected I4, but got Unknown
		if (guideButton.ModelTrainStep == CurTrainStep)
		{
			return;
		}
		ModelTrainStep curTrainStep = CurTrainStep;
		switch ((int)curTrainStep)
		{
		case 0:
			gbLocationModel.IsCurrentStep = false;
			break;
		case 1:
			pageTrainTarget.SaveInfo();
			gbDetectTarget.IsCurrentStep = false;
			break;
		case 2:
			pageTrainTarget.SaveInfo();
			gbFinePositioning.IsCurrentStep = false;
			break;
		case 3:
			pageTrainTarget.SaveInfo();
			gbAddMask.IsCurrentStep = false;
			break;
		case 4:
			gbStatisticsTrain.IsCurrentStep = false;
			break;
		}
		guideButton.IsCurrentStep = true;
		CurTrainStep = guideButton.ModelTrainStep;
		ModelTrainStep curTrainStep2 = CurTrainStep;
		switch ((int)curTrainStep2)
		{
		case 0:
			pageLocationModel.imageViewBox.ImageDataSource = null;
			pageLocationModel.ShowImage();
			UpdateNextStep();
			break;
		case 1:
		case 2:
			pageTrainTarget.UpdateInfo();
			UpdateNextStep();
			break;
		case 3:
			pageTrainTarget.UpdateInfo();
			break;
		case 4:
			pageStatisticsTrain.ModelProcess(bDelete: true);
			if (pageStatisticsTrain.ImageList.Count > 0)
			{
				pageStatisticsTrain.NextImageProcess();
			}
			break;
		}
	}
}
