using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaTopRect : Area, IComponentConnector
{
	protected override int AngleOffset => 90;

	public AreaTopRect()
	{
		InitializeComponent();
	}
}
