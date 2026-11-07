using System;
using System.Collections.Generic;

namespace App.XmlParser;

internal static class DefaultOperators
{
	internal static readonly Dictionary<string, IOperator> Operators;

	static DefaultOperators()
	{
		Operators = new Dictionary<string, IOperator>
		{
			{
				"+",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Plus(p[0], p[1])
				}
			},
			{
				"-",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Minus(p[0], p[1])
				}
			},
			{
				"negate",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math2.Negate(p[0])
				}
			},
			{
				"*",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Mul(p[0], p[1])
				}
			},
			{
				"/",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Div(p[0], p[1])
				}
			},
			{
				"%",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Mod(p[0], p[1])
				}
			},
			{
				">=",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.GreaterThanEqual(p[0], p[1])
				}
			},
			{
				">",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.GreaterThan(p[0], p[1])
				}
			},
			{
				"<",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.LessThan(p[0], p[1])
				}
			},
			{
				"<=",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.LessThanEqual(p[0], p[1])
				}
			},
			{
				"!=",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.NotEqual(p[0], p[1])
				}
			},
			{
				"<>",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.NotEqual(p[0], p[1])
				}
			},
			{
				"==",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Equal(p[0], p[1])
				}
			},
			{
				"&&",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.And(p[0], p[1])
				}
			},
			{
				"||",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math2.Or(p[0], p[1])
				}
			},
			{
				"^",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math.Pow(p[0], p[1])
				}
			},
			{
				"pow",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math.Pow(p[0], p[1]),
					IsFun = true
				}
			},
			{
				"abs",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Abs(p[0]),
					IsFun = true
				}
			},
			{
				"ceil",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Ceiling(p[0]),
					IsFun = true
				}
			},
			{
				"floor",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Floor(p[0]),
					IsFun = true
				}
			},
			{
				"trunc",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Truncate(p[0]),
					IsFun = true
				}
			},
			{
				"sqrt",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Sqrt(p[0]),
					IsFun = true
				}
			},
			{
				"sin",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Sin(p[0]),
					IsFun = true
				}
			},
			{
				"cos",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Cos(p[0]),
					IsFun = true
				}
			},
			{
				"tan",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Tan(p[0]),
					IsFun = true
				}
			},
			{
				"asin",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Asin(p[0]),
					IsFun = true
				}
			},
			{
				"acos",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Acos(p[0]),
					IsFun = true
				}
			},
			{
				"atan",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Atan(p[0]),
					IsFun = true
				}
			},
			{
				"asinh",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math2.Asinh(p[0]),
					IsFun = true
				}
			},
			{
				"acosh",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math2.Acosh(p[0]),
					IsFun = true
				}
			},
			{
				"atanh",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math2.Atanh(p[0]),
					IsFun = true
				}
			},
			{
				"sinh",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Sinh(p[0]),
					IsFun = true
				}
			},
			{
				"cosh",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Cosh(p[0]),
					IsFun = true
				}
			},
			{
				"tanh",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Tanh(p[0]),
					IsFun = true
				}
			},
			{
				"exp",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Exp(p[0]),
					IsFun = true
				}
			},
			{
				"log",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Log(p[0]),
					IsFun = true
				}
			},
			{
				"log10",
				new Operator
				{
					ParamCount = 1,
					MathFun = (double[] p) => Math.Log10(p[0]),
					IsFun = true
				}
			},
			{
				"max",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math.Max(p[0], p[1]),
					IsFun = true
				}
			},
			{
				"min",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math.Min(p[0], p[1]),
					IsFun = true
				}
			},
			{
				"round",
				new Operator
				{
					ParamCount = 2,
					MathFun = (double[] p) => Math.Round(p[0], (int)p[1]),
					IsFun = true
				}
			}
		};
		foreach (KeyValuePair<string, IOperator> @operator in Operators)
		{
			@operator.Value.Symbol = @operator.Key;
			@operator.Value.Priority = GetPriority(@operator.Key);
		}
	}

	public static int GetPriority(string operators)
	{
		switch (operators)
		{
		case "&&":
		case "||":
			return 0;
		case "==":
		case "!=":
		case "<>":
			return 1;
		case ">":
		case ">=":
		case "<":
		case "<=":
			return 2;
		case "+":
		case "-":
			return 3;
		case "*":
		case "/":
		case "%":
			return 4;
		case "^":
			return 5;
		case "negate":
			return 7;
		default:
			return 6;
		}
	}
}
