using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class AreaLeftTop : Area, IComponentConnector
{
	protected override int AngleOffset => 45;

	public AreaLeftTop()
	{
		InitializeComponent();
	}
}
