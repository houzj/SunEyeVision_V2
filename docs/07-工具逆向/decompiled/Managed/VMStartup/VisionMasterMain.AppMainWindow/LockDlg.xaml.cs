using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using VM.Controls;

namespace VisionMasterMain.AppMainWindow;

public partial class LockDlg : BaseWindow, INotifyPropertyChanged, IComponentConnector
{
	public static readonly DependencyProperty IsNotifyLockProperty = DependencyProperty.Register("IsNotifyLock", typeof(bool), typeof(LockDlg), new PropertyMetadata(false));

	public bool IsNotifyLock
	{
		get
		{
			return (bool)GetValue(IsNotifyLockProperty);
		}
		set
		{
			SetValue(IsNotifyLockProperty, value);
		}
	}

	public LockDlg(bool input)
	{
		InitializeComponent();
		IsNotifyLock = input;
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}

	private void btnNO_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}
}
