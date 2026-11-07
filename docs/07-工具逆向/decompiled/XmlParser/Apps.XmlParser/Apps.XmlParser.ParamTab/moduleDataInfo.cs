using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class moduleDataInfo
{
	public int nCycleCount = 0;

	public ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data CurResultList;

	public bool IsSelfExecute { get; set; }
}
