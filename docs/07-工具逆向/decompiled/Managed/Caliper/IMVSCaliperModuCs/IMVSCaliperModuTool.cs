using VM.Core;
using VM.PlatformSDKCS;

namespace IMVSCaliperModuCs;

/// \addtogroup 卡尺工具
/// @{
/// <summary>
/// @~chinese 卡尺工具工具 @~english Caliper Tool
/// </summary>
public class IMVSCaliperModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public CaliperResult ModuResult
	{
		get
		{
			_result = new CaliperResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (CaliperResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public CaliperParam ModuParams { get; set; }

	/// @cond
	public IMVSCaliperModuTool()
	{
		ModuParams = new CaliperParam(ID);
	}

	public IMVSCaliperModuTool(object param)
		: base(param, "IMVSCaliperModu")
	{
		ModuParams = new CaliperParam(ID);
	}

	public IMVSCaliperModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new CaliperParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}
}
