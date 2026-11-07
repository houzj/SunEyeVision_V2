using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaRightTopRect : Area, IComponentConnector
{
	protected override int AngleOffset => -45;

	public AreaRightTopRect()
	{
		InitializeComponent();
	}
}
