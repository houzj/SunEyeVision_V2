using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal struct Segment
{
	private bool _isP1Excluded;

	private bool _isP2Excluded;

	private Point _p1;

	private Point _p2;

	public static Segment Empty
	{
		get
		{
			Segment result = new Segment(new Point(0.0, 0.0));
			result._isP1Excluded = true;
			result._isP2Excluded = true;
			return result;
		}
	}

	public Point P1 => _p1;

	public Point P2 => _p2;

	public bool IsP1Excluded => _isP1Excluded;

	public bool IsP2Excluded => _isP2Excluded;

	public bool IsEmpty
	{
		get
		{
			if (DoubleHelper.AreVirtuallyEqual(_p1, _p2))
			{
				if (!_isP1Excluded)
				{
					return _isP2Excluded;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsPoint => DoubleHelper.AreVirtuallyEqual(_p1, _p2);

	public double Length => (P2 - P1).Length;

	public double Slope
	{
		get
		{
			if (P2.X != P1.X)
			{
				return (P2.Y - P1.Y) / (P2.X - P1.X);
			}
			return double.NaN;
		}
	}

	public Segment(Point point)
	{
		_p1 = point;
		_p2 = point;
		_isP1Excluded = false;
		_isP2Excluded = false;
	}

	public Segment(Point p1, Point p2)
	{
		_p1 = p1;
		_p2 = p2;
		_isP1Excluded = false;
		_isP2Excluded = false;
	}

	public Segment(Point p1, Point p2, bool excludeP1, bool excludeP2)
	{
		_p1 = p1;
		_p2 = p2;
		_isP1Excluded = excludeP1;
		_isP2Excluded = excludeP2;
	}

	public bool Contains(Point point)
	{
		if (IsEmpty)
		{
			return false;
		}
		if (DoubleHelper.AreVirtuallyEqual(_p1, point))
		{
			return _isP1Excluded;
		}
		if (DoubleHelper.AreVirtuallyEqual(_p2, point))
		{
			return _isP2Excluded;
		}
		bool result = false;
		if (DoubleHelper.AreVirtuallyEqual(Slope, new Segment(_p1, point).Slope))
		{
			result = point.X >= Math.Min(_p1.X, _p2.X) && point.X <= Math.Max(_p1.X, _p2.X) && point.Y >= Math.Min(_p1.Y, _p2.Y) && point.Y <= Math.Max(_p1.Y, _p2.Y);
		}
		return result;
	}

	public bool Contains(Segment segment)
	{
		return segment == Intersection(segment);
	}

	public override bool Equals(object o)
	{
		if (!(o is Segment segment))
		{
			return false;
		}
		if (IsEmpty)
		{
			return segment.IsEmpty;
		}
		if (DoubleHelper.AreVirtuallyEqual(_p1, segment._p1))
		{
			if (DoubleHelper.AreVirtuallyEqual(_p2, segment._p2) && _isP1Excluded == segment._isP1Excluded)
			{
				return _isP2Excluded == segment._isP2Excluded;
			}
			return false;
		}
		if (DoubleHelper.AreVirtuallyEqual(_p1, segment._p2) && DoubleHelper.AreVirtuallyEqual(_p2, segment._p1) && _isP1Excluded == segment._isP2Excluded)
		{
			return _isP2Excluded == segment._isP1Excluded;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return _p1.GetHashCode() ^ _p2.GetHashCode() ^ _isP1Excluded.GetHashCode() ^ _isP2Excluded.GetHashCode();
	}

	public Segment Intersection(Segment segment)
	{
		if (IsEmpty || segment.IsEmpty)
		{
			return Empty;
		}
		if (this == segment)
		{
			return new Segment(_p1, _p2, _isP1Excluded, _isP2Excluded);
		}
		if (IsPoint)
		{
			if (!segment.Contains(_p1))
			{
				return Empty;
			}
			return new Segment(_p1);
		}
		if (segment.IsPoint)
		{
			if (!Contains(segment._p1))
			{
				return Empty;
			}
			return new Segment(segment._p1);
		}
		Point p = _p1;
		Vector vector = _p2 - _p1;
		Point p2 = segment._p1;
		Vector vector2 = segment._p2 - segment._p1;
		Vector vector3 = p2 - p;
		double num = Vector.CrossProduct(vector, vector2);
		if (!DoubleHelper.AreVirtuallyEqual(Slope, segment.Slope))
		{
			double num2 = Vector.CrossProduct(vector3, vector) / num;
			if (num2 < 0.0 || num2 > 1.0)
			{
				return Empty;
			}
			num2 = Vector.CrossProduct(vector3, vector2) / num;
			if (num2 < 0.0 || num2 > 1.0)
			{
				return Empty;
			}
			return new Segment(p + num2 * vector);
		}
		num = Vector.CrossProduct(vector3, vector);
		if (num * num > 1E-06 * vector.LengthSquared * vector3.LengthSquared)
		{
			return Empty;
		}
		Segment result = default;
		Segment segment2 = new Segment(_p1, _p2);
		Segment segment3 = new Segment(segment._p1, segment._p2);
		bool flag = segment3.Contains(segment2._p1);
		bool flag2 = segment3.Contains(segment2._p2);
		if (flag & flag2)
		{
			result._p1 = _p1;
			result._p2 = _p2;
			result._isP1Excluded = _isP1Excluded || !segment.Contains(_p1);
			result._isP2Excluded = _isP2Excluded || !segment.Contains(_p2);
			return result;
		}
		bool flag3 = segment2.Contains(segment3._p1);
		bool flag4 = segment2.Contains(segment3._p2);
		if (flag3 & flag4)
		{
			result._p1 = segment._p1;
			result._p2 = segment._p2;
			result._isP1Excluded = segment._isP1Excluded || !Contains(segment._p1);
			result._isP2Excluded = segment._isP2Excluded || !Contains(segment._p2);
			return result;
		}
		if (flag)
		{
			result._p1 = _p1;
			result._isP1Excluded = _isP1Excluded || !segment.Contains(_p1);
		}
		else
		{
			result._p1 = _p2;
			result._isP1Excluded = _isP2Excluded || !segment.Contains(_p2);
		}
		if (flag3)
		{
			result._p2 = segment._p1;
			result._isP2Excluded = segment._isP1Excluded || !Contains(segment._p1);
		}
		else
		{
			result._p2 = segment._p2;
			result._isP2Excluded = segment._isP2Excluded || !Contains(segment._p2);
		}
		return result;
	}

	public override string ToString()
	{
		string text = base.ToString();
		if (IsEmpty)
		{
			return text + ": {Empty}";
		}
		if (IsPoint)
		{
			return text + ", Point: " + _p1.ToString();
		}
		return text + ": " + _p1.ToString() + (_isP1Excluded ? " (excl)" : " (incl)") + " to " + _p2.ToString() + (_isP2Excluded ? " (excl)" : " (incl)");
	}

	public static bool operator ==(Segment s1, Segment s2)
	{
		return s1.Equals(s2);
	}

	public static bool operator !=(Segment s1, Segment s2)
	{
		return !(s1 == s2);
	}
}
