using System.Windows.Markup;
using VMControls.WPF;

namespace ImageSourceModuleControl;

public partial class ImageListPanel : ImageListPanelBase, IComponentConnector, IStyleConnector
{
	public ImageListPanel()
	{
		InitializeComponent();
	}

	protected override void ChangeShadowDataSource(object source)
	{
		windowShadow.DataContext = source;
	}
}
