namespace Apps.XmlParser.ParamTab;

public class BooleanInt : Boolean
{
	public override string DisplayType => "Boolean";

	public override string Value
	{
		get
		{
			if (!CurValueA)
			{
				return "0";
			}
			return "1";
		}
		set
		{
			CurValueA = !string.IsNullOrEmpty(value) && value == "1";
		}
	}
}
