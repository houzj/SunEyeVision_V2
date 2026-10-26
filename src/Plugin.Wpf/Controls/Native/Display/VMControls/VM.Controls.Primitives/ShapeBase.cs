using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

public abstract class ShapeBase : Shape
{
	private Pen _pen;

	internal bool IsPenEmptyOrUndefined
	{
		get
		{
			double strokeThickness = StrokeThickness;
			if (Stroke != null && !DoubleHelper.IsNaN(strokeThickness))
			{
				return DoubleHelper.AreVirtuallyEqual(0.0, strokeThickness);
			}
			return true;
		}
	}

	protected abstract override Geometry DefiningGeometry { get; }

	static ShapeBase()
	{
		Shape.StrokeDashArrayProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeDashCapProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeDashOffsetProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeEndLineCapProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeLineJoinProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeMiterLimitProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeStartLineCapProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
		Shape.StrokeThicknessProperty.OverrideMetadata(typeof(ShapeBase), new FrameworkPropertyMetadata(OnStrokeChanged));
	}

	internal virtual Rect GetDefiningGeometryBounds()
	{
		return DefiningGeometry.Bounds;
	}

	internal virtual Size GetNaturalSize()
	{
		Rect renderBounds = DefiningGeometry.GetRenderBounds(GetPen());
		return new Size(Math.Max(renderBounds.Right, 0.0), Math.Max(renderBounds.Bottom, 0.0));
	}

	internal Pen GetPen()
	{
		if (IsPenEmptyOrUndefined)
		{
			return null;
		}
		if (_pen == null)
		{
			_pen = MakePen();
		}
		return _pen;
	}

	internal double GetStrokeThickness()
	{
		if (IsPenEmptyOrUndefined)
		{
			return 0.0;
		}
		return Math.Abs(StrokeThickness);
	}

	internal bool IsSizeEmptyOrUndefined(Size size)
	{
		if (!DoubleHelper.IsNaN(size.Width) && !DoubleHelper.IsNaN(size.Height))
		{
			return size.IsEmpty;
		}
		return true;
	}

	private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((ShapeBase)d)._pen = null;
	}

	private Pen MakePen()
	{
		Pen pen = new Pen();
		pen.Brush = Stroke;
		pen.DashCap = StrokeDashCap;
		if (StrokeDashArray != null || StrokeDashOffset != 0.0)
		{
			pen.DashStyle = new DashStyle(StrokeDashArray, StrokeDashOffset);
		}
		pen.EndLineCap = StrokeEndLineCap;
		pen.LineJoin = StrokeLineJoin;
		pen.MiterLimit = StrokeMiterLimit;
		pen.StartLineCap = StrokeStartLineCap;
		pen.Thickness = Math.Abs(StrokeThickness);
		return pen;
	}
}
