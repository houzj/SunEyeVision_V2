using System;

namespace App.XmlParser;

public static class Math2
{
	private const double True = 1.0;

	private const double False = 0.0;

	public static double Plus(double a, double b)
	{
		return a + b;
	}

	public static double Minus(double a, double b)
	{
		return a - b;
	}

	public static double Negate(double a)
	{
		return a * -1.0;
	}

	public static double Mul(double a, double b)
	{
		return a * b;
	}

	public static double Div(double a, double b)
	{
		return a / b;
	}

	public static double Mod(double a, double b)
	{
		return a % b;
	}

	public static double And(double a, double b)
	{
		return (!a.Equals(0.0) && !b.Equals(0.0)) ? 1.0 : 0.0;
	}

	public static double Or(double a, double b)
	{
		return (!a.Equals(0.0) || !b.Equals(0.0)) ? 1.0 : 0.0;
	}

	public static double GreaterThan(double a, double b)
	{
		return (a > b) ? 1.0 : 0.0;
	}

	public static double GreaterThanEqual(double a, double b)
	{
		return (a >= b) ? 1.0 : 0.0;
	}

	public static double LessThanEqual(double a, double b)
	{
		return (a <= b) ? 1.0 : 0.0;
	}

	public static double LessThan(double a, double b)
	{
		return (a < b) ? 1.0 : 0.0;
	}

	public static double Equal(double a, double b)
	{
		return a.Equals(b) ? 1.0 : 0.0;
	}

	public static double NotEqual(double a, double b)
	{
		return a.Equals(b) ? 0.0 : 1.0;
	}

	public static double Asinh(double x)
	{
		return Math.Log(x + Math.Sqrt(x * x + 1.0));
	}

	public static double Acosh(double x)
	{
		return Math.Log(x + Math.Sqrt(x * x - 1.0));
	}

	public static double Atanh(double x)
	{
		return Math.Log((1.0 + x) * (1.0 - x)) / 2.0;
	}
}
