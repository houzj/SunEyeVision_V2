using System.Collections.Generic;

namespace PyShellModuleControl;

public class ShellDefine
{
	public const string INT = "int";

	public const string INTARRAY = "int[]";

	public const string FLOAT = "float";

	public const string FLOATARRAY = "float[]";

	public const string STRING = "string";

	public const string STRINGARRAY = "string[]";

	public const string BYTE = "byte";

	public const string IMAGE = "IMAGE";

	public const string ROIBOX = "ROIBOX";

	public const string ROIANNULUS = "ROIANNULUS";

	public const string ANNULUS = "ANNULUS";

	public const string ROIPOLYGON = "ROIPOLYGON";

	public const string POLYGON = "POLYGON";

	public const string POINT = "POINT";

	public const string LINE = "LINE";

	public const string FIXTURE = "FIXTURE";

	public const string CIRCLE = "CIRCLE";

	public const string Rect = "Rect";

	public const string RECT = "RECT";

	public const string ELLIPSE = "ELLIPSE";

	public const string POINTSET = "pointset";

	public static List<string> PYTHON_KEY_WORDS = new List<string>
	{
		"and", "as", "assert", "break", "class", "continue", "def", "del", "elif", "else",
		"except", "exec", "finaily", "for", "from", "global", "if", "import", "in", "is",
		"lambda", "not", "or", "pass", "print", "raise", "return", "try", "while", "with",
		"yield"
	};
}
