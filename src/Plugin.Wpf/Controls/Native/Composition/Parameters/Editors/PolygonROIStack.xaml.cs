using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public partial class PolygonROIStack : ROIStackControl, IComponentConnector
{
	public PolygonROIStack()
	{
		InitializeComponent();
	}
}
