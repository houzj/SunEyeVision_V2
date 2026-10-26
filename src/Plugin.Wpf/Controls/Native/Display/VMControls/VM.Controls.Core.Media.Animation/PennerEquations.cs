using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Media.Animation;

public static class PennerEquations
{
	private static PennerEquation _backEaseIn;

	private static PennerEquation _backEaseInOut;

	private static PennerEquation _backEaseOut;

	private static PennerEquation _bounceEaseIn;

	private static PennerEquation _bounceEaseInOut;

	private static PennerEquation _bounceEaseOut;

	private static PennerEquation _circEaseIn;

	private static PennerEquation _circEaseInOut;

	private static PennerEquation _circEaseOut;

	private static PennerEquation _cubicEaseIn;

	private static PennerEquation _cubicEaseInOut;

	private static PennerEquation _cubicEaseOut;

	private static PennerEquation _elasticEaseIn;

	private static PennerEquation _elasticEaseInOut;

	private static PennerEquation _elasticEaseOut;

	private static PennerEquation _expoEaseIn;

	private static PennerEquation _expoEaseInOut;

	private static PennerEquation _expoEaseOut;

	private static PennerEquation _linear;

	private static PennerEquation _quadEaseIn;

	private static PennerEquation _quadEaseInOut;

	private static PennerEquation _quadEaseOut;

	private static PennerEquation _quartEaseIn;

	private static PennerEquation _quartEaseInOut;

	private static PennerEquation _quartEaseOut;

	private static PennerEquation _quintEaseIn;

	private static PennerEquation _quintEaseInOut;

	private static PennerEquation _quintEaseOut;

	private static PennerEquation _sineEaseIn;

	private static PennerEquation _sineEaseInOut;

	private static PennerEquation _sineEaseOut;

	public static PennerEquation BackEaseIn
	{
		get
		{
			if (_backEaseIn == null)
			{
				_backEaseIn = new PennerEquation(BackEaseInImpl);
			}
			return _backEaseIn;
		}
	}

	public static PennerEquation BackEaseInOut
	{
		get
		{
			if (_backEaseInOut == null)
			{
				_backEaseInOut = new PennerEquation(BackEaseInOutImpl);
			}
			return _backEaseInOut;
		}
	}

	public static PennerEquation BackEaseOut
	{
		get
		{
			if (_backEaseOut == null)
			{
				_backEaseOut = new PennerEquation(BackEaseOutImpl);
			}
			return _backEaseOut;
		}
	}

	public static PennerEquation BounceEaseIn
	{
		get
		{
			if (_bounceEaseIn == null)
			{
				_bounceEaseIn = new PennerEquation(BounceEaseInImpl);
			}
			return _bounceEaseIn;
		}
	}

	public static PennerEquation BounceEaseInOut
	{
		get
		{
			if (_bounceEaseInOut == null)
			{
				_bounceEaseInOut = new PennerEquation(BounceEaseInOutImpl);
			}
			return _bounceEaseInOut;
		}
	}

	public static PennerEquation BounceEaseOut
	{
		get
		{
			if (_bounceEaseOut == null)
			{
				_bounceEaseOut = new PennerEquation(BounceEaseOutImpl);
			}
			return _bounceEaseOut;
		}
	}

	public static PennerEquation CircEaseIn
	{
		get
		{
			if (_circEaseIn == null)
			{
				_circEaseIn = new PennerEquation(CircEaseInImpl);
			}
			return _circEaseIn;
		}
	}

	public static PennerEquation CircEaseInOut
	{
		get
		{
			if (_circEaseInOut == null)
			{
				_circEaseInOut = new PennerEquation(CircEaseInOutImpl);
			}
			return _circEaseInOut;
		}
	}

	public static PennerEquation CircEaseOut
	{
		get
		{
			if (_circEaseOut == null)
			{
				_circEaseOut = new PennerEquation(CircEaseOutImpl);
			}
			return _circEaseOut;
		}
	}

	public static PennerEquation CubicEaseIn
	{
		get
		{
			if (_cubicEaseIn == null)
			{
				_cubicEaseIn = new PennerEquation(CubicEaseInImpl);
			}
			return _cubicEaseIn;
		}
	}

