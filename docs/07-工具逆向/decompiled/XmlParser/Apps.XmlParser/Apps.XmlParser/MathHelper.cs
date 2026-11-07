using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Apps.XmlParser;

public class MathHelper
{
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
		return !regex.IsMatch(strNumber) && !regex2.IsMatch(strNumber) && !regex3.IsMatch(strNumber) && regex4.IsMatch(strNumber);
	}

	public static bool IsConst(string strNumber)
	{
		return IsInt(strNumber) || IsFloat(strNumber) || IsString(strNumber);
	}

	public static bool IsInt(string strValue)
	{
		Regex regex = new Regex("^[-|+]?[0-9]+$");
		return regex.IsMatch(strValue);
	}

	public static bool IsFloat(string strValue)
	{
		Regex regex = new Regex("^([-|+]?[0-9]+)([.]?[0-9]*)$");
		return regex.IsMatch(strValue);
	}

	public static bool IsString(string strValue)
	{
		Regex regex = new Regex("^[A-Za-z0-9\\u4e00-\\u9fa5_,]*$");
		return regex.IsMatch(strValue);
	}

	public static bool IsNameString(string strValue)
	{
		Regex regex = new Regex("^[A-Za-z0-9_]*$");
		return regex.IsMatch(strValue);
	}

	public static bool IsSubscribeString(string strValue)
	{
		if (string.IsNullOrWhiteSpace(strValue))
		{
			return false;
		}
		try
		{
			if ("TRIGGER_STRING[]" == strValue || "TRIGGER_STRING" == strValue)
			{
				return true;
			}
			if (1 != strValue.Count((char p) => p == '[') || 1 != strValue.Count((char p) => p == ']'))
			{
				return false;
			}
			if (!strValue.EndsWith("]"))
			{
				return false;
			}
			int num = -1;
			int num2 = -1;
			num = strValue.IndexOf('[');
			num2 = strValue.IndexOf(']');
			bool flag = false;
			if (num + 1 == num2)
			{
				flag = IsModuleFilterString(strValue.Substring(0, num));
			}
			else
			{
				string strValue2 = strValue.Substring(0, num);
				string strValue3 = strValue.Substring(num + 1, num2 - num - 1);
				flag = IsModuleFilterString(strValue2) && IsModuleFilterIndex(strValue3);
			}
			return flag;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool IsModuleFilterString(string strValue)
	{
		if (string.IsNullOrWhiteSpace(strValue))
		{
			return false;
		}
		string s = strValue.Substring(0, strValue.IndexOf(" "));
		bool flag;
		try
		{
			flag = uint.TryParse(s, out var _);
		}
		catch (Exception)
		{
			flag = false;
		}
		if (!flag)
		{
			return false;
		}
		if (1 != strValue.Count((char p) => p == '.'))
		{
			return false;
		}
		int num = strValue.IndexOf(".");
		if (0 >= num || num >= strValue.Length)
		{
			return false;
		}
		return true;
	}

	public static bool IsModuleFilterIndex(string strValue)
	{
		if (string.IsNullOrWhiteSpace(strValue))
		{
			return false;
		}
		if (IsUint(strValue))
		{
			return true;
		}
		if (IsModuleLoopIndex(strValue))
		{
			return true;
		}
		if (IsModuleFilterString(strValue))
		{
			return true;
		}
		return false;
	}

	private static bool IsModuleLoopIndex(string strValue)
	{
		if (string.IsNullOrWhiteSpace(strValue))
		{
			return false;
		}
		if (1 != strValue.Count((char p) => p == '.'))
		{
			return false;
		}
		int num = strValue.IndexOf(".");
		if (0 >= num || num >= strValue.Length)
		{
			return false;
		}
		string text = strValue.Substring(num + 1);
		if ("LoopIndex" != text)
		{
			return false;
		}
		return true;
	}

	private static bool IsUint(string strValue)
	{
		bool flag = false;
		try
		{
			uint result;
			return uint.TryParse(strValue, out result);
		}
		catch (Exception)
		{
			return false;
		}
	}
}
