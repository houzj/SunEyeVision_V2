using System;
using System.Windows;

namespace VM.Controls.Core.Utilities;

internal static class PointHelper
{
	public static Point Empty => new Point(double.NaN, double.NaN);

	public static double DistanceBetween(Point p1, Point p2)
	{
		return Math.Sqrt(Math.Pow(p1.X - p2.X, 2.0) + Math.Pow(p1.Y - p2.Y, 2.0));
	}

	public static bool IsEmpty(Point point)
	{
		if (DoubleHelper.IsNaN(point.X))
		{
			return DoubleHelper.IsNaN(point.Y);
		}
		return false;
	}
}
