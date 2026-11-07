using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class BottomLeftRotateArea : RotateArea, IComponentConnector
{
	protected override int AngleOffset => 180;

	public BottomLeftRotateArea()
	{
		InitializeComponent();
	}
}
