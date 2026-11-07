using System;
using System.ComponentModel;

namespace VM.Controls.Core.Media.Animation;

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
