using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Apps.PcUI;
using VM.Controls.Utility;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.WPF;

namespace SolutionModuleControl;

public partial class ParamsTabControl4Solution : TabWinBase, IComponentConnector
{
	private DelegateCommand _openHelpCommand;

	public override IParamsConfig ParamsConfig
	{
		get
		{
			return paramsTab.ParamsConfig;
		}
		set
		{
			paramsTab.ParamsConfig = value;
		}
	}

	public DelegateCommand OpenHelpCommand
	{
		get
		{
			if (_openHelpCommand == null)
			{
				_openHelpCommand = new DelegateCommand(ExecuteOpenHelp);
			}
			return _openHelpCommand;
		}
	}

	public ParamsTabControl4Solution()
	{
		InitializeComponent();
		Loaded += Window_Loaded;
	}

	private void ExecuteOpenHelp()
	{
		string toolItemInfoPath = "";
		if (ParamsConfig != null)
		{
			toolItemInfoPath = $"{Path.GetDirectoryName(ParamsConfig.XmlPath)}\\ToolItemInfo.xml";
		}
		HelpDocumentUtil.OpenHelpDocument(toolItemInfoPath);
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
		float num = 96f / graphics.DpiX;
		_ = 96f / graphics.DpiY;
		WpfScreen screenFrom = WpfScreen.GetScreenFrom(this);
		Left = (screenFrom.WorkingArea.Width * (double)num - ActualWidth) / 2.0 + screenFrom.WorkingArea.Left * (double)num;
	}
}
