using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class SubscribeParam : ISubParam
{
	public string Type { get; set; }

	public string CurValue { get; set; }

	public string ParamName { get; set; }
}
