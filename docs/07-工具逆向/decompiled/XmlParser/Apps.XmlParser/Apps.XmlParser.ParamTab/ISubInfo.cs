namespace Apps.XmlParser.ParamTab;

public interface ISubInfo
{
	bool IsSubA { get; set; }

	ButtonSelecter SubData { get; }

	string ComType { get; set; }

	void SetInit();
}