	public static PennerEquation CubicEaseInOut
	{
		get
		{
			if (_cubicEaseInOut == null)
			{
				_cubicEaseInOut = new PennerEquation(CubicEaseInOutImpl);
			}
			return _cubicEaseInOut;
		}
	}

	public static PennerEquation CubicEaseOut
	{
		get
		{
			if (_cubicEaseOut == null)
			{
				_cubicEaseOut = new PennerEquation(CubicEaseOutImpl);
			}
			return _cubicEaseOut;
		}
	}

	public static PennerEquation ElasticEaseIn
	{
		get
		{
			if (_elasticEaseIn == null)
			{
				_elasticEaseIn = new PennerEquation(ElasticEaseInImpl);
			}
			return _elasticEaseIn;
		}
	}

	public static PennerEquation ElasticEaseInOut
	{
		get
		{
			if (_elasticEaseInOut == null)
			{
				_elasticEaseInOut = new PennerEquation(ElasticEaseInOutImpl);
			}
			return _elasticEaseInOut;
		}
	}

	public static PennerEquation ElasticEaseOut
	{
		get
		{
			if (_elasticEaseOut == null)
			{
				_elasticEaseOut = new PennerEquation(ElasticEaseOutImpl);
			}
			return _elasticEaseOut;
		}
	}

	public static PennerEquation ExpoEaseIn
	{
		get
		{
			if (_expoEaseIn == null)
			{
				_expoEaseIn = new PennerEquation(ExpoEaseInImpl);
			}
			return _expoEaseIn;
		}
	}

	public static PennerEquation ExpoEaseInOut
	{
		get
		{
			if (_expoEaseInOut == null)
			{
				_expoEaseInOut = new PennerEquation(ExpoEaseInOutImpl);
			}
			return _expoEaseInOut;
		}
	}

	public static PennerEquation ExpoEaseOut
	{
		get
		{
			if (_expoEaseOut == null)
			{
				_expoEaseOut = new PennerEquation(ExpoEaseOutImpl);
			}
			return _expoEaseOut;
		}
	}

	public static PennerEquation Linear
	{
		get
		{
			if (_linear == null)
			{
				_linear = new PennerEquation(LinearImpl);
			}
			return _linear;
		}
	}

	public static PennerEquation QuadEaseIn
	{
		get
		{
			if (_quadEaseIn == null)
			{
				_quadEaseIn = new PennerEquation(QuadEaseInImpl);
			}
			return _quadEaseIn;
		}
	}

	public static PennerEquation QuadEaseInOut
	{
		get
		{
			if (_quadEaseInOut == null)
			{
				_quadEaseInOut = new PennerEquation(QuadEaseInOutImpl);
			}
			return _quadEaseInOut;
		}
	}

	public static PennerEquation QuadEaseOut
	{
		get
		{
			if (_quadEaseOut == null)
			{
				_quadEaseOut = new PennerEquation(QuadEaseOutImpl);
			}
			return _quadEaseOut;
		}
	}

	public static PennerEquation QuartEaseIn
	{
		get
		{
			if (_quartEaseIn == null)
			{
				_quartEaseIn = new PennerEquation(QuartEaseInImpl);
			}
			return _quartEaseIn;
		}
	}

	public static PennerEquation QuartEaseInOut
	{
		get
		{
			if (_quartEaseInOut == null)
			{
				_quartEaseInOut = new PennerEquation(QuartEaseInOutImpl);
			}
			return _quartEaseInOut;
		}
	}

	public static PennerEquation QuartEaseOut
	{
		get
		{
			if (_quartEaseOut == null)
			{
				_quartEaseOut = new PennerEquation(QuartEaseOutImpl);
			}
			return _quartEaseOut;
		}
	}

	public static PennerEquation QuintEaseIn
	{
		get
		{
			if (_quintEaseIn == null)
			{
				_quintEaseIn = new PennerEquation(QuintEaseInImpl);
			}
			return _quintEaseIn;
		}
	}

	public static PennerEquation QuintEaseInOut
	{
		get
		{
			if (_quintEaseInOut == null)
			{
				_quintEaseInOut = new PennerEquation(QuintEaseInOutImpl);
			}
			return _quintEaseInOut;
		}
	}

