using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class RightRotateArea : RotateArea, IComponentConnector
{
	protected override int AngleOffset => 45;

	public RightRotateArea()
	{
		InitializeComponent();
	}
}
