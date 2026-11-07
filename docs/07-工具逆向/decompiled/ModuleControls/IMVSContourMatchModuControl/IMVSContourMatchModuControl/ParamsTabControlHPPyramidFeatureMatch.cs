using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using VM.Controls;
using VM.ModuleControl;
using VMControls.BaseInterface;
using VMControls.WPF;

namespace IMVSContourMatchModuControl;

public class ParamsTabControlHPPyramidFeatureMatch : ParamsTabControl4FeatureMatch, IComponentConnector
{
	public static readonly DependencyProperty IsShowProgressMaskProperty = DependencyProperty.Register("IsShowProgressMask", typeof(bool), typeof(ParamsTabControlHPPyramidFeatureMatch), new PropertyMetadata(false));

	internal ParamsTabControlHPPyramidFeatureMatch win;

	internal Image img;

	internal ParamsConfigControlBase paramstab;

	private bool _contentLoaded;

	public override IParamsConfig ParamsConfig
	{
		get
		{
			return paramstab.ParamsConfig;
		}
		set
		{
			paramstab.ParamsConfig = value;
		}
	}

	public override bool IsShowProgressMask
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsShowProgressMaskProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsShowProgressMaskProperty, (object)value);
		}
	}

	public ParamsTabControlHPPyramidFeatureMatch()
	{
		InitializeComponent();
		((FrameworkElement)this).SetBinding(FrameworkElement.WidthProperty, (BindingBase)new Binding("DataContext.ConfigWindowWidth")
		{
			Source = paramstab
		});
		((FrameworkElement)this).SetBinding(FrameworkElement.HeightProperty, (BindingBase)new Binding("DataContext.ConfigWindowHeight")
		{
			Source = paramstab
		});
		Binding binding = new Binding("DataContext.ConfigMaxButtonVisibility")
		{
			Converter = new BooleanToVisibilityConverter(),
			Source = paramstab
		};
		((FrameworkElement)this).SetBinding(BaseWindow.MaxButtonVisibilityProperty, (BindingBase)binding);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/IMVSContourMatchModuControl;component/paramstabcontrolhppyramidfeaturematch.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			win = (ParamsTabControlHPPyramidFeatureMatch)target;
			break;
		case 2:
			img = (Image)target;
			break;
		case 3:
			paramstab = (ParamsConfigControlBase)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
