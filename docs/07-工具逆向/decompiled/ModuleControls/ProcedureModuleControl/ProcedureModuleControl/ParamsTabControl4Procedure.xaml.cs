using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Apps.PcUI;
using DynamicObjectConfigControl;
using VM.Controls;
using VMControls.BaseInterface;
using VMControls.WPF;

namespace ProcedureModuleControl;

public partial class ParamsTabControl4Procedure : TabWinBase, IComponentConnector
{
	internal DynamicParamsTabControl dynamicTab;

	public override IParamsConfig ParamsConfig
	{
		get
		{
			return dynamicTab.ParamsConfig;
		}
		set
		{
			dynamicTab.ParamsConfig = value;
		}
	}

	public ParamsTabControl4Procedure()
	{
		InitializeComponent();
		SetBinding(FrameworkElement.WidthProperty, new Binding("AlgorithmTabRoot.ConfigWindowWidth")
		{
			Source = dynamicTab,
			Mode = BindingMode.TwoWay
		});
		SetBinding(FrameworkElement.HeightProperty, new Binding("AlgorithmTabRoot.ConfigWindowHeight")
		{
			Source = dynamicTab,
			Mode = BindingMode.TwoWay
		});
		Binding binding = new Binding("AlgorithmTabRoot.ConfigMaxButtonVisibility")
		{
			Converter = new BooleanToVisibilityConverter(),
			Source = dynamicTab,
			Mode = BindingMode.TwoWay
		};
		SetBinding(BaseWindow.MaxButtonVisibilityProperty, binding);
		Loaded += Window_Loaded;
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
		float num = 96f / graphics.DpiX;
		float num2 = 96f / graphics.DpiY;
		WpfScreen screenFrom = WpfScreen.GetScreenFrom(this);
		Left = (screenFrom.WorkingArea.Width * (double)num - ActualWidth) / 2.0 + screenFrom.WorkingArea.Left * (double)num;
		if (dynamicTab.AlgorithmTabRoot != null)
		{
			Top = (screenFrom.WorkingArea.Height * (double)num2 - (double)dynamicTab.AlgorithmTabRoot.ConfigWindowHeight) / 2.0 + screenFrom.WorkingArea.Top * (double)num2;
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		dynamicTab.Dispose();
		base.OnClosing(e);
	}
}
