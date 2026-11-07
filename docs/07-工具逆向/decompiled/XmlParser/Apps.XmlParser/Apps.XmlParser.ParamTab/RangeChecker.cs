using System;
using Apps.Localization;

namespace Apps.XmlParser.ParamTab;

public class RangeChecker : IChecker
{
	public void Check(object[] Params, string Value, string TargetName)
	{
		if (Params.Length < 0)
		{
			throw new Exception(TargetName + AppLocalizationService.GetLang("Lang_ParamErr"));
		}
		double num = Convert.ToDouble(Params[0]);
		double num2 = Convert.ToDouble(Params[1]);
		if (double.TryParse(Value, out var result))
		{
			if (num - result > 1E-08 || result - num2 > 1E-08)
			{
				string message = TargetName + " :" + Value + AppLocalizationService.GetLang("参数越界") + "(" + num + "," + num2 + ")！";
				throw new Exception(message);
			}
			return;
		}
		throw new Exception(TargetName + AppLocalizationService.GetLang("Lang_ParamErr"));
	}
}
