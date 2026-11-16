using VM.Core;
using VM.PlatformSDKCS;

namespace IMVSBinaryModuCs;

/// \addtogroup 图像二值化
/// @{
/// <summary>
/// @~chinese 图像二值化工具 @~english Image Binarization Tool
/// </summary>
public class IMVSBinaryModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public ImageBinaryResult ModuResult
	{
		get
		{
			_result = new ImageBinaryResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (ImageBinaryResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public ImageBinaryParam ModuParams { get; set; }

	/// @cond
	public IMVSBinaryModuTool()
	{
		ModuParams = new ImageBinaryParam(ID);
	}

	public IMVSBinaryModuTool(object param)
		: base(param, "IMVSBinaryModu")
	{
		ModuParams = new ImageBinaryParam(ID);
	}

	public IMVSBinaryModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new ImageBinaryParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}
}
