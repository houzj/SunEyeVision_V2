using System;
using System.Collections.Generic;
using System.Linq;

namespace App.XmlParser;

public class Calculator
{
	private static readonly ExpressBuilder ExpressBuilder = new ExpressBuilder();

	private double Compute(List<IStackObject> rpnlist)
	{
		if (rpnlist.Count == 0)
		{
			throw new Exception("无算数因子！");
		}
		if (rpnlist[0].Symbol != "#")
		{
			throw new Exception("缺少开始运算符，请确认该表达式式是否已经使用！");
		}
		rpnlist.RemoveAt(0);
		if (rpnlist.Count == 0)
		{
			throw new Exception("无算数因子！");
		}
		for (IStackObject stackObject = rpnlist.FirstOrDefault((IStackObject p) => p.Type == StackObjectType.Operator); stackObject != null; stackObject = rpnlist.FirstOrDefault((IStackObject p) => p.Type == StackObjectType.Operator))
		{
			int num = rpnlist.IndexOf(stackObject);
			Operator obj = stackObject as Operator;
			int num2 = num - obj.ParamCount;
			if (num2 < 0)
			{
				throw new Exception("运算错误！");
			}
			List<Operand> list = new List<Operand>();
			for (int num3 = num2; num3 < num; num3++)
			{
				if (!(rpnlist[num3] is Operand item))
				{
					throw new Exception($"运算符{obj.Symbol}计算的对象不应为{rpnlist[num3].Symbol}");
				}
				list.Add(item);
			}
			double value = obj.MathFun(list.Select((Operand p) => p.Value).ToArray());
			(rpnlist[num2] as Operand).Value = value;
			for (int num4 = num - num2; num4 > 0; num4--)
			{
				rpnlist.RemoveAt(num2 + 1);
			}
		}
		if (rpnlist.Count != 1)
		{
			throw new Exception("运算出错！");
		}
		if (!(rpnlist[0] is Operand operand))
		{
			throw new Exception("运算出错！");
		}
		return operand.Value;
	}

	public double Compute(ExpressBuilder builder, out bool isOk, out string msg)
	{
		msg = string.Empty;
		isOk = true;
		try
		{
			double num = Compute(builder.RpnList);
			if (double.IsNaN(num) || double.IsInfinity(num))
			{
				throw new Exception("非法值");
			}
			return num;
		}
		catch (Exception ex)
		{
			isOk = false;
			msg = ex.Message;
		}
		return 0.0;
	}

	public double Compute(string expression, out bool isOk, out string msg)
	{
		isOk = ExpressBuilder.Parse(expression, out msg);
		return (!isOk) ? 0.0 : Compute(ExpressBuilder, out isOk, out msg);
	}
}
