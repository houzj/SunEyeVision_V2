using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaLeft : Area, IComponentConnector
{
	protected override int AngleOffset => 0;

	public AreaLeft()
	{
		InitializeComponent();
	}
}
