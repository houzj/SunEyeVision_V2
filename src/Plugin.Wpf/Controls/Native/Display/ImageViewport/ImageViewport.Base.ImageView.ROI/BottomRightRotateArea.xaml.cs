using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

public partial class BottomRightRotateArea : RotateArea, IComponentConnector
{
	protected override int AngleOffset => 90;

	public BottomRightRotateArea()
	{
		InitializeComponent();
	}
}
