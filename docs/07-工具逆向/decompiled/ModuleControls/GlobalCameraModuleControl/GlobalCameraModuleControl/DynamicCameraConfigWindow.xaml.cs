using System.ComponentModel;
using System.Windows.Markup;
using Apps.PcUI.Common;

namespace GlobalCameraModuleControl;

public partial class DynamicCameraConfigWindow : ConfirmBaseWindow, IComponentConnector
{
	private bool _isCloseDialog = true;

	public DynamicCameraConfigWindow()
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
}
