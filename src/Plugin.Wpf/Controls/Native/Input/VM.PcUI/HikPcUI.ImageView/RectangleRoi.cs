using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ImageView;

public interface RectangleRoi
{
	void Doth(FrameworkElement rectangleArea, MouseEventArgs e);

	void DoCallBack();
}
