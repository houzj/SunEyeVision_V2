namespace Apps.XmlParser.ParamTab;

public interface IDynamic
{
	void RemoveIO(object obj);

	object AddIO(string name = null, string type = null, int index = -1);
}
