using Apps.Data;
using Apps.Localization;
using Apps.Log;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class BooleanLx : BooleanBx
{
	public override int SetParamUnit()
	{
		if (MyItemState == ItemState.Normal)
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				int num = ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData(vmModule.ID, "ConnectIP", StringUtil.UTF8GetBytesPadZero(Value), 60000u);
				if (num != 0)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CamLinkFail"), MessageIcons.Error);
					LogHelper.Error("相机连接失败 ," + num);
				}
			}
			(GetTopBaseNode() as AlgorithmTabRoot).GetItemsValueUnit();
			return 0;
		}
		return -1;
	}
}
