using VMControls.WPF;

namespace Apps.XmlParser.ParamTab;

public class EnumerationCameraForAcq : Enumeration_ModuleSelector
{
	public override bool OpenGlobalCamera()
	{
		if (!base.OpenGlobalCamera())
		{
			ToolControl.CameraSet();
		}
		return true;
	}
}
