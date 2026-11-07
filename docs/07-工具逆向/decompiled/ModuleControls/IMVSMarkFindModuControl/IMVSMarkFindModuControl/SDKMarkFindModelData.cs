using Hik.Xml;

namespace IMVSMarkFindModuControl;

public class SDKMarkFindModelData : BaseNode
{
	public string SDKClearModelData = "ClearCandidate";

	public byte[] AddSpareMatchModel_ByteArray = null;

	public byte[] AddSpareMarkModel_ByteArray = null;

	public string FeatureMatchType = string.Empty;
}
