using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.VisionMaster.ParamTab;

public partial class ParamsTabControl : BaseWindow, IComponentConnector
{
	public static bool IsAlive;

	public bool ShowImage { get; set; }

	public ParamsTabControl()
	{
		InitializeComponent();
		Owner = Application.Current.MainWindow;
	}

	protected override void OnLoaded()
	{
		base.OnLoaded();
		if (!IsAlive)
		{
			IsAlive = true;
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		bool flag = false;
		Window lastWindow = BaseWindow.LastWindow;
		if (lastWindow != null)
		{
			if (!lastWindow.Topmost)
			{
				lastWindow.Topmost = true;
			}
			lastWindow.Activate();
		}
		if (!IsAlive)
		{
			IsAlive = false;
		}
	}

	private void BaseWindow_Loaded(object sender, RoutedEventArgs e)
	{
	}

	private void BaseWindow_Closing(object sender, CancelEventArgs e)
	{
	}

}
