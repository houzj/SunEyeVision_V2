using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class SDKPathModelData : BaseNode
{
	public string ModEdgePointFindMode = string.Empty;

	public string ModEdgePointPolarity = string.Empty;

	public string ModEdgeStrength = string.Empty;

	public string ModCaliperHeight = string.Empty;

	public string ModCaliperWidth = string.Empty;

	public string ModHalfKernelSize = string.Empty;

	public string ModMaxPointNum = string.Empty;

	public string ModPointOffsetLen = string.Empty;

	public string ModelImageWidth = "0";

	public string ModelImageHeight = "0";

	public byte[] ModelImageData_ByteArray = null;

	public byte[] ModelTranj_ByteArray = null;

	public byte[] ModelProcessResult_ByteArray = null;
}
