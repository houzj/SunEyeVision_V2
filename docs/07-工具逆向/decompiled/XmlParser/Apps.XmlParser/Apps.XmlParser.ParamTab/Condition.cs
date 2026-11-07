using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class Condition : BaseNode
{
	public string Property;

	public string Value;

	public string PropertyA
	{
		get
		{
			return Property;
		}
		set
		{
			Property = value;
		}
	}

	public string ValueA
	{
		get
		{
			return Value;
		}
		set
		{
			Value = value;
		}
	}
}
