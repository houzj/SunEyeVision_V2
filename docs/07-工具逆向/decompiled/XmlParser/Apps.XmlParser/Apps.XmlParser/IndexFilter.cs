namespace Apps.XmlParser;

public class IndexFilter
{
	public string Index = "";

	public Filter Filter = null;

	public string Name = "";

	public IndexFilter(string index, Filter filter)
	{
		Index = index;
		Filter = filter;
	}
}
