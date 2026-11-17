using System.Windows;
using System.Windows.Markup;
using VM.Controls;
using VM.Utility;

namespace VisionMasterMain.AppMainWindow;

public partial class CloseSetDlg : BaseWindow, IComponentConnector
{
	private const string conIsRunBackground = "IsRunBackground";

	private const string conIsNotifyRunBackground = "IsNotifyRunBackground";

	private bool _IsRunBackground;

	private bool _IsNotifyRunBackground;

	public bool IsRunBackground
	{
		get
		{
			return _IsRunBackground;
		}
		set
		{
			if (_IsRunBackground != value)
			{
				_IsRunBackground = value;
				OnPropertyChanged("IsRunBackground");
			}
		}
	}

	public bool IsNotifyRunBackground
	{
		get
		{
			return _IsNotifyRunBackground;
		}
		set
		{
			if (_IsNotifyRunBackground != value)
			{
				_IsNotifyRunBackground = value;
				OnPropertyChanged("IsNotifyRunBackground");
			}
		}
	}

	public CloseSetDlg()
	{
		InitializeComponent();
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		string defaultValue = (IsRunBackground ? "1" : "0");
		ConfigurationHelper.SaveConfig("IsRunBackground", defaultValue);
		string defaultValue2 = (IsNotifyRunBackground ? "1" : "0");
		ConfigurationHelper.SaveConfig("IsNotifyRunBackground", defaultValue2);
		DialogResult = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void dlgModify_Loaded(object sender, RoutedEventArgs e)
	{
		string config = ConfigurationHelper.GetConfig("IsRunBackground");
		IsRunBackground = config == "1";
		string config2 = ConfigurationHelper.GetConfig("IsNotifyRunBackground");
		IsNotifyRunBackground = config2 == "1";
	}
}
