using System.Collections.Generic;
using System.Linq;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser;

public class VmIOManager
{
	public static List<string> GetLeafIOResultByCallBack(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data resultInfo, VmIO vmIO)
	{
		List<string> list = new List<string>();
		if (vmIO == null)
		{
			LogHelper.Error("leaf io is null");
			return list;
		}
		ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_EX_Data? iMVS_MODULE_RESULT_INFO_EX_Data = null;
		iMVS_MODULE_RESULT_INFO_EX_Data = resultInfo.pInfo.FirstOrDefault((ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_EX_Data p) => p.strParamName == vmIO.Name);
		if (!iMVS_MODULE_RESULT_INFO_EX_Data.HasValue)
		{
			LogHelper.Error("result is null");
			return list;
		}
		switch (vmIO.TypeName)
		{
		case "int":
			if (iMVS_MODULE_RESULT_INFO_EX_Data.Value.nParamType == 1 && iMVS_MODULE_RESULT_INFO_EX_Data.Value.nValueNum > 0)
			{
				list.AddRange(iMVS_MODULE_RESULT_INFO_EX_Data.Value.pIntValue.Select((int p) => p.ToString()));
			}
			break;
		case "float":
			if (iMVS_MODULE_RESULT_INFO_EX_Data.Value.nParamType == 2 && iMVS_MODULE_RESULT_INFO_EX_Data.Value.nValueNum > 0)
			{
				list.AddRange(iMVS_MODULE_RESULT_INFO_EX_Data.Value.pFloatValue.Select((float p) => p.ToString()));
			}
			break;
		case "string":
		{
			if (iMVS_MODULE_RESULT_INFO_EX_Data.Value.nParamType != 3 || iMVS_MODULE_RESULT_INFO_EX_Data.Value.nValueNum <= 0)
			{
				break;
			}
			for (int num2 = 0; num2 < iMVS_MODULE_RESULT_INFO_EX_Data.Value.nValueNum; num2++)
			{
				if (num2 < iMVS_MODULE_RESULT_INFO_EX_Data.Value.pStringValue.Length && !string.IsNullOrWhiteSpace(iMVS_MODULE_RESULT_INFO_EX_Data.Value.pStringValue[num2].strValue))
				{
					list.Add(iMVS_MODULE_RESULT_INFO_EX_Data.Value.pStringValue[num2].strValue);
				}
			}
			break;
		}
		case "byte":
		{
			if (iMVS_MODULE_RESULT_INFO_EX_Data.Value.nParamType != 6 || iMVS_MODULE_RESULT_INFO_EX_Data.Value.nValueNum <= 0)
			{
				break;
			}
			for (int num = 0; num < iMVS_MODULE_RESULT_INFO_EX_Data.Value.nValueNum; num++)
			{
				if (num < iMVS_MODULE_RESULT_INFO_EX_Data.Value.pByteValue.Length && !string.IsNullOrWhiteSpace(iMVS_MODULE_RESULT_INFO_EX_Data.Value.pByteValue[num].strValue))
				{
					list.Add(iMVS_MODULE_RESULT_INFO_EX_Data.Value.pByteValue[num].strValue);
				}
			}
			break;
		}
		default:
			LogHelper.Info("filter.ParamValueType is unknown");
			break;
		}
		return list;
	}
}
