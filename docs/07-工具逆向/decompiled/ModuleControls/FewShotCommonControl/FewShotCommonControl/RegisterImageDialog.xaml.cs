using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using VM.Controls;
using VM.ModuleControl;

namespace FewShotCommonControl;

public partial class RegisterImageDialog : BaseWindow, IEdgeLearning, INotifyPropertyChanged, IComponentConnector
{
	private ProgressDialogContext _progressContext = null;

	private ProgressDialog _progressDialog = null;

	private bool _isCloseDialog = true;

	IList IEdgeLearning.Items => pageRegisterImage.CategoryList;

	public RegisterImageDialog()
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
		DeInit();
		DialogResult = true;
		base.OnClosing(e);
	}

	public void Init(ObservableCollection<CategoryData> categoryList)
	{
		EdgeLearningData.Instance.SetWindowShadowEvent += SetWindowShadow;
		pageRegisterImage.Init(categoryList);
	}

	public void ShowProgressDialog(string msg)
	{
		Dispatcher.Invoke(() =>
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Expected Obj, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Expected Obj, but got Unknown
			_isCloseDialog = false;
			windowShadow.Visibility = Visibility.Visible;
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

	private void DeInit()
	{
		EdgeLearningData.Instance.SetWindowShadowEvent -= SetWindowShadow;
		pageRegisterImage.DeInit();
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
}
