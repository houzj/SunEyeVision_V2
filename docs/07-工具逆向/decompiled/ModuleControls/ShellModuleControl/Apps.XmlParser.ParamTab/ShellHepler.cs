using System;
using System.CodeDom.Compiler;

namespace Apps.XmlParser.ParamTab;

public class ShellHepler
{
	private static CodeDomProvider provider = CodeDomProvider.CreateProvider("C#");

	public static bool IsValidIdentifier(string str)
	{
		try
		{
			if (string.IsNullOrEmpty(str))
			{
				return false;
			}
			return !provider.IsValidIdentifier(str);
		}
		catch (Exception)
		{
			return false;
		}
	}
}
