namespace Apps.XmlParser.ParamTab;

public interface IChecker
{
	void Check(object[] Params, string Value, string TargetName);
}
