using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class AreaLeft : Area, IComponentConnector
{
	protected override int AngleOffset => 0;

	public AreaLeft()
	{
		InitializeComponent();
	}
}
