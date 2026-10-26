using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Media.Animation;

[TypeConverter(typeof(IterativeEquationConverter))]
public class IterativeEquation<T>
{
	private readonly IterativeAnimationEquationDelegate<T> _equation;

	public IterativeEquation(IterativeAnimationEquationDelegate<T> equation)
	{
		_equation = equation;
	}

	internal IterativeEquation()
	{
	}

	public virtual T Evaluate(TimeSpan currentTime, T from, T to, TimeSpan duration)
	{
		return _equation(currentTime, from, to, duration);
	}
}
