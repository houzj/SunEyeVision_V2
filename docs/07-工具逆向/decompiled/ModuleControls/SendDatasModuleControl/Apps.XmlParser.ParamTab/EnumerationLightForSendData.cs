using VMControls.WPF;

namespace Apps.XmlParser.ParamTab;

public class EnumerationLightForSendData : EnumerationLight
{
	public override string DisplayType => "EnumerationLight";

	public override bool OpenGlobalLight()
	{
		if (!base.OpenGlobalLight())
		{
			ToolControl.CommSet();
		}
		return true;
	}
}
