using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public abstract class ArrowBase : Shape
{
	private readonly PathGeometry _wholeGeometry = new PathGeometry();

	private readonly PathFigure _figureConcrete = new PathFigure();

	private readonly PathFigure _figureStart = new PathFigure();

	private readonly PathFigure _figureEnd = new PathFigure();

	public static readonly DependencyProperty ArrowAngleProperty = DependencyProperty.Register("ArrowAngle", typeof(double), typeof(ArrowBase), new FrameworkPropertyMetadata(90.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty ArrowLengthProperty = DependencyProperty.Register("ArrowLength", typeof(double), typeof(ArrowBase), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty ArrowEndsProperty = DependencyProperty.Register("ArrowEnds", typeof(ArrowEnds), typeof(ArrowBase), new FrameworkPropertyMetadata(ArrowEnds.End, FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty IsArrowClosedProperty = DependencyProperty.Register("IsArrowClosed", typeof(bool), typeof(ArrowBase), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty StartPointProperty = DependencyProperty.Register("StartPoint", typeof(Point), typeof(ArrowBase), new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsMeasure));

	public double ArrowAngle
	{
		get
		{
			return (double)GetValue(ArrowAngleProperty);
		}
		set
		{
			SetValue(ArrowAngleProperty, value);
		}
	}

	public double ArrowLength
	{
		get
		{
			return (double)GetValue(ArrowLengthProperty);
		}
		set
		{
			SetValue(ArrowLengthProperty, value);
		}
	}

	public ArrowEnds ArrowEnds
	{
		get
		{
			return (ArrowEnds)GetValue(ArrowEndsProperty);
		}
		set
		{
			SetValue(ArrowEndsProperty, value);
		}
	}

	public bool IsArrowClosed
	{
		get
		{
			return (bool)GetValue(IsArrowClosedProperty);
		}
		set
		{
			SetValue(IsArrowClosedProperty, value);
		}
	}

	public Point StartPoint
	{
		get
		{
			return (Point)GetValue(StartPointProperty);
		}
		set
		{
			SetValue(StartPointProperty, value);
		}
	}

	protected override Geometry DefiningGeometry
	{
		get
		{
			_figureConcrete.StartPoint = StartPoint;
			_figureConcrete.Segments.Clear();
			PathSegmentCollection pathSegmentCollection = FillFigure();
			if (pathSegmentCollection != null)
			{
				foreach (PathSegment item in pathSegmentCollection)
				{
					_figureConcrete.Segments.Add(item);
				}
			}
			if ((ArrowEnds & ArrowEnds.Start) == ArrowEnds.Start)
			{
				CalculateArrow(_figureStart, GetStartArrowEndPoint(), StartPoint);
			}
			if ((ArrowEnds & ArrowEnds.End) == ArrowEnds.End)
			{
				CalculateArrow(_figureEnd, GetEndArrowStartPoint(), GetEndArrowEndPoint());
			}
			return _wholeGeometry;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	protected ArrowBase()
	{
		PolyLineSegment value = new PolyLineSegment();
		_figureStart.Segments.Add(value);
		PolyLineSegment value2 = new PolyLineSegment();
		_figureEnd.Segments.Add(value2);
		_figureConcrete.IsFilled = false;
		_wholeGeometry.Figures.Add(_figureConcrete);
		_wholeGeometry.Figures.Add(_figureStart);
		_wholeGeometry.Figures.Add(_figureEnd);
	}

	protected abstract PathSegmentCollection FillFigure();

	protected abstract Point GetStartArrowEndPoint();

	protected abstract Point GetEndArrowStartPoint();

	protected abstract Point GetEndArrowEndPoint();

	private void CalculateArrow(PathFigure pathfig, Point startPoint, Point endPoint)
	{
		if (pathfig.Segments[0] is PolyLineSegment polyLineSegment)
		{
			Matrix matrix = default;
			Vector vector = startPoint - endPoint;
			vector.Normalize();
			vector *= ArrowLength;
			matrix.Rotate(ArrowAngle / 2.0);
			pathfig.StartPoint = endPoint + vector * matrix;
			polyLineSegment.Points.Clear();
			polyLineSegment.Points.Add(endPoint);
			matrix.Rotate(0.0 - ArrowAngle);
			polyLineSegment.Points.Add(endPoint + vector * matrix);
		}
		pathfig.IsClosed = IsArrowClosed;
	}
}
