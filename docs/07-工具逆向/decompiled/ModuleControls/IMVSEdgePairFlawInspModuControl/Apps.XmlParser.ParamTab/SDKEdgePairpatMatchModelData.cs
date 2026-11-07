using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class SDKEdgePairpatMatchModelData : BaseNode
{
	public string StartIndicate = string.Empty;

	public string AutoTrajAutoCfg = string.Empty;

	public string AutoTrajStartIdeaWidth = string.Empty;

	public string AutoTrajStartEdgeStrength = string.Empty;

	public string AutoTrajStartEdge0Polarity = string.Empty;

	public string AutoTrajStartEdge1Polarity = string.Empty;

	public string AutoTrajMaxWidthBias = string.Empty;

	public string AutoTrajSmoCoef = string.Empty;

	public string ModEdgePairFindMode = string.Empty;

	public string ModCaliperDistCourse = string.Empty;

	public string ModCaliperHeight = string.Empty;

	public string ModCaliperWidth = string.Empty;

	public string ModEdgePairIdealWidth = string.Empty;

	public string ModEdgeStrength = string.Empty;

	public string ModEdgePairEdge0Polarity = string.Empty;

	public string ModEdgePairEdge1Polarity = string.Empty;

	public string ModFineEdgeEnable = string.Empty;

	public string ModHalfKernelSize = string.Empty;

	public string ModelImageWidth = "0";

	public string ModelImageHeight = "0";

	public byte[] ModelImageData_ByteArray = null;

	public byte[] ModelImageMask_ByteArray = null;

	public string ClearModelImageMask = string.Empty;

	public byte[] ModelTranj_ByteArray = null;

	public string CreateModel = string.Empty;
}
