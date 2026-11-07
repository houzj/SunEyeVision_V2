using System.Linq;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class LightSourceHelper
{
	public const int BRAND_NAME_LEN = 256;

	public const int DEVICE_NAME_LEN = 256;

	public const int TYPE_NONE = 0;

	public const int TYPE_SVC2500 = 8917764;

	public const int DEVICE_IO_MAX_NUMBER = 20;

	public const int TYPE_LIGHT_CONTROLLER = 101;

	public const int TYPE_LIGHT_6C = 102;

	public const int TYPE_VB2200 = 103;

	public const int TYPE_VB2230 = 104;

	public const int TYPE_VC2000 = 105;

	public const int TYPE_VC3000_IO = 106;

	public const int TYPE_VC3000_LIGHT = 107;

	public const int TYPE_GPIO = 108;

	public const int TYPE_VC3000H = 109;

	public const int TYPE_VC4000 = 110;

	public const int TYPE_VC5000 = 114;

	public static bool IsExitOldLightMoudle => VmSolution.Instance.Modules.FirstOrDefault((VmModule m) => m.ID == 14000) != null;

	public static int GetNewLightTypeByOldLightType(int type)
	{
		if (!IsExitOldLightMoudle)
		{
			return type;
		}
		return type switch
		{
			4100 => 101, 
			4456705 => 103, 
			4456706 => 104, 
			8913412 => 110, 
			516 => 107, 
			8912896 => 106, 
			3670016 => 108, 
			25690628 => 105, 
			47120904 => 114, 
			_ => type, 
		};
	}
}
