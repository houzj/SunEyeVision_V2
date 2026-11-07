using VM.Core;
using VM.PlatformSDKCS;

namespace IMVSBlobFindModuCs;

/// \addtogroup BLOB分析
/// @{
/// <summary>
/// @~chinese BLOB分析工具 @~english Blob Analysis Tool
/// </summary>
public class IMVSBlobFindModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public BlobFindResult ModuResult
	{
		get
		{
			_result = new BlobFindResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (BlobFindResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public BlobFindParam ModuParams { get; set; }

	/// @cond
	public IMVSBlobFindModuTool()
	{
		ModuParams = new BlobFindParam(ID);
	}

	public IMVSBlobFindModuTool(object param)
		: base(param, "IMVSBlobFindModu")
	{
		ModuParams = new BlobFindParam(ID);
	}

	public IMVSBlobFindModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new BlobFindParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}
}
