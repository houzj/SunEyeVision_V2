using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class BottomRightRotateArea : RotateArea, IComponentConnector
{
	protected override int AngleOffset => 90;

	public BottomRightRotateArea()
	{
		InitializeComponent();
	}
}