	public static PennerEquation QuintEaseOut
	{
		get
		{
			if (_quintEaseOut == null)
			{
				_quintEaseOut = new PennerEquation(QuintEaseOutImpl);
			}
			return _quintEaseOut;
		}
	}

	public static PennerEquation SineEaseIn
	{
		get
		{
			if (_sineEaseIn == null)
			{
				_sineEaseIn = new PennerEquation(SineEaseInImpl);
			}
			return _sineEaseIn;
		}
	}

	public static PennerEquation SineEaseInOut
	{
		get
		{
			if (_sineEaseInOut == null)
			{
				_sineEaseInOut = new PennerEquation(SineEaseInOutImpl);
			}
			return _sineEaseInOut;
		}
	}

	public static PennerEquation SineEaseOut
	{
		get
		{
			if (_sineEaseOut == null)
			{
				_sineEaseOut = new PennerEquation(SineEaseOutImpl);
			}
			return _sineEaseOut;
		}
	}

	private static double BackEaseOutImpl(double t, double b, double c, double d)
	{
		return c * ((t = t / d - 1.0) * t * (2.70158 * t + 1.70158) + 1.0) + b;
	}

	private static double BackEaseInImpl(double t, double b, double c, double d)
	{
		return c * (t /= d) * t * (2.70158 * t - 1.70158) + b;
	}

