using Hik.Xml;

namespace FewShotCommonControl;

public class ModelData : BaseNode
{
	public bool IsExportToModel = false;

	public string TaskType = "0";

	public string ModeType = "0";

	public byte[] GalleryData = null;

	public byte[] ImagesData = null;

	public byte[] UICategoryData = null;
}
