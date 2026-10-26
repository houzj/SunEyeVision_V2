using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class AreaLeftTopRect : Area, IComponentConnector
{
	protected override int AngleOffset => 45;

	public AreaLeftTopRect()
	{
		InitializeComponent();
	}
}
