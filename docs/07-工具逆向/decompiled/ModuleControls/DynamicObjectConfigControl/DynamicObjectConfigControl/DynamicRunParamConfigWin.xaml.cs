using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using Apps.PcUI.Common;
using VM.ModuleControl;

namespace DynamicObjectConfigControl;

public partial class DynamicRunParamConfigWin : ConfirmBaseWindow, IRelateControl, IComponentConnector
{
	private ProgressDialogContext _progressContext = null;

	private ProgressDialog _progressDialog = null;

	private bool _isCloseDialog = true;

	public DynamicRunParamConfigWin()
	{
		InitializeComponent();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!_isCloseDialog)
		{
			e.Cancel = true;
			return;
		}
		DialogResult = true;
		base.OnClosing(e);
	}

	public void ShowProgressDialog(string msg)
	{
		Dispatcher.Invoke(() =>
		{
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Expected Obj, but got Unknown
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Expected Obj, but got Unknown
			_isCloseDialog = false;
			windowShadow.Visibility = Visibility.Visible;
			if (_progressDialog != null)
			{
				((Window)(object)_progressDialog).Close();
				_progressDialog = null;
				_progressContext = null;
			}
			_progressContext = new ProgressDialogContext(msg);
			_progressContext.Percent = 10;
			_progressDialog = new ProgressDialog();
			((FrameworkElement)(object)_progressDialog).DataContext = _progressContext;
			((Window)(object)_progressDialog).Activate();
			((Window)(object)_progressDialog).Topmost = true;
			((Window)(object)_progressDialog).Show();
		});
	}

	public void CloseProgressDialog()
	{
		Dispatcher.Invoke(() =>
		{
			if (_progressDialog != null)
			{
				_progressContext.Percent = 100;
				((Window)(object)_progressDialog).Close();
				_progressDialog = null;
				_progressContext = null;
			}
			if (windowShadow.Visibility != Visibility.Collapsed)
			{
				windowShadow.Visibility = Visibility.Collapsed;
			}
			if (!_isCloseDialog)
			{
				_isCloseDialog = true;
			}
		});
	}

	public void SetProgressValue(int value)
	{
		Dispatcher.Invoke(() =>
		{
			if (_progressContext != null)
			{
				_progressContext.Percent = value;
			}
		});
	}
}
