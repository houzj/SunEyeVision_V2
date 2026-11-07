using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaRightTop : Area, IComponentConnector
{
	protected override int AngleOffset => -45;

	public AreaRightTop()
	{
		InitializeComponent();
	}
}
