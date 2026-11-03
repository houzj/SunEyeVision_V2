using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.Media.Animation;

public class PennerEquation : IterativeEquation<double>
{
	internal delegate double PennerEquationDelegate(double t, double b, double c, double d);

	private readonly PennerEquationDelegate _pennerImpl;

	internal PennerEquation(PennerEquationDelegate pennerImpl)
	{
		_pennerImpl = pennerImpl;
	}

	public override double Evaluate(TimeSpan currentTime, double from, double to, TimeSpan duration)
	{
		double totalSeconds = currentTime.TotalSeconds;
		double c = to - from;
		double totalSeconds2 = duration.TotalSeconds;
		return _pennerImpl(totalSeconds, from, c, totalSeconds2);
	}
}
