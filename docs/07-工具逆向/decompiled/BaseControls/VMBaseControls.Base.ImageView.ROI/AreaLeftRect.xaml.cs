using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaLeftRect : Area, IComponentConnector
{
	protected override int AngleOffset => 0;

	public AreaLeftRect()
	{
		InitializeComponent();
	}
}
