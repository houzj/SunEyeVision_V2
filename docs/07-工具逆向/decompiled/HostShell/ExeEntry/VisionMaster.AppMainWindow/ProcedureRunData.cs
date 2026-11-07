using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;

namespace VisionMaster.AppMainWindow;

public class ProcedureRunData : NotifyPropertyChangedBaseNode
{
	public static bool _bRunStrategyChange = false;

	private int _RunningModeIndex = 0;

	private List<ProcedureRunPolicy> procedureRunPolicyList = null;

	private bool _isProcesMulti = false;

	private uint _processMutiNumForxy = 5u;

	private uint _processMutiNum = 6u;

	private int cpuNum = -1;

	private ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY? procRunPolicy = null;

	private List<string> operationModeOptions = new List<string>
	{
		AppLocalizationService.GetLang("Normal Mode"),
		AppLocalizationService.GetLang("诊断模式")
	};

	private List<string> runPolicyOptions = new List<string>
	{
		AppLocalizationService.GetLang("默认"),
		AppLocalizationService.GetLang("自定义"),
		AppLocalizationService.GetLang("Lang_TimeStability")
	};

	public int RunningModeIndex
	{
		get
		{
			return _RunningModeIndex;
		}
		set
		{
			if (_RunningModeIndex != value)
			{
				_RunningModeIndex = value;
				OnPropertyChanged("RunningModeIndex");
			}
		}
	}

	public List<ProcedureRunPolicy> ProcedureRunPolicyList
	{
		get
		{
			if (procedureRunPolicyList == null)
			{
				procedureRunPolicyList = new List<ProcedureRunPolicy>();
				VmSolution.Instance.Modules.OfType<VmProcedure>().ToList().ForEach((VmProcedure x) =>
				{
					ProcedureRunPolicy item = new ProcedureRunPolicy
					{
						DecorateSource = x
					};
					procedureRunPolicyList.Add(item);
				});
			}
			return procedureRunPolicyList;
		}
	}

	public bool IsProcesMulti
	{
		get
		{
			return _isProcesMulti;
		}
		set
		{
			if (_isProcesMulti != value)
			{
				_isProcesMulti = value;
				SetProcessMultiInfo();
				OnPropertyChanged("IsProcesMulti");
			}
		}
	}

	public uint ProcessMutiNum
	{
		get
		{
			return _processMutiNum;
		}
		set
		{
			if (_processMutiNum != value)
			{
				_processMutiNum = value;
				SetProcessMultiInfo();
				OnPropertyChanged("ProcessMutiNum");
			}
		}
	}

	public bool RunStrategyEnable => RunStrategyIndex == 1;

	public int RunStrategyIndex
	{
		get
		{
			return ProcRunPolicy.Value.nRunPolicy - 1;
		}
		set
		{
			ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY value2 = ProcRunPolicy.Value;
			value2.nRunPolicy = value + 1;
			ProcRunPolicy = value2;
			OnPropertyChanged("RunStrategyEnable");
		}
	}

	public int CpuNum
	{
		get
		{
			if (cpuNum == -1)
			{
				ImvsSdkDefine.IMVS_PROCESSOR_INFO iMVS_PROCESSOR_INFO = default;
				iMVS_PROCESSOR_INFO = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetProcessOrInfo().Value;
				if (iMVS_PROCESSOR_INFO.nLogicProcessorNum <= 0)
				{
					throw new Exception("IMVS_GetProcessorInfo Error");
				}
				cpuNum = iMVS_PROCESSOR_INFO.nLogicProcessorNum;
				return iMVS_PROCESSOR_INFO.nLogicProcessorNum;
			}
			return cpuNum;
		}
	}

	public ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY? ProcRunPolicy
	{
		get
		{
			if (!procRunPolicy.HasValue)
			{
				List<VmProcedure> list = VmSolution.Instance.Modules.OfType<VmProcedure>().ToList();
				int num = list.Count;
				while (num > 0)
				{
					ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY iMVS_PROCEDURE_RUN_POLICY = new ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY
					{
						nCustomIndex = list.Count - num
					};
					iMVS_PROCEDURE_RUN_POLICY = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetProcedureRunPolicy().Value;
					num -= 32;
					SetProcRunPolicy(iMVS_PROCEDURE_RUN_POLICY);
				}
			}
			return procRunPolicy;
		}
		set
		{
			if (!value.HasValue)
			{
				procRunPolicy = null;
				return;
			}
			ref ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY? reference = ref procRunPolicy;
			if (!reference.HasValue || !reference.GetValueOrDefault().Equals(value))
			{
				SetProcRunPolicy(value);
				_bRunStrategyChange = true;
			}
		}
	}

	public List<string> OperationModeOptions => operationModeOptions;

