using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaLeftTopRect : Area, IComponentConnector
{
	protected override int AngleOffset => 45;

	public AreaLeftTopRect()
	{
		InitializeComponent();
	}
}
