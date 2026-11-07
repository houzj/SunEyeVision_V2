using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.WPF;

namespace DynamicObjectConfigControl;

public partial class DynamicParamsTabControl : Control, INotifyPropertyChanged, IComponentConnector
{
	private AlgorithmTabRoot algorithmTabRoot = null;

	private IDecorate iamgeDecorate;

	private ParamsConfigControlBase paramsConfigControl;

	private ContinuousExecuteBottomCommand continuousExecuteBottom;

	private StopExecuteBottomCommand stopExecuteBottom;

	private ExecuteOnceBottomCommand executeOnceBottom;

	private BottomCommand confirmBottom;

	public static readonly DependencyProperty ParamsConfigProperty = DependencyProperty.Register("ParamsConfig", typeof(IParamsConfig), typeof(DynamicParamsTabControl), new PropertyMetadata(null));

	public static readonly DependencyProperty ROIPartUIProperty = DependencyProperty.Register("ROIPartUI", typeof(object), typeof(DynamicParamsTabControl), new PropertyMetadata(null));

	public static readonly DependencyProperty IsExecuteProperty = DependencyProperty.Register("IsExecute", typeof(bool), typeof(DynamicParamsTabControl), new PropertyMetadata(true));

	public static readonly DependencyProperty IsEditStateProperty = DependencyProperty.Register("IsEditState", typeof(bool), typeof(DynamicParamsTabControl), new PropertyMetadata(true));

	public IParamsConfig ParamsConfig
	{
		get
		{
			return (IParamsConfig)GetValue(ParamsConfigProperty);
		}
		set
		{
			SetValue(ParamsConfigProperty, value);
		}
	}

	public object ROIPartUI
	{
		get
		{
			return GetValue(ROIPartUIProperty);
		}
		set
		{
			SetValue(ROIPartUIProperty, value);
		}
	}

	public bool IsExecute
	{
		get
		{
			return (bool)GetValue(IsExecuteProperty);
		}
		set
		{
			SetValue(IsExecuteProperty, value);
		}
	}

	public bool IsEditState
	{
		get
		{
			return (bool)GetValue(IsEditStateProperty);
		}
		set
		{
			SetValue(IsEditStateProperty, value);
		}
	}

	public IDecorate ImageDecorate
	{
		get
		{
			return iamgeDecorate;
		}
		set
		{
			if (value != iamgeDecorate)
			{
				iamgeDecorate = value;
				OnPropertyChanged("ImageDecorate");
			}
		}
	}

	public AlgorithmTabRoot AlgorithmTabRoot
	{
		get
		{
			return algorithmTabRoot;
		}
		set
		{
			if (algorithmTabRoot == value)
			{
				return;
			}
			algorithmTabRoot = value;
			OnPropertyChanged("AlgorithmTabRoot");
			if (algorithmTabRoot != null)
			{
				ContinuousExecuteBottom?.SetParentNode(algorithmTabRoot);
				ContinuousExecuteBottom?.XmlLoadedTask();
				StopExecuteBottom?.SetParentNode(algorithmTabRoot);
				StopExecuteBottom?.XmlLoadedTask();
				ExecuteOnceBottom?.SetParentNode(algorithmTabRoot);
				ExecuteOnceBottom?.XmlLoadedTask();
				ConfirmBottom?.SetParentNode(algorithmTabRoot);
				ConfirmBottom?.XmlLoadedTask();
				if (algorithmTabRoot.FindBaseNodeByName("Tab_Run Params") is GroupRunParam groupRunParam)
				{
					ImageDecorate = new ModuleDecorate
					{
						DecorateSource = groupRunParam
					};
					groupRunParam.ClearDecorateEvent();
					groupRunParam.UpdateDecorateEvent += UpdateDecorate;
				}
			}
		}
	}

	public ContinuousExecuteBottomCommand ContinuousExecuteBottom
	{
		get
		{
			if (continuousExecuteBottom == null)
			{
				continuousExecuteBottom = new ContinuousExecuteBottomCommand();
				continuousExecuteBottom.DisplayNameA = "Lang_MutiRun";
				Apps.XmlParser.ParamTab.Setter setter = new Apps.XmlParser.ParamTab.Setter
				{
					OperationName = "ExcuteMethodOperation",
					OperationParams = "ContinuousSelfExcuteModule"
				};
				setter.SetParentNode(continuousExecuteBottom);
				continuousExecuteBottom.CommandSetterA = setter;
			}
			return continuousExecuteBottom;
		}
	}

	public StopExecuteBottomCommand StopExecuteBottom
	{
		get
		{
			if (stopExecuteBottom == null)
			{
				stopExecuteBottom = new StopExecuteBottomCommand();
				stopExecuteBottom.DisplayNameA = "Lang_StopRun";
				Apps.XmlParser.ParamTab.Setter setter = new Apps.XmlParser.ParamTab.Setter
				{
					OperationName = "ExcuteMethodOperation",
					OperationParams = "StopSelfExcuteModule"
				};
				setter.SetParentNode(stopExecuteBottom);
				stopExecuteBottom.CommandSetterA = setter;
			}
			return stopExecuteBottom;
		}
	}

	public ExecuteOnceBottomCommand ExecuteOnceBottom
	{
		get
		{
			if (executeOnceBottom == null)
			{
				executeOnceBottom = new ExecuteOnceBottomCommand();
				executeOnceBottom.DisplayNameA = "Execute";
				Apps.XmlParser.ParamTab.Setter setter = new Apps.XmlParser.ParamTab.Setter
				{
					OperationName = "ExcuteMethodOperation",
					OperationParams = "SelfExcuteModule"
				};
				setter.SetParentNode(executeOnceBottom);
				executeOnceBottom.CommandSetterA = setter;
			}
			return executeOnceBottom;
		}
	}

	public BottomCommand ConfirmBottom
	{
		get
		{
			if (confirmBottom == null)
			{
				confirmBottom = new BottomCommand();
				confirmBottom.DisplayNameA = "Confirm";
				Apps.XmlParser.ParamTab.Setter setter = new Apps.XmlParser.ParamTab.Setter
				{
					OperationName = "ExcuteMethodOperation",
					OperationParams = "CloseParamsTabControl"
				};
				setter.SetParentNode(confirmBottom);
				confirmBottom.CommandSetterA = setter;
			}
			return confirmBottom;
		}
	}

	public double FirstColumnWidth => (AppLocalizationService.GetLang("Language") == "English") ? 680 : 430;

	public event PropertyChangedEventHandler PropertyChanged;

	public DynamicParamsTabControl()
	{
		InitializeComponent();
	}

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		paramsConfigControl = Template.FindName("paramsTab", this) as ParamsConfigControlBase;
		AlgorithmTabRoot = paramsConfigControl.DataContext as AlgorithmTabRoot;
	}

	public void Dispose()
	{
		ImageDecorate = null;
	}

	private void UpdateDecorate(IDecorate decorate)
	{
		ImageDecorate = decorate;
	}
}
