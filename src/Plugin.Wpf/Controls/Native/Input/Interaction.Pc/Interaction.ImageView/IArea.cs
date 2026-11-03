using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.Interaction.ImageView;

public interface IArea
{
	Control Container { get; set; }

	Point CurPoint { get; set; }

	bool IsPressed { get; set; }

	void OnScaleChange(object sender, object Tag);
}
