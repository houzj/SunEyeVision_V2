using Hik.Xml;

namespace IMVSFastFeatureMatchModuCs;

public class IMVSFeatureMatchUIData : BaseNode
{
	public string ModelName = string.Empty;

	public byte[] BaseInfo = null;

	public byte[] ImageData = null;

	public byte[] PointsData = null;

	public byte[] OtherMask = null;
}
