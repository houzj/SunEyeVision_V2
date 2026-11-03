using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

internal class DateTimeInfo
{
	public string Content { get; set; }

	public string Format { get; set; }

	public bool IsReadOnly { get; set; }

	public int Length { get; set; }

	public int StartPosition { get; set; }

	public DateTimePart Type { get; set; }
}
