using System.Windows;
using System.Windows.Input;

namespace HikPcUI.ImageView;

public interface RectangleRoi
{
	void Doth(FrameworkElement rectangleArea, MouseEventArgs e);

	void DoCallBack();
}
