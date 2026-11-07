namespace Apps.XmlParser.ParamTab;

public interface ISubChild : ISubInfoCollection
{
	NameValueItem GetChildByName(string name);
}
