using VM.Core;
using VM.PlatformSDKCS;

namespace IMVSDivideImageModuCs;

/// \addtogroup 划片拆分
/// @{
/// <summary>
/// @~chinese 划片拆分工具 @~english Divide Image Tool
/// </summary>
public class IMVSDivideImageModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public DivideImageResult ModuResult
	{
		get
		{
			_result = new DivideImageResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (DivideImageResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public DivideImageParam ModuParams { get; set; }

	/// @cond
	public IMVSDivideImageModuTool()
	{
		ModuParams = new DivideImageParam(ID);
	}

	public IMVSDivideImageModuTool(object param)
		: base(param, "IMVSDivideImageModu")
	{
		ModuParams = new DivideImageParam(ID);
	}

	public IMVSDivideImageModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new DivideImageParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}
}
