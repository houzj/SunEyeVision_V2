using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public interface ROIStructInterface
{
	IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec);
}
