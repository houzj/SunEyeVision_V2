using System.Collections.ObjectModel;
using Apps.Data;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace VisionMaster.AppMainWindow;

public class ProcedureRunPolicy : NotifyPropertyChangedBase, IDecorate
{
	private ObservableCollection<CPURelateInfo> runStrategyCpus = null;

	private ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY? procRunPolicy = null;

	public object DecorateSource { get; set; }

	public VmProcedure Owner
	{
		get
		{
			if (DecorateSource != null && DecorateSource is VmProcedure)
			{
				return DecorateSource as VmProcedure;
			}
			return null;
		}
	}

	public string strDisplayName => Owner?.Name;

	public int ID => (int)(Owner?.ID).Value;

	public uint RunStrategyThreadNum
	{
		get
		{
			return (uint)ProcRunPolicy.Value.nThreadNum;
		}
		set
		{
			ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY value2 = ProcRunPolicy.Value;
			value2.nThreadNum = (int)value;
			ProcRunPolicy = value2;
		}
	}

	public ObservableCollection<CPURelateInfo> RunStrategyCpus
	{
		get
		{
			if (runStrategyCpus == null)
			{
				runStrategyCpus = new ObservableCollection<CPURelateInfo>();
				for (uint num = 0u; num < Program.MainData.ProcRunPolicyData.CpuNum; num++)
				{
					runStrategyCpus.Add(new CPURelateInfo
					{
						owner = ProcRunPolicy,
						Id = num
					});
				}
			}
			return runStrategyCpus;
		}
		set
		{
			if (runStrategyCpus != value)
			{
				runStrategyCpus = value;
				OnPropertyChanged("RunStrategyCpus");
			}
		}
	}

	public ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY? ProcRunPolicy
	{
		get
		{
			if (!procRunPolicy.HasValue)
			{
				for (int i = 0; i < Program.MainData.ProcRunPolicyData.ProcRunPolicy.Value.stCustomRunPolicy.nNum; i++)
				{
					if (Program.MainData.ProcRunPolicyData.ProcRunPolicy.Value.stCustomRunPolicy.stPolicyList[i].nProcessID == Owner?.ID)
					{
						procRunPolicy = Program.MainData.ProcRunPolicyData.ProcRunPolicy.Value.stCustomRunPolicy.stPolicyList[i];
					}
				}
				if (!procRunPolicy.HasValue)
				{
					procRunPolicy = new ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY
					{
						nProcessID = (int)(Owner?.ID).Value,
						nThreadNum = Program.MainData.ProcRunPolicyData.CpuNum,
						chCpuCore = new byte[1024]
					};
					int num = Program.MainData.ProcRunPolicyData.CpuNum;
					if (num > 1024)
					{
						LogHelper.Error($"cpu num {num} is more than {1024}");
						num = 1024;
					}
					for (int j = 0; j < num; j++)
					{
						procRunPolicy.Value.chCpuCore[j] = 1;
					}
				}
				OnPropertyChanged("RunStrategyThreadNum");
			}
			return procRunPolicy;
		}
		set
		{
			ref ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY? reference = ref procRunPolicy;
			if (!reference.HasValue || !reference.GetValueOrDefault().Equals(value))
			{
				procRunPolicy = value;
				OnPropertyChanged("RunStrategyThreadNum");
				RunStrategyCpus = null;
				ProcedureRunData._bRunStrategyChange = true;
			}
		}
	}
}
