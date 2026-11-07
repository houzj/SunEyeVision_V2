using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using VM.Controls;
using VM.ModuleControl;
using VMControls.BaseInterface;

namespace IMVSMarkInspModuControl;

public partial class OCRModelTrainDialog : BaseWindow, INotifyPropertyChanged, IComponentConnector
{
	private ProgressDialogContext _progressContext = null;

	private ProgressDialog _progressDialog = null;

	private bool _isCloseDialog = true;

	public static readonly DependencyProperty CurTrainStepProperty = DependencyProperty.Register("CurTrainStep", typeof(ModelTrainStep), typeof(OCRModelTrainDialog), new PropertyMetadata((object)(ModelTrainStep)0));

	public static readonly DependencyProperty IsFinishLocationModelProperty = DependencyProperty.Register("IsFinishLocationModel", typeof(bool), typeof(OCRModelTrainDialog), new PropertyMetadata(false));

	internal GuideButton gbLocationModel;

	internal GuideButton gbTrainRegion;

	internal GuideButton gbTrainTarget;

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

	public OCRModelTrainDialog()
	{
		InitializeComponent();
	}

	private void NextStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected I4, but got Unknown
		ModelTrainStep curTrainStep = CurTrainStep;
		switch ((int)curTrainStep)
		{
		case 0:
			((UIElement)(object)gbTrainRegion).IsEnabled = true;
			gbTrainRegion.IsCurrentStep = true;
			gbLocationModel.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)1;
			pageTrainRegion.imageView.ImageDataSource = pageLocationModel.CurImage;
			if (OCRTrainDataMgr.Instance.TrainTargetInit)
			{
				pageTrainRegion.SetRoiData();
				pageTrainTarget.Init();
				if (!pageTrainTarget.IsGetFeatureModel)
				{
					((UIElement)(object)gbStatisticsTrain).IsEnabled = false;
					pageStatisticsTrain.ClearStatData();
				}
				OCRTrainDataMgr.Instance.TrainTargetInit = false;
			}
			break;
		case 1:
			if ((!OCRTrainDataMgr.Instance.IsStartTrain && pageTrainRegion.imageView.Source != null) || OCRTrainDataMgr.Instance.TrainTargetInit)
			{
				pageTrainRegion.SetRoiData();
				if (OCRTrainDataMgr.Instance.TrainTargetInit)
				{
					pageTrainTarget.Init();
					if (!pageTrainTarget.IsGetFeatureModel)
					{
						((UIElement)(object)gbStatisticsTrain).IsEnabled = false;
						pageStatisticsTrain.ClearStatData();
					}
					OCRTrainDataMgr.Instance.TrainTargetInit = false;
				}
				else
				{
					pageTrainTarget.GetTargetImage();
					pageTrainTarget.Binarization();
				}
			}
			((UIElement)(object)gbTrainTarget).IsEnabled = true;
			gbTrainTarget.IsCurrentStep = true;
			gbTrainRegion.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)2;
			if (pageTrainTarget.IsGetFeatureModel)
			{
				btnNextStep.IsEnabled = true;
			}
			else
			{
				btnNextStep.IsEnabled = false;
			}
			break;
		case 2:
			pageStatisticsTrain.ModelProcess(bDelete: true);
			if (pageStatisticsTrain.ImageList.Count > 0)
			{
				pageStatisticsTrain.NextImageProcess();
			}
			((UIElement)(object)gbStatisticsTrain).IsEnabled = true;
			gbStatisticsTrain.IsCurrentStep = true;
			gbTrainTarget.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)3;
			break;
		}
	}

	private void LastStep_OnClick(object sender, RoutedEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected I4, but got Unknown
		ModelTrainStep curTrainStep = CurTrainStep;
		switch (curTrainStep - 1)
		{
		case 0:
			gbLocationModel.IsCurrentStep = true;
			gbTrainRegion.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)0;
			btnNextStep.IsEnabled = true;
			break;
		case 1:
			gbTrainRegion.IsCurrentStep = true;
			gbTrainTarget.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)1;
			btnNextStep.IsEnabled = true;
			break;
		case 2:
			gbTrainTarget.IsCurrentStep = true;
			gbStatisticsTrain.IsCurrentStep = false;
			CurTrainStep = (ModelTrainStep)2;
			break;
		}
	}

	private void TrainFinish_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			((IUserStringData)OCRTrainDataMgr.Instance.Param)["TrainStop"] = "";
		}
		catch
		{
		}
		DialogResult = true;
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

	public void ClearDataByRoi()
	{
		OCRTrainDataMgr.Instance.IsStartTrain = false;
		pageStatisticsTrain.ClearStatData();
		pageStatisticsTrain.CurImage = null;
		pageStatisticsTrain.MaskImage = null;
		((UIElement)(object)gbStatisticsTrain).IsEnabled = false;
		pageTrainTarget.BoxCovers.Clear();
		pageTrainTarget.imageView.ClearShapesE();
		pageTrainTarget.MaskImage = null;
		pageTrainTarget.IsGetBox = false;
		pageTrainTarget.IsGetFeatureModel = false;
		pageTrainTarget.ClearUnredoData();
		OCRTrainDataMgr.Instance.OnClearModelEvent();
		OCRTrainDataMgr.Instance.OnEmptyModelEvent();
		OCRTrainDataMgr.Instance.TrainTargetInit = false;
	}

	public void Init(ModuleTrainData moduleTrainData)
	{
		OCRTrainDataMgr.Instance.SetWindowShadowEvent += SetWindowShadow;
		OCRTrainDataMgr.Instance.ShowProgressWindowEvent += ShowProgressWindow;
		OCRTrainDataMgr.Instance.CloseProgressWindowEvent += CloseProgressWindow;
		CurTrainStep = (ModelTrainStep)0;
		OCRTrainDataMgr.Instance.TrainTargetInit = true;
		((UIElement)(object)gbTrainRegion).IsEnabled = moduleTrainData.IsSetTrainRegion;
		((UIElement)(object)gbTrainTarget).IsEnabled = moduleTrainData.IsSetTrainTarget;
		((UIElement)(object)gbStatisticsTrain).IsEnabled = moduleTrainData.IsStatisticsTrain;
		pageLocationModel.Init(moduleTrainData);
		pageTrainRegion.imageView.ImageDataSource = pageLocationModel.CurImage;
		pageTrainRegion.Init(moduleTrainData);
		if (!moduleTrainData.IsSetTrainTarget)
		{
			OCRTrainDataMgr.Instance.IsStartTrain = false;
		}
		pageStatisticsTrain.Init(moduleTrainData);
		if ((((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData != null && ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData.Count > 0) || ((UIElement)(object)gbTrainRegion).IsEnabled)
		{
			btnNextStep.IsEnabled = true;
		}
		else
		{
			btnNextStep.IsEnabled = false;
		}
	}

	private void DeInit()
	{
		OCRTrainDataMgr.Instance.SetWindowShadowEvent -= SetWindowShadow;
		OCRTrainDataMgr.Instance.ShowProgressWindowEvent -= ShowProgressWindow;
		OCRTrainDataMgr.Instance.CloseProgressWindowEvent -= CloseProgressWindow;
		pageTrainTarget.DeInit();
		OCRTrainDataMgr.Instance.TrainData = new ModuleTrainData();
		OCRTrainDataMgr.Instance.TrainData.IsSetTrainRegion = ((UIElement)(object)gbTrainRegion).IsEnabled;
		OCRTrainDataMgr.Instance.TrainData.IsSetTrainTarget = ((UIElement)(object)gbTrainTarget).IsEnabled;
		OCRTrainDataMgr.Instance.TrainData.IsStatisticsTrain = ((UIElement)(object)gbStatisticsTrain).IsEnabled;
		pageLocationModel.DeInit(OCRTrainDataMgr.Instance.TrainData);
		pageTrainRegion.DeInit(OCRTrainDataMgr.Instance.TrainData);
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
		//IL_0036: Expected I4, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected I4, but got Unknown
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
			gbTrainRegion.IsCurrentStep = false;
			break;
		case 2:
			gbTrainTarget.IsCurrentStep = false;
			break;
		case 3:
			gbStatisticsTrain.IsCurrentStep = false;
			break;
		}
		guideButton.IsCurrentStep = true;
		CurTrainStep = guideButton.ModelTrainStep;
		ModelTrainStep curTrainStep2 = CurTrainStep;
		switch ((int)curTrainStep2)
		{
		case 0:
		case 1:
			btnNextStep.IsEnabled = true;
			pageTrainRegion.imageView.ImageDataSource = pageLocationModel.CurImage;
			if (OCRTrainDataMgr.Instance.TrainTargetInit)
			{
				pageTrainRegion.SetRoiData();
				pageTrainTarget.Init();
				if (!pageTrainTarget.IsGetFeatureModel)
				{
					((UIElement)(object)gbStatisticsTrain).IsEnabled = false;
					pageStatisticsTrain.ClearStatData();
				}
				OCRTrainDataMgr.Instance.TrainTargetInit = false;
			}
			break;
		case 2:
			if ((!OCRTrainDataMgr.Instance.IsStartTrain && pageTrainRegion.imageView.Source != null) || OCRTrainDataMgr.Instance.TrainTargetInit)
			{
				pageTrainRegion.SetRoiData();
				if (OCRTrainDataMgr.Instance.TrainTargetInit)
				{
					pageTrainTarget.Init();
					if (!pageTrainTarget.IsGetFeatureModel)
					{
						((UIElement)(object)gbStatisticsTrain).IsEnabled = false;
						pageStatisticsTrain.ClearStatData();
					}
					OCRTrainDataMgr.Instance.TrainTargetInit = false;
				}
				else
				{
					pageTrainTarget.GetTargetImage();
					pageTrainTarget.Binarization();
				}
			}
			if (pageTrainTarget.IsGetFeatureModel)
			{
				btnNextStep.IsEnabled = true;
			}
			else
			{
				btnNextStep.IsEnabled = false;
			}
			break;
		case 3:
			if (OCRTrainDataMgr.Instance.TrainTargetInit)
			{
				pageTrainRegion.SetRoiData();
				pageTrainTarget.Init();
				OCRTrainDataMgr.Instance.TrainTargetInit = false;
			}
			pageStatisticsTrain.ModelProcess(bDelete: true);
			if (pageStatisticsTrain.ImageList.Count > 0)
			{
				pageStatisticsTrain.NextImageProcess();
			}
			break;
		}
	}
}