	private static double BackEaseInOutImpl(double t, double b, double c, double d)
	{
		double num = 1.70158;
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * (t * t * (((num *= 1.525) + 1.0) * t - num)) + b;
		}
		return c / 2.0 * ((t -= 2.0) * t * (((num *= 1.525) + 1.0) * t + num) + 2.0) + b;
	}

	private static double BounceEaseOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d) < 372.0 / 1023.0)
		{
			return c * (7.5625 * t * t) + b;
		}
		if (t < 744.0 / 1023.0)
		{
			return c * (7.5625 * (t -= 558.0 / 1023.0) * t + 0.75) + b;
		}
		if (t < 930.0 / 1023.0)
		{
			return c * (7.5625 * (t -= 837.0 / 1023.0) * t + 0.9375) + b;
		}
		return c * (7.5625 * (t -= 21.0 / 22.0) * t + 63.0 / 64.0) + b;
	}

	private static double BounceEaseInImpl(double t, double b, double c, double d)
	{
		return c - BounceEaseOutImpl(d - t, 0.0, c, d) + b;
	}

	private static double BounceEaseInOutImpl(double t, double b, double c, double d)
	{
		if (t < d / 2.0)
		{
			return BounceEaseInImpl(t * 2.0, 0.0, c, d) * 0.5 + b;
		}
		return BounceEaseOutImpl(t * 2.0 - d, 0.0, c, d) * 0.5 + c * 0.5 + b;
	}

	private static double CircEaseOutImpl(double t, double b, double c, double d)
	{
		return c * Math.Sqrt(1.0 - (t = t / d - 1.0) * t) + b;
	}

	private static double CircEaseInImpl(double t, double b, double c, double d)
	{
		return (0.0 - c) * (Math.Sqrt(1.0 - (t /= d) * t) - 1.0) + b;
	}

	private static double CircEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) < 1.0)
		{
			return (0.0 - c) / 2.0 * (Math.Sqrt(1.0 - t * t) - 1.0) + b;
		}
		return c / 2.0 * (Math.Sqrt(1.0 - (t -= 2.0) * t) + 1.0) + b;
	}

	private static double CubicEaseOutImpl(double t, double b, double c, double d)
	{
		return c * ((t = t / d - 1.0) * t * t + 1.0) + b;
	}

	private static double CubicEaseInImpl(double t, double b, double c, double d)
	{
		return c * (t /= d) * t * t + b;
	}

	private static double CubicEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * t * t * t + b;
		}
		return c / 2.0 * ((t -= 2.0) * t * t + 2.0) + b;
	}

	private static double ElasticEaseOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d) == 1.0)
		{
			return b + c;
		}
		double num = d * 0.3;
		double num2 = num / 4.0;
		return c * Math.Pow(2.0, -10.0 * t) * Math.Sin((t * d - num2) * (Math.PI * 2.0) / num) + c + b;
	}

	private static double ElasticEaseInImpl(double t, double b, double c, double d)
	{
		if ((t /= d) == 1.0)
		{
			return b + c;
		}
		double num = d * 0.3;
		double num2 = num / 4.0;
		return 0.0 - c * Math.Pow(2.0, 10.0 * --t) * Math.Sin((t * d - num2) * (Math.PI * 2.0) / num) + b;
	}

	private static double ElasticEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) == 2.0)
		{
			return b + c;
		}
		double num = d * 0.44999999999999996;
		double num2 = num / 4.0;
		if (t < 1.0)
		{
			return -0.5 * (c * Math.Pow(2.0, 10.0 * --t) * Math.Sin((t * d - num2) * (Math.PI * 2.0) / num)) + b;
		}
		return c * Math.Pow(2.0, -10.0 * --t) * Math.Sin((t * d - num2) * (Math.PI * 2.0) / num) * 0.5 + c + b;
	}

	private static double ExpoEaseOutImpl(double t, double b, double c, double d)
	{
		if (t != d)
		{
			return c * (0.0 - Math.Pow(2.0, -10.0 * t / d) + 1.0) + b;
		}
		return b + c;
	}

	private static double ExpoEaseInImpl(double t, double b, double c, double d)
	{
		if (t != 0.0)
		{
			return c * Math.Pow(2.0, 10.0 * (t / d - 1.0)) + b;
		}
		return b;
	}

	private static double ExpoEaseInOutImpl(double t, double b, double c, double d)
	{
		if (t == 0.0)
		{
			return b;
		}
		if (t == d)
		{
			return b + c;
		}
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * Math.Pow(2.0, 10.0 * (t - 1.0)) + b;
		}
		return c / 2.0 * (0.0 - Math.Pow(2.0, -10.0 * --t) + 2.0) + b;
	}

	private static double LinearImpl(double t, double b, double c, double d)
	{
		return c * (t / d) + b;
	}

	private static double QuadEaseOutImpl(double t, double b, double c, double d)
	{
		return (0.0 - c) * (t /= d) * (t - 2.0) + b;
	}

	private static double QuadEaseInImpl(double t, double b, double c, double d)
	{
		return c * (t /= d) * t + b;
	}

	private static double QuadEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * t * t + b;
		}
		return (0.0 - c) / 2.0 * (--t * (t - 2.0) - 1.0) + b;
	}

	private static double QuartEaseOutImpl(double t, double b, double c, double d)
	{
		return (0.0 - c) * ((t = t / d - 1.0) * t * t * t - 1.0) + b;
	}

	private static double QuartEaseInImpl(double t, double b, double c, double d)
	{
		return c * (t /= d) * t * t * t + b;
	}

	private static double QuartEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * t * t * t * t + b;
		}
		return (0.0 - c) / 2.0 * ((t -= 2.0) * t * t * t - 2.0) + b;
	}

	private static double QuintEaseOutImpl(double t, double b, double c, double d)
	{
		return c * ((t = t / d - 1.0) * t * t * t * t + 1.0) + b;
	}

	private static double QuintEaseInImpl(double t, double b, double c, double d)
	{
		return c * (t /= d) * t * t * t * t + b;
	}

	private static double QuintEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * t * t * t * t * t + b;
		}
		return c / 2.0 * ((t -= 2.0) * t * t * t * t + 2.0) + b;
	}

	private static double SineEaseOutImpl(double t, double b, double c, double d)
	{
		return c * Math.Sin(t / d * (Math.PI / 2.0)) + b;
	}

	private static double SineEaseInImpl(double t, double b, double c, double d)
	{
		return (0.0 - c) * Math.Cos(t / d * (Math.PI / 2.0)) + c + b;
	}

	private static double SineEaseInOutImpl(double t, double b, double c, double d)
	{
		if ((t /= d / 2.0) < 1.0)
		{
			return c / 2.0 * Math.Sin(Math.PI * t / 2.0) + b;
		}
		return (0.0 - c) / 2.0 * (Math.Cos(Math.PI * --t / 2.0) - 2.0) + b;
	}
}