	public List<string> RunPolicyOptions => runPolicyOptions;

	public bool IsRemoteMode => RemoteOperate.IsRemoteMode;

	private void SetProcessMultiInfo()
	{
		try
		{
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetSetProcedureReentrant(IsProcesMulti, _processMutiNumForxy);
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetModuleResultBuffer(IsProcesMulti, ProcessMutiNum);
		}
		catch (VmException ex)
		{
			LogHelper.Error("SetProcessMultiInfo is error:" + ex.errorMessage);
		}
	}

	private void GetProcessMultiInfo()
	{
		try
		{
			bool bReentrantEnable = false;
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetSetProcedureReentrant(ref bReentrantEnable, ref _processMutiNumForxy);
			bool bBufferEnable = false;
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetModuleResultBuffer(ref bBufferEnable, ref _processMutiNum);
			_isProcesMulti = bReentrantEnable;
			if (_isProcesMulti != bBufferEnable)
			{
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetModuleResultBuffer(_isProcesMulti, ProcessMutiNum);
			}
		}
		catch (VmException ex)
		{
			LogHelper.Error("GetProcessMultiInfo is error:" + ex.errorMessage);
		}
		OnPropertyChanged("IsProcesMulti");
		OnPropertyChanged("ProcessMutiNum");
	}

	public void ReloadProcRunStragety()
	{
		procedureRunPolicyList?.Clear();
		procedureRunPolicyList = null;
		procRunPolicy = null;
		Program.MainData.IsNormalMode = int.Parse(ConfigurationHelper.GetConfig("RunningMode")) != 1;
		if (Program.MainData.IsNormalMode)
		{
			RunningModeIndex = 0;
		}
		else
		{
			RunningModeIndex = 1;
		}
		operationModeOptions = new List<string>
		{
			AppLocalizationService.GetLang("Normal Mode"),
			AppLocalizationService.GetLang("诊断模式")
		};
		if (IsRemoteMode)
		{
			runPolicyOptions = new List<string>
			{
				AppLocalizationService.GetLang("默认"),
				AppLocalizationService.GetLang("自定义")
			};
		}
		else
		{
			runPolicyOptions = new List<string>
			{
				AppLocalizationService.GetLang("默认"),
				AppLocalizationService.GetLang("自定义"),
				AppLocalizationService.GetLang("Lang_TimeStability")
			};
		}
	}

	private void SetProcRunPolicy(ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY? temp)
	{
		procRunPolicy = temp;
		foreach (ProcedureRunPolicy proc in ProcedureRunPolicyList)
		{
			if (temp.Value.stCustomRunPolicy.stPolicyList.Any((ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY p) => p.nProcessID == proc.ID))
			{
				proc.ProcRunPolicy = temp.Value.stCustomRunPolicy.stPolicyList.FirstOrDefault((ImvsSdkDefine.IMVS_PROCEDURE_CUSTOM_RUN_POLICY p) => p.nProcessID == proc.ID);
			}
		}
		OnPropertyChanged("RunStrategyIndex");
		OnPropertyChanged("RunStrategyEnable");
	}

	public void SetRunPolicy2Server()
	{
		List<ProcedureRunPolicy> list = ProcedureRunPolicyList;
		for (int num = list.Count; num > 0; num -= 32)
		{
			int num2 = ((num > 32) ? 32 : num);
			for (int i = 0; i < num2; i++)
			{
				ProcRunPolicy.Value.stCustomRunPolicy.stPolicyList[i] = list[i + list.Count - num].ProcRunPolicy.Value;
			}
			ImvsSdkDefine.IMVS_PROCEDURE_RUN_POLICY value = ProcRunPolicy.Value;
			value.stCustomRunPolicy.nNum = num2;
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetProcedureRunPolicy(value);
		}
	}

	public void LoadRunPolicyConfig()
	{
		ReloadProcRunStragety();
		GetProcessMultiInfo();
	}

	public void LoadProcessConfig()
	{
		try
		{
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetSetProcedureReentrant(ref _isProcesMulti, ref _processMutiNumForxy);
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetModuleResultBuffer(_isProcesMulti, _processMutiNum);
		}
		catch (VmException ex)
		{
			LogHelper.Error("GetSetProcedureReentrant is error:" + ex.errorMessage);
		}
	}

	public void SetRunPolicyConfig()
	{
		if (_bRunStrategyChange)
		{
			SetRunPolicy2Server();
			_bRunStrategyChange = false;
		}
		ConfigurationHelper.SaveConfig("RunningMode", ((RunningModeIndex == 1) ? 1 : 0).ToString());
		Program.MainData.IsNormalMode = RunningModeIndex != 1;
		procedureRunPolicyList?.Clear();
		procedureRunPolicyList = null;
		ProcRunPolicy = null;
	}
}
