using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(IAngleShape))]
public class LineAngleShape : ShapeContainer
{
	public static readonly DependencyProperty InitialLineStartPointProperty;

	public static readonly DependencyProperty InitialLineEndPointProperty;

	public static readonly DependencyProperty TerminalLineStartPointProperty;

	public static readonly DependencyProperty TerminalLineEndPointProperty;

	public static readonly DependencyProperty IntersectionProperty;

	public Point InitialLineStartPoint
	{
		get
		{
			return (Point)GetValue(InitialLineStartPointProperty);
		}
		set
		{
			SetValue(InitialLineStartPointProperty, value);
		}
	}

	public Point InitialLineEndPoint
	{
		get
		{
			return (Point)GetValue(InitialLineEndPointProperty);
		}
		set
		{
			SetValue(InitialLineEndPointProperty, value);
		}
	}

	public Point TerminalLineStartPoint
	{
		get
		{
			return (Point)GetValue(TerminalLineStartPointProperty);
		}
		set
		{
			SetValue(TerminalLineStartPointProperty, value);
		}
	}

	public Point TerminalLineEndPoint
	{
		get
		{
			return (Point)GetValue(TerminalLineEndPointProperty);
		}
		set
		{
			SetValue(TerminalLineEndPointProperty, value);
		}
	}

	public Point Intersection
	{
		get
		{
			return (Point)GetValue(IntersectionProperty);
		}
		set
		{
			SetValue(IntersectionProperty, value);
		}
	}

	static LineAngleShape()
	{
		InitialLineStartPointProperty = DependencyProperty.Register("InitialLineStartPoint", typeof(Point), typeof(LineAngleShape), new PropertyMetadata(default(Point)));
		InitialLineEndPointProperty = DependencyProperty.Register("InitialLineEndPoint", typeof(Point), typeof(LineAngleShape), new PropertyMetadata(default(Point)));
		TerminalLineStartPointProperty = DependencyProperty.Register("TerminalLineStartPoint", typeof(Point), typeof(LineAngleShape), new PropertyMetadata(default(Point)));
		TerminalLineEndPointProperty = DependencyProperty.Register("TerminalLineEndPoint", typeof(Point), typeof(LineAngleShape), new PropertyMetadata(default(Point)));
		IntersectionProperty = DependencyProperty.Register("Intersection", typeof(Point), typeof(LineAngleShape), new PropertyMetadata(default(Point)));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(LineAngleShape), new FrameworkPropertyMetadata(typeof(LineAngleShape)));
	}
}
