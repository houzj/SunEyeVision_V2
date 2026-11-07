using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class EnumEntryLx : BaseNode
{
	public string Description;

	public string DisplayName;

	public int Value;

	public string StringValue;

	public string DescriptionA
	{
		get
		{
			return Description;
		}
		set
		{
			Description = value;
		}
	}

	public string DisplayNameA
	{
		get
		{
			return DisplayName;
		}
		set
		{
			DisplayName = value;
		}
	}

	public int ValueA
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

	public string StringValueA
	{
		get
		{
			return StringValue;
		}
		set
		{
			StringValue = value;
		}
	}

	public override string ToString()
	{
		return DisplayName;
	}
}
