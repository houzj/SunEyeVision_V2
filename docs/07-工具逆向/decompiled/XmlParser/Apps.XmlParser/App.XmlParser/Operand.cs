namespace App.XmlParser;

public class Operand : IOperand, IStackObject
{
	public string Symbol { get; set; }

	public double Value { get; set; }

	public StackObjectType Type
	{
		get
		{
			return StackObjectType.Operand;
		}
		set
		{
		}
	}
}
