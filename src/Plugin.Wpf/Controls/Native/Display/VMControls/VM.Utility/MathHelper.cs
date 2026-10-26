using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Text.RegularExpressions;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

public static class MathHelper
{
	public static Point[] GetLine(double startX, double startY, double angle, double distance)
	{
		Point point = new Point(startX, startY);
		return new Point[2]
		{
			point,
			point.GetEndPoint(angle, distance)
		};
	}

	public static Point GetEndPoint(this Point start, double angle, double distance)
	{
		double d = angle * Math.PI / 180.0;
		double d2 = (90.0 - angle) * Math.PI / 180.0;
		double num = distance * (double)(float)Math.Cos(d);
		double num2 = distance * (double)(float)Math.Cos(d2);
		return new Point(start.X + num, start.Y + num2);
	}

	public static bool IsNumber(string strNumber)
	{
		if (strNumber == null)
		{
			return false;
		}
		Regex regex = new Regex("[^0-9.-]");
		Regex regex2 = new Regex("[0-9]*[.][0-9]*[.][0-9]*");
		Regex regex3 = new Regex("[0-9]*[-][0-9]*[-][0-9]*");
		string text = "^([-]|[.]|[-.]|[0-9])[0-9]*[.]*[0-9]+$";
		string text2 = "^([-]|[0-9])[0-9]*$";
		Regex regex4 = new Regex("(" + text + ")|(" + text2 + ")");
		if (!regex.IsMatch(strNumber) && !regex2.IsMatch(strNumber) && !regex3.IsMatch(strNumber))
		{
			return regex4.IsMatch(strNumber);
		}
		return false;
	}

	public static bool IsConst(string strNumber)
	{
		if (!IsInt(strNumber) && !IsFloat(strNumber))
		{
			return IsString(strNumber);
		}
		return true;
	}

	public static bool IsInt(string strValue)
	{
		return new Regex("^[-|+]?[0-9]+$").IsMatch(strValue);
	}

	public static bool IsFloat(string strValue)
	{
		return new Regex("^([-|+]?[0-9]+)([.]?[0-9]*)$").IsMatch(strValue);
	}

	public static bool IsString(string strValue)
	{
		return new Regex("^[A-Za-z0-9\\u4e00-\\u9fa5_,]*$").IsMatch(strValue);
	}
}
