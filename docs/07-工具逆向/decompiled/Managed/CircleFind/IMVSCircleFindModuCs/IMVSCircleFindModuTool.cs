using VM.Core;
using VM.PlatformSDKCS;

namespace IMVSCircleFindModuCs;

/// \addtogroup 圆查找
/// @{
/// <summary>
/// @~chinese 圆查找工具 @~english Circle Search Tool
/// </summary>
public class IMVSCircleFindModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public CircleFindResult ModuResult
	{
		get
		{
			_result = new CircleFindResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (CircleFindResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public CircleFindParam ModuParams { get; set; }

	/// @cond
	public IMVSCircleFindModuTool()
	{
		ModuParams = new CircleFindParam(ID);
	}

	public IMVSCircleFindModuTool(object param)
		: base(param, "IMVSCircleFindModu")
	{
		ModuParams = new CircleFindParam(ID);
	}

	public IMVSCircleFindModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new CircleFindParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}
}
