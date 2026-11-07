using System.Windows.Markup;

namespace VMBaseControls.Base.ImageView.ROI;

public partial class TopLeftRotateArea : RotateArea, IComponentConnector
{
	protected override int AngleOffset => 270;

	public TopLeftRotateArea()
	{
		InitializeComponent();
	}
}
