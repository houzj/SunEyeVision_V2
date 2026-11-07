using Apps.Data;
using VM.PlatformSDKCS;

namespace VisionMaster.AppMainWindow;

public class CPURelateInfo : NotifyPropertyChangedBaseNode
{
	public ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY? owner = null;

	public string Name => "CPU" + Id;

	public uint Id { get; set; } = 0u;

	public bool IsRelated
	{
		get
		{
			return owner.Value.chCpuCore[Id] == 1;
		}
		set
		{
			owner.Value.chCpuCore[Id] = (byte)(value ? 1u : 0u);
			OnPropertyChanged("IsRelated");
			ProcedureRunData._bRunStrategyChange = true;
		}
	}
}
