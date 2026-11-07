namespace App.XmlParser;

public class Operator : IOperator, IStackObject
{
	public string Symbol { get; set; }

	public int ParamCount { get; set; }

	public int Priority { get; set; }

	public MathFun MathFun { get; set; }

	public bool IsFun { get; set; }

	public StackObjectType Type
	{
		get
		{
			return StackObjectType.Operator;
		}
		set
		{
		}
	}
}
