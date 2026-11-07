using System.Runtime.InteropServices;
using System.Text;
using ModelCommonHelper;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace IMVSFastFeatureMatchModuCs;

/// \addtogroup 快速匹配
/// @{
/// <summary>
/// @~chinese 快速匹配工具 @~english Fast Match Tool
/// </summary>
public class IMVSFastFeatureMatchModuTool : VmModule
{
	/// @endcond
	private CModuleResultBase _result;

	private const int Max_Model_Count = 36;

	/// <summary>
	/// @~chinese 模块结果对象 @~english Module Result Object
	/// </summary>
	public FastFeatureMatchResult ModuResult
	{
		get
		{
			_result = new FastFeatureMatchResult();
			ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX? iMVS_MODULE_RESULT_INFO_LIST_EX = ServerSDKManager.serverSDKManager.mModuleManager.QueryModuleOneResult(ID);
			if (iMVS_MODULE_RESULT_INFO_LIST_EX.HasValue)
			{
				ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX stModuleResult = iMVS_MODULE_RESULT_INFO_LIST_EX.Value;
				ServerSDKManager.serverSDKManager.mModuleResultManager.SetModuleResult(ref stModuleResult, ref _result, ModuResultMemType);
			}
			return (FastFeatureMatchResult)_result;
		}
	}

	/// <summary>
	/// @~chinese 模块参数对象 @~english Module Param Object
	/// </summary>
	public FastFeatureMatchParam ModuParams { get; set; }

	/// @cond
	public override bool IsContainsModel => true;

	/// @cond
	public IMVSFastFeatureMatchModuTool()
	{
		ModuParams = new FastFeatureMatchParam(ID);
	}

	public IMVSFastFeatureMatchModuTool(object param)
		: base(param, "IMVSFastFeatureMatchModu")
	{
		ModuParams = new FastFeatureMatchParam(ID);
	}

	public IMVSFastFeatureMatchModuTool(ISubjectModel proxyModule)
		: base(proxyModule)
	{
		ModuParams = new FastFeatureMatchParam(ID);
	}

