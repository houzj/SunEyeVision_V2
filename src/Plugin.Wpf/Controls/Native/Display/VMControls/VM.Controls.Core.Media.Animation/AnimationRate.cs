using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Media.Animation;

[StructLayout(LayoutKind.Explicit)]
[TypeConverter(typeof(AnimationRateConverter))]
public struct AnimationRate
{
	private enum RateType
	{
		TimeSpan,
		Speed
	}

	private static AnimationRate _default = new AnimationRate(ignore: true);

	[FieldOffset(0)]
	private long _duration;

	[FieldOffset(0)]
	private double _speed;

	[FieldOffset(8)]
	private RateType _rateType;

	public static AnimationRate Default => _default;

	public bool HasDuration => _rateType == RateType.TimeSpan;

	public TimeSpan Duration
	{
		get
		{
			if (HasDuration)
			{
				return TimeSpan.FromTicks(_duration);
			}
			throw new InvalidOperationException(string.Format(ErrorMessages.GetMessage("InvalidRatePropertyAccessed"), "Duration", this, "Speed"));
		}
	}

	public bool HasSpeed => _rateType == RateType.Speed;

	public double Speed
	{
		get
		{
			if (HasSpeed)
			{
				return _speed;
			}
			throw new InvalidOperationException(string.Format(ErrorMessages.GetMessage("InvalidRatePropertyAccessed"), "Speed", this, "Duration"));
		}
	}

	public AnimationRate(TimeSpan duration)
	{
		if (duration < TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.GetMessage("NegativeTimeSpanNotSupported"));
		}
		_speed = 0.0;
		_duration = duration.Ticks;
		_rateType = RateType.TimeSpan;
	}

	public AnimationRate(double speed)
	{
		if (DoubleHelper.IsNaN(speed) || speed < 0.0)
		{
			throw new ArgumentException(ErrorMessages.GetMessage("NegativeSpeedNotSupported"));
		}
		_duration = 0L;
		_speed = speed;
		_rateType = RateType.Speed;
	}

	private AnimationRate(bool ignore)
	{
		_duration = 0L;
		_speed = double.NaN;
		_rateType = RateType.Speed;
	}

	public AnimationRate Add(AnimationRate animationRate)
	{
		return this + animationRate;
	}

	public override bool Equals(object value)
	{
		if (value == null)
		{
			return false;
		}
		if (value is AnimationRate)
		{
			return Equals((AnimationRate)value);
		}
		return false;
	}

	public bool Equals(AnimationRate animationRate)
	{
		if (HasDuration)
		{
			if (animationRate.HasDuration)
			{
				return _duration == animationRate._duration;
			}
			return false;
		}
		if (animationRate.HasSpeed)
		{
			if (DoubleHelper.IsNaN(_speed))
			{
				return DoubleHelper.IsNaN(animationRate._speed);
			}
			return _speed == animationRate._speed;
		}
		return false;
	}

	public static bool Equals(AnimationRate t1, AnimationRate t2)
	{
		return t1.Equals(t2);
	}

	public override int GetHashCode()
	{
		if (HasDuration)
		{
			return _duration.GetHashCode();
		}
		return _speed.GetHashCode();
	}

	public AnimationRate Subtract(AnimationRate animationRate)
	{
		return this - animationRate;
	}

	public override string ToString()
	{
		if (HasDuration)
		{
			return TypeDescriptor.GetConverter(_duration)?.ConvertToString(_duration);
		}
		return TypeDescriptor.GetConverter(_speed)?.ConvertToString(_speed);
	}

	public static implicit operator AnimationRate(TimeSpan duration)
	{
		if (duration < TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.GetMessage("NegativeTimeSpanNotSupported"));
		}
		return new AnimationRate(duration);
	}

	public static implicit operator AnimationRate(double speed)
	{
		if (DoubleHelper.IsNaN(speed) || speed < 0.0)
		{
			throw new ArgumentException(ErrorMessages.GetMessage("NegativeSpeedNotSupported"));
		}
		return new AnimationRate(speed);
	}

	public static implicit operator AnimationRate(int speed)
	{
		if (DoubleHelper.IsNaN(speed) || speed < 0)
		{
			throw new ArgumentException(ErrorMessages.GetMessage("NegativeSpeedNotSupported"));
		}
		return new AnimationRate(speed);
	}

	public static AnimationRate operator +(AnimationRate t1, AnimationRate t2)
	{
		if (t1.HasDuration && t2.HasDuration)
		{
			return new AnimationRate(t1._duration + t2._duration);
		}
		if (t1.HasSpeed && t2.HasSpeed)
		{
			return new AnimationRate(t1._speed + t2._speed);
		}
		return 0.0;
	}

	public static AnimationRate operator -(AnimationRate t1, AnimationRate t2)
	{
		if (t1.HasDuration && t2.HasDuration)
		{
			return new AnimationRate(t1._duration - t2._duration);
		}
		if (t1.HasSpeed && t2.HasSpeed)
		{
			return new AnimationRate(t1._speed - t2._speed);
		}
		return 0.0;
	}

	public static bool operator ==(AnimationRate t1, AnimationRate t2)
	{
		return t1.Equals(t2);
	}

	public static bool operator !=(AnimationRate t1, AnimationRate t2)
	{
		return !t1.Equals(t2);
	}

	public static bool operator >(AnimationRate t1, AnimationRate t2)
	{
		if (t1.HasDuration && t2.HasDuration)
		{
			return t1._duration > t2._duration;
		}
		if (t1.HasSpeed && t2.HasSpeed)
		{
			if (t1._speed > t2._speed)
			{
				return !DoubleHelper.AreVirtuallyEqual(t1._speed, t2._speed);
			}
			return false;
		}
		return t1.HasSpeed;
	}

	public static bool operator >=(AnimationRate t1, AnimationRate t2)
	{
		return !(t1 < t2);
	}

	public static bool operator <(AnimationRate t1, AnimationRate t2)
	{
		if (t1.HasDuration && t2.HasDuration)
		{
			return t1._duration < t2._duration;
		}
		if (t1.HasSpeed && t2.HasSpeed)
		{
			if (t1._speed < t2._speed)
			{
				return !DoubleHelper.AreVirtuallyEqual(t1._speed, t2._speed);
			}
			return false;
		}
		return t1.HasDuration;
	}

	public static bool operator <=(AnimationRate t1, AnimationRate t2)
	{
		return !(t1 > t2);
	}

	public static int Compare(AnimationRate t1, AnimationRate t2)
	{
		if (t1 < t2)
		{
			return -1;
		}
		if (t1 > t2)
		{
			return 1;
		}
		return 0;
	}

	public static AnimationRate Plus(AnimationRate animationRate)
	{
		return animationRate;
	}

	public static AnimationRate operator +(AnimationRate animationRate)
	{
		return animationRate;
	}
}
