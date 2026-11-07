using System.Windows;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(ICross))]
public class CrossShape : ShapeContainer
{
	static CrossShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CrossShape), new FrameworkPropertyMetadata(typeof(CrossShape)));
	}
}
