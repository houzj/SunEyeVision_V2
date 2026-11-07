using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class SDKEdgepatMatchModelData : BaseNode
{
	public string ModEdgePointFindMode = string.Empty;

	public string ModEdgePointEdgePolarity = string.Empty;

	public string ModEdgeStrength = string.Empty;

	public string ModCaliperHeight = string.Empty;

	public string ModCaliperWidth = string.Empty;

	public string ModCaliperDist = string.Empty;

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
