using Apps.ErrorCode;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class CountBottomCommand : BottomCommand
{
	public override string DisplayType
	{
		get
		{
			return "BottomCommand";
		}
		set
		{
		}
	}

	public void ClearTrackCountNum()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
		{
			IVmModule vmModule = algorithmTabRoot.Owner as IVmModule;
			try
			{
				((IUserStringData)vmModule.Params)["ClearTrackCountNum"] = string.Empty;
				HikMessageBox.Show("Lang_ClearTrackCountNumFinished", MessageIcons.Ok, isTranslate: true);
			}
			catch (VmException ex)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), MessageIcons.Error);
			}
		}
	}
}