	/// <summary>
	/// @~chinese 模块自执行 @~english Module Self Run
	/// </summary>
	public new void Run()
	{
		ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID);
	}

	public override byte[] GetModel()
	{
		byte[] result = null;
		try
		{
			int num = 0;
			IMVSFastFeatureMatchModelData iMVSFastFeatureMatchModelData = new IMVSFastFeatureMatchModelData();
			while (true)
			{
				try
				{
					IMVSFeatureMatchModelData iMVSFeatureMatchModelData = new IMVSFeatureMatchModelData();
					IMVSFeatureMatchUIData iMVSFeatureMatchUIData = new IMVSFeatureMatchUIData();
					string modelName = ((IUserStringData)Params)["CandidateName" + num];
					byte[] array = ((IUserBytesData)Params)["CandidateData" + num];
					if (array != null)
					{
						iMVSFeatureMatchModelData.ModelName = modelName;
						iMVSFeatureMatchModelData.ModelData = array;
						iMVSFeatureMatchUIData.ModelName = modelName;
						string text = "SolModelFile_" + num;
						string key = text + "_BasicData";
						iMVSFeatureMatchUIData.BaseInfo = UserData[key];
						key = text + "_CurImageData";
						iMVSFeatureMatchUIData.ImageData = UserData[key];
						key = text + "_OtherMask";
						iMVSFeatureMatchUIData.OtherMask = UserData[key];
						key = text + "_PointsData";
						iMVSFeatureMatchUIData.PointsData = UserData[key];
						iMVSFastFeatureMatchModelData.Models.Add(iMVSFeatureMatchModelData);
						iMVSFastFeatureMatchModelData.UIDatas.Add(iMVSFeatureMatchUIData);
						num++;
						continue;
					}
				}
				catch
				{
				}
				break;
			}
			if (iMVSFastFeatureMatchModelData.Models.Count == 0 || iMVSFastFeatureMatchModelData.UIDatas.Count == 0 || iMVSFastFeatureMatchModelData.Models.Count != iMVSFastFeatureMatchModelData.UIDatas.Count)
			{
				result = null;
			}
			else
			{
				string s = iMVSFastFeatureMatchModelData.ToXmlStringWithHead();
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				result = EncryptionAndDecryptionHelper.GetEncryptedData(bytes);
			}
		}
		catch
		{
		}
		return result;
	}

	public override void ImportModel(object model)
	{
		((IUserStringData)Params)["ClearCandidate"] = "";
		for (int i = 0; i < 36; i++)
		{
			try
			{
				string text = "SolModelFile_" + i;
				string key = text + "_BasicData";
				if (UserData[key] == null)
				{
					break;
				}
				UserData[key] = null;
				key = text + "_CurImageData";
				UserData[key] = null;
				key = text + "_OtherMask";
				UserData[key] = null;
				key = text + "_PointsData";
				UserData[key] = null;
				continue;
			}
			catch
			{
			}
			break;
		}
		if (model == null)
		{
			return;
		}
		IMVSFastFeatureMatchModelData iMVSFastFeatureMatchModelData = new IMVSFastFeatureMatchModelData();
		try
		{
			byte[] decryptedData = EncryptionAndDecryptionHelper.GetDecryptedData((byte[])model);
			iMVSFastFeatureMatchModelData.loadXml(decryptedData);
			for (int j = 0; j < iMVSFastFeatureMatchModelData.Models.Count; j++)
			{
				((IUserStringData)Params)["SpareModelName"] = (iMVSFastFeatureMatchModelData.Models[j] as IMVSFeatureMatchModelData).ModelName;
				((IUserBytesData)Params)["AddSpareModel"] = (iMVSFastFeatureMatchModelData.Models[j] as IMVSFeatureMatchModelData).ModelData;
			}
			for (int k = 0; k < iMVSFastFeatureMatchModelData.UIDatas.Count; k++)
			{
				string text2 = "SolModelFile_" + k;
				string key2 = text2 + "_BasicData";
				IMVSFeatureMatchUIData iMVSFeatureMatchUIData = iMVSFastFeatureMatchModelData.UIDatas[k] as IMVSFeatureMatchUIData;
				if (iMVSFeatureMatchUIData.BaseInfo != null)
				{
					UserData[key2] = iMVSFeatureMatchUIData.BaseInfo;
				}
				if (iMVSFeatureMatchUIData.ImageData != null)
				{
					key2 = text2 + "_CurImageData";
					UserData[key2] = iMVSFeatureMatchUIData.ImageData;
				}
				if (iMVSFeatureMatchUIData.OtherMask != null)
				{
					key2 = text2 + "_OtherMask";
					UserData[key2] = iMVSFeatureMatchUIData.OtherMask;
				}
				if (iMVSFeatureMatchUIData.PointsData != null)
				{
					key2 = text2 + "_PointsData";
					UserData[key2] = iMVSFeatureMatchUIData.PointsData;
				}
			}
		}
		catch
		{
		}
	}

	/// @endcond
	/// <summary>
	/// @~chinese 导入数据 @~english Import Module Data
	/// </summary>
	/// <param name="strPaths">文件路径</param>
	public void ImportModelData(string[] strPaths)
	{
		if (strPaths == null)
		{
			throw new VmException(-536870902);
		}
		int num = strPaths.Length;
		if (num <= 0 || num > 16)
		{
			throw new VmException(-536870911);
		}
		int num2 = 0;
		ImvsSdkDefine.IMVS_IMPORT_MODULE_DATA_INPUT pstImportData = new ImvsSdkDefine.IMVS_IMPORT_MODULE_DATA_INPUT
		{
			nModuleID = ID,
			nDataType = 1u,
			nDataNum = (uint)num,
			stImportModuData = new ImvsSdkDefine.IMVS_PF_IMPORT_MODULE_DATA[16]
		};
		for (int i = 0; i < num; i++)
		{
			if (string.IsNullOrEmpty(strPaths[i]))
			{
				num2 = -536870902;
				break;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(strPaths[i]);
			pstImportData.stImportModuData[i].nDataLen = (uint)(bytes.Length + 1);
			pstImportData.stImportModuData[i].pData = Marshal.AllocHGlobal((int)pstImportData.stImportModuData[i].nDataLen);
			Marshal.Copy(bytes, 0, pstImportData.stImportModuData[i].pData, bytes.Length);
			Marshal.WriteByte(pstImportData.stImportModuData[i].pData, bytes.Length, 0);
		}
		if (num2 == 0)
		{
			num2 = ServerSDKAPI.IMVS_ImportModuleData(CVmObjectBase.mhandle, pstImportData);
		}
		for (int j = 0; j < num; j++)
		{
			Marshal.FreeHGlobal(pstImportData.stImportModuData[j].pData);
		}
		if (num2 != 0)
		{
			throw new VmException(num2);
		}
	}

	/// <summary>
	/// @~chinese 清除所有模型数据 @~english Clear all model data
	/// </summary>
	public void ClearModelData()
	{
		int num = ServerSDKAPI.IMVS_SetParamValue(CVmObjectBase.mhandle, ID, "ClearCandidate", "");
		if (num != 0)
		{
			throw new VmException(num);
		}
	}
}
