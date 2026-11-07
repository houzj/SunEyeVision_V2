namespace App.XmlParser;

internal interface IOperator : IStackObject
{
	int Priority { get; set; }

	MathFun MathFun { get; set; }

	int ParamCount { get; set; }

	bool IsFun { get; set; }
}
