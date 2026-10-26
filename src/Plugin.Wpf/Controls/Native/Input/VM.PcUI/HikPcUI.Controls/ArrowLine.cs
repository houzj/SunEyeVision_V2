using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public class ArrowLine : ArrowBase
{
	private readonly LineSegment _lineSegmentStart = new LineSegment();

	private readonly LineSegment _lineSegmentMiddle = new LineSegment();

	private readonly LineSegment _lineSegmentEnd = new LineSegment();

	public static readonly DependencyProperty MidStartPointProperty = DependencyProperty.Register("MidStartPoint", typeof(Point), typeof(ArrowLine), new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty MidEndPointProperty = DependencyProperty.Register("MidEndPoint", typeof(Point), typeof(ArrowLine), new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty EndPointProperty = DependencyProperty.Register("EndPoint", typeof(Point), typeof(ArrowLine), new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsMeasure));

	public Point MidStartPoint
	{
		get
		{
			return (Point)GetValue(MidStartPointProperty);
		}
		set
		{
			SetValue(MidStartPointProperty, value);
		}
	}

	public Point MidEndPoint
	{
		get
		{
			return (Point)GetValue(MidEndPointProperty);
		}
		set
		{
			SetValue(MidEndPointProperty, value);
		}
	}

	public Point EndPoint
	{
		get
		{
			return (Point)GetValue(EndPointProperty);
		}
		set
		{
			SetValue(EndPointProperty, value);
		}
	}

	protected override PathSegmentCollection FillFigure()
	{
		_lineSegmentStart.Point = MidStartPoint;
		_lineSegmentMiddle.Point = MidEndPoint;
		_lineSegmentEnd.Point = EndPoint;
		return new PathSegmentCollection { _lineSegmentStart, _lineSegmentMiddle, _lineSegmentEnd };
	}

	protected override Point GetStartArrowEndPoint()
	{
		return EndPoint;
	}

	protected override Point GetEndArrowStartPoint()
	{
		return MidEndPoint;
	}

	protected override Point GetEndArrowEndPoint()
	{
		return EndPoint;
	}
}
