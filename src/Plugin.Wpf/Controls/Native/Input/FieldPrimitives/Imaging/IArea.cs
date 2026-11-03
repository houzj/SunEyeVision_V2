using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Imaging;

public interface IArea
{
	Control Container { get; set; }

	Point CurPoint { get; set; }

	bool IsPressed { get; set; }

	void OnScaleChange(object sender, object Tag);
}
