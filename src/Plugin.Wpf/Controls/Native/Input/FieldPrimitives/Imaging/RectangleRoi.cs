using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Imaging;

public interface RectangleRoi
{
	void Doth(FrameworkElement rectangleArea, MouseEventArgs e);

	void DoCallBack();
}
