namespace App.XmlParser;

public class GroupOperator : IStackObject
{
	public string Symbol { get; set; }

	public StackObjectType Type
	{
		get
		{
			return StackObjectType.GroupOperator;
		}
		set
		{
		}
	}
}
