using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaTop : Area, IComponentConnector
{
	protected override int AngleOffset => 90;

	public AreaTop()
	{
		InitializeComponent();
	}
}
