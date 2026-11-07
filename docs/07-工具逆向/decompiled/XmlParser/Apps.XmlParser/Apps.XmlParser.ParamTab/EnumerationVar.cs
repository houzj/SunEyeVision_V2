namespace Apps.XmlParser.ParamTab;

public class EnumerationVar : Enumeration
{
	public override string DisplayType
	{
		get
		{
			return "Enumeration";
		}
		set
		{
		}
	}

	public object ReservedProperty { get; set; }

	public uint BindSourceID { get; set; }

	public uint OldBindSourceID { get; set; }
}
