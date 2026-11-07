using System;
using System.Collections.Generic;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class GroupOutputParam : GroupParam
{
	private const string _DefaultStartName = "out";

	private Dictionary<string, uint> ioModes;

	public override string DisplayType => "GroupInputParam";

	public override VmIOCollection TargetVmIOs => Owner?.Outputs;

	public override string DefaultStartName => "out";

	public override string CurrentParamName => "DynamicOutputParam";

	public Dictionary<string, uint> IOModes
	{
		get
		{
			if (Owner != null && Owner is IMVSGroup && ioModes == null)
			{
				ioModes = new Dictionary<string, uint>();
				ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO_LIST stGroupIOModeList = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO_LIST
				{
					nGroupId = Owner.ID,
					stGroupIOMode = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO[1024]
				};
				try
				{
					ServerSDKManager.serverSDKManager.mModuleManager.GetGroupIOReportMode(ref stGroupIOModeList);
					for (int i = 0; i < stGroupIOModeList.nIONum; i++)
					{
						string key = StringUtil.UTF8GetString(stGroupIOModeList.stGroupIOMode[i].strParamName);
						ioModes[key] = stGroupIOModeList.stGroupIOMode[i].nMode;
					}
				}
				catch (VmException ex)
				{
					int errorCode = ex.errorCode;
					string message = AppLocalizationService.GetLang("errCode:") + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(errorCode);
					LogHelper.Error(message);
				}
				catch (Exception ex2)
				{
					LogHelper.Error("GetGroupIOReportMode error: " + ex2.Message + Environment.NewLine + ex2.StackTrace);
				}
			}
			return ioModes;
		}
	}
}
