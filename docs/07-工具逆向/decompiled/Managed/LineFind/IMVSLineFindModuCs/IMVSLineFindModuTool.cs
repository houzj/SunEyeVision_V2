using VM.Core;
using VM.PlatformSDKCS;

namespace IMVSLineFindModuCs;

/// \addtogroup 直线查找
/// @{
/// <summary>
/// @~chinese 直线查找工具 @~english Line Search Tool
/// </summary>
public class IMVSLineFindModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public LineFindResult ModuResult
	{
		get
		{
			_result = new LineFindResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (LineFindResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public LineFindParam ModuParams { get; set; }

	/// @cond
	public IMVSLineFindModuTool()
	{
		ModuParams = new LineFindParam(ID);
	}

	public IMVSLineFindModuTool(object param)
		: base(param, "IMVSLineFindModu")
	{
		ModuParams = new LineFindParam(ID);
	}

	public IMVSLineFindModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new LineFindParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}
}
