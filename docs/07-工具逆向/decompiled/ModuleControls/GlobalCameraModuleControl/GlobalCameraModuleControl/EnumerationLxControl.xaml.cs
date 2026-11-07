using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.XmlParser.ParamTab;
using VM.PlatformSDKCS;

namespace GlobalCameraModuleControl;

public partial class EnumerationLxControl : UserControl, IComponentConnector
{
	public EnumerationLxControl()
	{
		InitializeComponent();
		myUserControl.Loaded += EnumerationLxControl_Loaded;
	}

	private void EnumerationLxControl_Loaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is EnumerationLx)
		{
			ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM += (DataContext as EnumerationLx).ServerManager_OnReportDataToVM;
		}
	}

	private void EnumerationLxControl_Unloaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is EnumerationLx)
		{
			ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM -= (DataContext as EnumerationLx).ServerManager_OnReportDataToVM;
		}
	}
}
