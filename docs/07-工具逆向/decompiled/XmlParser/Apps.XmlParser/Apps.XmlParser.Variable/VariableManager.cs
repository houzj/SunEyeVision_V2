using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using HikExternalCall.Common;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.Variable;

public class VariableManager
{
	public enum MODIFYACTION
	{
		VAR_NAME,
		VAR_TYPE,
		VAR_REMARK,
		VAR_VALUE,
		VAR_LINKMODULE,
		VAR_LINKMODULEBYPARAM,
		VAR_COMMENABLE,
		VAR_INDEX,
		VAR_SETTOP
	}

	public enum IO_VALUE_TYPE
	{
		IO_VALUE_TYPE_INT,
		IO_VALUE_TYPE_FLOAT,
		IO_VALUE_TYPE_STRING,
		IO_VALUE_TYPE_BYTE,
		IO_VALUE_TYPE_IMAGE,
		IO_VALUE_TYPE_POINTSET,
		IO_VALUE_TYPE_UNKNOW
	}

	public struct ByteDataArray
	{
		public IntPtr pData;

		public uint nDataLen;

		public uint nDataSize;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[] strReserved;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public uint[] nReserved;
	}

	public struct IMVS_SUB_VALUE
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszValueName;

		public IO_VALUE_TYPE emValueType;

		public uint nValueNum;

		public IntPtr pIntValue;

		public IntPtr pFloatValue;

		public IntPtr pByteValue;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public uint[] nReserved;
	}

	public struct IMVS_MODIFY_VAR
	{
		public MODIFYACTION enumAction;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszVarName;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszSubVarName;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszChangeValue;

		public uint nValueNum;

		public IntPtr pBinaryValue;
	}

	public struct IMVS_LINKMODULE
	{
		[MarshalAs(UnmanagedType.I4)]
		public int nModuleID;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] chszRunParamType;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
		public byte[] chszRunParamName;
	}

	public struct IMVS_VARIABLE_VALUE
	{
		public uint nValueNum;

		public IntPtr pIntValue;

		public IntPtr pFloatValue;

		public IntPtr pByteValue;

		public IntPtr pIoValue;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public int[] nReserved;
	}

	public struct IMVS_VARIABLE
	{
		public int nIndex;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
		public byte[] chszGuid;

		[MarshalAs(UnmanagedType.I1)]
		public bool bCommEnable;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszValueName;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] chszValueType;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszValue;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chszRemark;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] chsGroupInfo;

		public int nLinkCount;

		public IntPtr pLinkModuleList;

		[MarshalAs(UnmanagedType.I1)]
		public bool bCombination;

		public IMVS_VARIABLE_VALUE stVarValue;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public int[] nReserved;
	}

	private const int IMVS_MAX_SHARE_MEMORY_ADDR_LENGTH = 64;

	private const int IMVS_MAX_PARAM_LENGTH = 256;

	private const int IMVS_MAX_RUN_PARAM_LENGTH = 128;

	private const int IMVS_MAX_TYPE_LENGTH = 16;

	private const int IMVS_MAX_GUID_LENGTH = 36;

	public static Dictionary<string, List<VarModuleResult>> ModuleVarDict = new Dictionary<string, List<VarModuleResult>>();

	public static Dictionary<uint, ImvsSdkDefine.IMVS_MODULE_INFO> AllModuleList = new Dictionary<uint, ImvsSdkDefine.IMVS_MODULE_INFO>();

	private VmModule DataSource;

	private static List<string> ExceptType = new List<string> { "CIRCLE", "CONTOURPOINTS", "CLASSINFO", "PIXELIMAGE", "ANNULUS", "POSTURE", "COORDINATE", "DEBUG", "DOUBLE", "image" };

	private static List<string> VarDisplaySupportIO = null;

	private static List<string> VarSupportIO = null;

	private const string GetVarValue = "VarValue-null-";

	private string strDefaultGroupName => AppLocalizationService.GetLang("Lang_VarGroup") + "0";

	public VariableManager(object dataSource)
	{
		if (dataSource is VmModule)
		{
			DataSource = (VmModule)dataSource;
		}
	}

	public VariableManager()
	{
	}

	private string RepairVarName(string varName)
	{
		if (varName.First() != '%' || varName.Last() != '%')
		{
			varName = AppLocalizationService.UnDoTranslate(varName);
		}
		return varName;
	}

	public static List<string> GetDisplaySupportIO()
	{
		if (VarDisplaySupportIO == null)
		{
			IEnumerable<string> first = from p in Enum.GetValues(typeof(LeafIOTypeEnum)).OfType<LeafIOTypeEnum>()
				where !ExceptType.Exists((string m) => m == p.ToString())
				select p.ToString().ToLower();
			IEnumerable<string> second = from p in Enum.GetValues(typeof(CombinationIOTypeEnum)).OfType<CombinationIOTypeEnum>()
				where !ExceptType.Exists((string m) => m == p.ToString())
				select DynamicIO.GetDefaultObjectName(p);
			if (CheckAdditionExist.IsExistAddition)
			{
				IEnumerable<string> second2 = from p in Enum.GetValues(typeof(CombinationIOTypeEnumEx)).OfType<CombinationIOTypeEnumEx>()
					where !ExceptType.Exists((string m) => m == p.ToString())
					select DynamicIO.GetDefaultObjectName(p);
				VarDisplaySupportIO = first.Concat(second).Concat(second2).ToList();
			}
			else
			{
				VarDisplaySupportIO = first.Concat(second).ToList();
			}
		}
		return VarDisplaySupportIO;
	}

	public static List<string> GetSupportIO()
	{
		if (VarSupportIO == null)
		{
			IEnumerable<string> first = from p in Enum.GetValues(typeof(LeafIOTypeEnum)).OfType<LeafIOTypeEnum>()
				where !ExceptType.Exists((string m) => m == p.ToString())
				select p.ToString().ToLower();
			IEnumerable<string> second = from p in Enum.GetValues(typeof(CombinationIOTypeEnum)).OfType<CombinationIOTypeEnum>()
				where !ExceptType.Exists((string m) => m == p.ToString())
				select p.ToString();
			if (CheckAdditionExist.IsExistAddition)
			{
				IEnumerable<string> second2 = from p in Enum.GetValues(typeof(CombinationIOTypeEnumEx)).OfType<CombinationIOTypeEnumEx>()
					where !ExceptType.Exists((string m) => m == p.ToString())
					select p.ToString();
				VarSupportIO = first.Concat(second).Concat(second2).ToList();
			}
			else
			{
				VarSupportIO = first.Concat(second).ToList();
			}
		}
		return VarSupportIO;
	}

	private string[] GetBindVarIO(string varName)
	{
		if (!ModuleVarDict.ContainsKey(varName))
		{
			return null;
		}
		List<string> list = new List<string>();
		foreach (VarModuleResult item in ModuleVarDict[varName])
		{
			if (!string.IsNullOrEmpty(item.FullName))
			{
				list.Add(item.FullName);
			}
			else
			{
				list.Add(VmSolution.Instance.GetGlobalVariabelInputModule(item.ModuleID, item.IoName, ref AllModuleList));
			}
		}
		return list.ToArray();
	}

	public string[] GetBindGlobalVariableItem(string varName, string type)
	{
		if (!ModuleVarDict.ContainsKey(varName))
		{
			return null;
		}
		List<string> templist = new List<string>();
		string[] bindVarIO = GetBindVarIO(varName);
		if (bindVarIO != null)
		{
			Array.ForEach(bindVarIO, (string str) =>
			{
				if (!string.IsNullOrEmpty(str))
				{
					string[] array = str.Split('.');
					array[array.Length - 2] = AppLocalizationService.GetLang(array[array.Length - 2]);
					string text = array[array.Length - 1];
					text = ((text[0] == '(' && text[text.Length - 1] == ')') ? text.Substring(1, text.Length - 2) : text);
					array[array.Length - 1] = AppLocalizationService.GetLang(text);
					templist.Add(string.Join(".", array));
				}
			});
		}
		else
		{
			UnBindGlobarVarItem(varName, isFromUI: false);
		}
		return templist.ToArray();
	}

	private bool GetCombinBind(ref VmIO combinIO, ref VmIO varIO)
	{
		if (varIO == null || combinIO == null)
		{
			return false;
		}
		while (varIO.CombinRoot != null)
		{
			varIO = varIO.CombinRoot;
		}
		if (varIO.TypeName == combinIO.TypeName)
		{
			return true;
		}
		while (combinIO.CombinRoot != null)
		{
			combinIO = combinIO.CombinRoot;
			if (varIO.TypeName == combinIO.TypeName)
			{
				return true;
			}
		}
		return false;
	}

	private ImvsSdkDefine.IMVS_UNBIND_GLOBALVAR_PARAM_INFO GetUnbindInfo(ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO bindInfo)
	{
		return new ImvsSdkDefine.IMVS_UNBIND_GLOBALVAR_PARAM_INFO
		{
			nModuleId = bindInfo.nModuleId,
			strParamName = bindInfo.strParamName,
			stBindValue = new ImvsSdkDefine.IMVS_SUBSCRIBE_MODU_PARAM_INFO
			{
				eVarType = ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE,
				stVariableValue = new ImvsSdkDefine.IMVS_VARIABLE_VALUE_INFO
				{
					eIndexType = ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL,
					strIndexConstant = StringUtil.UTF8GetFixLenBytes("All", 256),
					stModuleParam = new ImvsSdkDefine.IMVS_SUBSCRIBE_MODULE_PARAM_INFO
					{
						nModuleId = bindInfo.stBindValue.stVariableValue.stModuleParam.nModuleId,
						byParamName = bindInfo.stBindValue.stVariableValue.stModuleParam.byParamName
					}
				}
			},
			eGlobalVarType = bindInfo.eGlobalVarType
		};
	}

	private void GetGlobalBindingRelation(ref Dictionary<string, List<VarModuleResult>> moduelVarDict, List<VarItem> varList = null)
	{
		moduelVarDict.Clear();
		VmModule dataSource = DataSource;
		if (dataSource == null)
		{
			return;
		}
		ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO[] bindGlobalVarParam = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetBindGlobalVarParam(dataSource.ID);
		List<int> BindListId = new List<int>();
		Array.ForEach(bindGlobalVarParam, (ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO x) =>
		{
			BindListId.Add(x.stBindValue.stVariableValue.stModuleParam.nModuleId);
		});
		List<ImvsSdkDefine.IMVS_UNBIND_GLOBALVAR_PARAM_INFO> list = new List<ImvsSdkDefine.IMVS_UNBIND_GLOBALVAR_PARAM_INFO>();
		List<VmModule> vmModulesByIDArray = GetVmModulesByIDArray(BindListId);
		for (int num = 0; num < bindGlobalVarParam.Length; num++)
		{
			string text = StringUtil.UTF8GetString(bindGlobalVarParam[num].strParamName);
			ImvsSdkDefine.IMVS_SUBSCRIBE_MODULE_PARAM_INFO param = bindGlobalVarParam[num].stBindValue.stVariableValue.stModuleParam;
			VmModule vmModule = vmModulesByIDArray.FirstOrDefault((VmModule p) => p.ID == param.nModuleId);
			if (vmModule == null)
			{
				list.Add(GetUnbindInfo(bindGlobalVarParam[num]));
				continue;
			}
			VmIO io = vmModule?[StringUtil.UTF8GetString(param.byParamName)] as VmIO;
			if (io == null)
			{
				list.Add(GetUnbindInfo(bindGlobalVarParam[num]));
				continue;
			}
			VmIO combinIO = io.CombinRoot;
			VmIO varIO = dataSource?[text] as VmIO;
			if (varIO == null && varList != null && varList.FirstOrDefault((VarItem x) => x.Name == io.Name && x.Type == io.TypeName) == null)
			{
				list.Add(GetUnbindInfo(bindGlobalVarParam[num]));
			}
			else if (combinIO == null)
			{
				if (!moduelVarDict.ContainsKey(text))
				{
					moduelVarDict.Add(text, new List<VarModuleResult>());
				}
				string moduParamName = StringUtil.UTF8GetString(param.byParamName);
				if (moduelVarDict[text].FirstOrDefault((VarModuleResult x) => x.IoName == moduParamName && x.ModuleID == param.nModuleId) == null)
				{
					moduelVarDict[text].Add(new VarModuleResult
					{
						ModuleID = param.nModuleId,
						IoName = moduParamName,
						FullName = io.FullName
					});
				}
			}
			else if (GetCombinBind(ref combinIO, ref varIO))
			{
				if (combinIO == null || varIO == null)
				{
					continue;
				}
				if (moduelVarDict.ContainsKey(varIO.Name))
				{
					VarModuleResult varModuleResult = moduelVarDict[varIO.Name].FirstOrDefault((VarModuleResult x) => x.IoName == combinIO.Name && x.ModuleID == combinIO.Root.ID);
					if (varModuleResult != null)
					{
						continue;
					}
				}
				if (!moduelVarDict.ContainsKey(varIO.Name))
				{
					moduelVarDict.Add(varIO.Name, new List<VarModuleResult>());
				}
				moduelVarDict[varIO.Name].Add(new VarModuleResult
				{
					ModuleID = param.nModuleId,
					IoName = combinIO.Name,
					FullName = combinIO.FullName
				});
			}
			else
			{
				if (!moduelVarDict.ContainsKey(text))
				{
					moduelVarDict.Add(text, new List<VarModuleResult>());
				}
				string moduParamName2 = StringUtil.UTF8GetString(param.byParamName);
				if (moduelVarDict[text].FirstOrDefault((VarModuleResult x) => x.IoName == moduParamName2 && x.ModuleID == param.nModuleId) == null)
				{
					moduelVarDict[text].Add(new VarModuleResult
					{
						ModuleID = param.nModuleId,
						IoName = moduParamName2,
						FullName = io.FullName
					});
				}
			}
		}
		if (list.Count > 0)
		{
			ServerSDKManager.serverSDKManager.mGlobalModuleManager.UnBindVarArray(list);
		}
	}

	public void GetGlobalBindingRelation(IMVS_VARIABLE[] vARIABLEs)
	{
		List<VarItem> varList = new List<VarItem>();
		for (int i = 0; i < vARIABLEs.Length; i++)
		{
			VarItem varItem = new VarItem();
			IMVS_VARIABLE iMVS_VARIABLE = vARIABLEs[i];
			varItem.Name = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueName);
			varItem.Type = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueType);
			varList.Add(varItem);
			if (iMVS_VARIABLE.bCombination && iMVS_VARIABLE.stVarValue.pIoValue != IntPtr.Zero && iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				Array.ForEach(array, (IMVS_SUB_VALUE x) =>
				{
					VarItem item = new VarItem
					{
						Name = StringUtil.UTF8GetString(x.chszValueName),
						Type = GetStringByType(x.emValueType)
					};
					varList.Add(item);
				});
			}
		}
		GetGlobalBindingRelation(ref ModuleVarDict, varList);
	}

	public void ClearCache()
	{
		ModuleVarDict.Clear();
		AllModuleList.Clear();
	}

	private void ChangeBind(string varName, string varNewName, string varType)
	{
		if (!ModuleVarDict.ContainsKey(varName))
		{
			return;
		}
		if (!ModuleVarDict.ContainsKey(varNewName))
		{
			ModuleVarDict.Add(varNewName, new List<VarModuleResult>());
		}
		List<int> BindListId = new List<int>();
		ModuleVarDict[varName].ForEach((VarModuleResult x) =>
		{
			BindListId.Add(x.ModuleID);
		});
		List<VmModule> vmModulesByIDArray = GetVmModulesByIDArray(BindListId);
		List<VarModuleResult> list = ModuleVarDict[varName];
		foreach (VarModuleResult item in list)
		{
			VmModule vmModule = vmModulesByIDArray.FirstOrDefault((VmModule p) => p.ID == item.ModuleID);
			if (vmModule != null)
			{
				VmIO vmIO = vmModule.Outputs[item.IoName];
				if (vmIO.IsLeafNode)
				{
					vmIO?.UnBindingVariableWithNoIo(varName, varType, DataSource);
					vmIO.BindingGlobalVariable(varNewName, DataSource);
				}
				else if (vmIO.Tag != null)
				{
					vmIO.Tag = new VarItem
					{
						Owner = DataSource,
						Name = varNewName
					};
				}
				ModuleVarDict[varNewName].Add(item);
			}
		}
		ModuleVarDict.Remove(varName);
	}

	private bool UnBindGlobarVarItem(string varName, Dictionary<string, List<VarModuleResult>> moduelVarDict)
	{
		if (!moduelVarDict.ContainsKey(varName))
		{
			return false;
		}
		List<int> BindListId = new List<int>();
		ModuleVarDict[varName].ForEach((VarModuleResult x) =>
		{
			BindListId.Add(x.ModuleID);
		});
		List<VmModule> vmModulesByIDArray = GetVmModulesByIDArray(BindListId);
		List<VarModuleResult> list = moduelVarDict[varName];
		foreach (VarModuleResult item in list)
		{
			VmModule vmModule = vmModulesByIDArray.FirstOrDefault((VmModule p) => p.ID == item.ModuleID);
			if (vmModule == null)
			{
				continue;
			}
			VmIO vmIO = vmModule.Outputs[item.IoName];
			if (vmIO == null)
			{
				return false;
			}
			try
			{
				vmIO.UnBindingGlobalVariable(varName, DataSource);
			}
			catch (Exception ex)
			{
				VmException vmException = VmSolution.GetVmException(ex);
				if (vmException != null)
				{
					LogHelper.Error("UnBindGlobarVarItem is exception[" + varName + "],ex:" + vmException.errorMessage);
				}
				else
				{
					LogHelper.Error("UnBindGlobarVarItem is exception[" + varName + "],ex:" + ex.ToString());
				}
			}
		}
		moduelVarDict.Remove(varName);
		return true;
	}

	public bool UnBindGlobarVarItem(string varName, bool isFromUI = true)
	{
		if (isFromUI)
		{
			return UnBindGlobarVarItem(varName, ModuleVarDict);
		}
		Dictionary<string, List<VarModuleResult>> moduelVarDict = new Dictionary<string, List<VarModuleResult>>();
		GetGlobalBindingRelation(ref moduelVarDict);
		return UnBindGlobarVarItem(varName, moduelVarDict);
	}

	private IVmModule GetVmModuleById(int ModuleID)
	{
		List<VmModule> list = new List<VmModule>();
		return VmSolution.Instance.GetModuleByID((uint)ModuleID);
	}

	public List<VmModule> GetVmModulesByIDArray(List<int> nModuleID)
	{
		List<VmModule> list = new List<VmModule>();
		VmSolution.Instance.GetAllModuleByIDs(list, nModuleID.ToArray());
		return list;
	}

	private IO_VALUE_TYPE GetTypeByString(string type)
	{
		IO_VALUE_TYPE result = IO_VALUE_TYPE.IO_VALUE_TYPE_UNKNOW;
		switch (type)
		{
		case "int":
			result = IO_VALUE_TYPE.IO_VALUE_TYPE_INT;
			break;
		case "string":
			result = IO_VALUE_TYPE.IO_VALUE_TYPE_STRING;
			break;
		case "float":
			result = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			break;
		case "byte":
			result = IO_VALUE_TYPE.IO_VALUE_TYPE_BYTE;
			break;
		case "image":
			result = IO_VALUE_TYPE.IO_VALUE_TYPE_IMAGE;
			break;
		case "pointset":
			result = IO_VALUE_TYPE.IO_VALUE_TYPE_POINTSET;
			break;
		}
		return result;
	}

	private string GetStringByType(IO_VALUE_TYPE type)
	{
		string result = "";
		switch (type)
		{
		case IO_VALUE_TYPE.IO_VALUE_TYPE_INT:
			result = "int";
			break;
		case IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT:
			result = "float";
			break;
		case IO_VALUE_TYPE.IO_VALUE_TYPE_STRING:
			result = "string";
			break;
		case IO_VALUE_TYPE.IO_VALUE_TYPE_BYTE:
			result = "byte";
			break;
		case IO_VALUE_TYPE.IO_VALUE_TYPE_IMAGE:
			result = "image";
			break;
		case IO_VALUE_TYPE.IO_VALUE_TYPE_POINTSET:
			result = "pointset";
			break;
		}
		return result;
	}

	private void UpdateSendDatas(VmModule sendModule, int delIndex, int destIndex = 0, bool isDel = true, bool isReset = false, bool isSetTop = false, bool isChangeType = false)
	{
		LogHelper.Info($"UpdateSendDatas start id:{sendModule.ID}");
		try
		{
			uint iD = DataSource.ID;
			string text = sendModule.Params["OutputType"];
			if (text != "2")
			{
				return;
			}
			byte[] array = sendModule.UserData["GlobalVarRecord"];
			if (array != null && array.Length != 0)
			{
				string text2 = StringUtil.UTF8GetString(array);
				string[] array2 = text2.Split('\r');
				if (array2.Length == 0)
				{
					return;
				}
				LogHelper.Info("UpdateSendDatas,get GlobalVarRecord:" + text2);
				for (int i = 0; i < array2.Length; i++)
				{
					if (string.IsNullOrEmpty(array2[i]))
					{
						continue;
					}
					string[] array3 = array2[i].Split('#');
					if (array3.Length < 5)
					{
						continue;
					}
					string[] array4 = array3[4].Split('+');
					if (array4.Length <= 1 || !int.TryParse(array4[0], out var result) || iD != result)
					{
						continue;
					}
					string key = array3[0];
					string name = array3[1];
					int num = int.Parse(array3[3]);
					if (isReset)
					{
						if (num > 0)
						{
							num = 0;
							VmIO iO = sendModule.GetIO(name);
							if (iO != null)
							{
								iO.RelateIO = null;
							}
						}
					}
					else if (isChangeType)
					{
						if (num == delIndex)
						{
							num = 0;
							VmIO iO2 = sendModule.GetIO(name);
							if (iO2 != null)
							{
								iO2.RelateIO = null;
							}
						}
					}
					else if (isDel)
					{
						if (delIndex > num)
						{
							continue;
						}
						if (delIndex < num)
						{
							num--;
						}
						else if (delIndex == num)
						{
							num = 0;
							VmIO iO3 = sendModule.GetIO(name);
							if (iO3 != null)
							{
								iO3.RelateIO = null;
							}
						}
					}
					else
					{
						num = UpdateIndex(num, delIndex, destIndex, isSetTop);
					}
					array3[3] = num.ToString();
					string text3 = string.Join("#", array3);
					LogHelper.Info("UpdateSendDatas,set sendModule:" + text3);
					sendModule.Params[key] = text3;
					array2[i] = text3;
				}
				string text4 = string.Join("\r", array2);
				if (text2 != text4)
				{
					LogHelper.Info("UpdateSendDatas,set GlobalVarRecord:" + text4);
					sendModule.UserData["GlobalVarRecord"] = StringUtil.UTF8GetBytesPadZero(text4);
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("UpdateSendDatas is error" + ex.ToString());
		}
		LogHelper.Info("UpdateSendDatas end");
	}

	private void UpdateReceiveDatas(VmModule receiveModule, int delIndex, int destIndex = 0, bool isDel = true, bool isReset = false, bool isSetTop = false, bool isChangeType = false)
	{
		try
		{
			uint iD = DataSource.ID;
			string text = receiveModule.Params["OutputType"];
			if (text != "2")
			{
				return;
			}
			byte[] array = receiveModule.UserData["GlobalVarRecord"];
			if (array == null || array.Length == 0)
			{
				return;
			}
			string text2 = StringUtil.UTF8GetString(array);
			if (string.IsNullOrEmpty(text2))
			{
				return;
			}
			string[] array2 = text2.Split('\n');
			for (int i = 0; i < array2.Length; i++)
			{
				if (string.IsNullOrEmpty(array2[i]))
				{
					continue;
				}
				string[] array3 = array2[i].Split('#');
				if (array3.Length < 10)
				{
					continue;
				}
				string[] array4 = array3[9].Split('+');
				if (array4.Length <= 1 || !int.TryParse(array4[0], out var result) || iD != result)
				{
					continue;
				}
				string[] array5 = array3[8].Split(':');
				if (array5.Length < 2)
				{
					continue;
				}
				int num = int.Parse(array5[1]);
				if (isReset)
				{
					num = 0;
				}
				else if (isChangeType)
				{
					if (num == delIndex)
					{
						num = 0;
					}
				}
				else if (isDel)
				{
					if (num > delIndex)
					{
						num--;
					}
					else if (num == delIndex)
					{
						num = 0;
					}
				}
				else
				{
					num = UpdateIndex(num, delIndex, destIndex, isSetTop);
				}
				receiveModule.UserData[array3[1] + ":GlobalIndex"] = StringUtil.UTF8GetBytesPadZero(iD + ":" + num.ToString());
				array5[1] = num.ToString();
				array3[8] = string.Join(":", array5);
				array2[i] = string.Join("#", array3);
				receiveModule.Params["OutputFilter"] = array2[i];
			}
			string text3 = string.Join("\n", array2);
			if (text2 != text3)
			{
				receiveModule.UserData["GlobalVarRecord"] = StringUtil.UTF8GetBytesPadZero(text3);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("UpdateReceiveDatas is error" + ex.ToString());
		}
	}

	public void ChangeToSendReceiveData(int delIndex, int destIndex = 0, bool isDel = true, bool isReset = false, bool isSetTop = false, bool isChangeType = false)
	{
		LogHelper.Info($"ChangeToSendReceiveData: delIndex:{delIndex},destIndex:{destIndex},isDel:{isDel},isReset:{isReset}");
		List<VmModule> list = new List<VmModule>();
		VmSolution.Instance.GetAllModuleByType(list, "SendDatasModule");
		VmSolution.Instance.GetAllModuleByType(list, "ReadDatasModule");
		foreach (VmModule item in list)
		{
			if (item.StrModuleName == "SendDatasModule")
			{
				UpdateSendDatas(item, delIndex, destIndex, isDel, isReset, isSetTop, isChangeType);
			}
			else if (item.StrModuleName == "ReadDatasModule")
			{
				UpdateReceiveDatas(item, delIndex, destIndex, isDel, isReset, isSetTop, isChangeType);
			}
		}
	}

	private int UpdateIndex(int index, int sourceIndex, int destIndex, bool isSetTop = false)
	{
		if (!isSetTop)
		{
			if (index == sourceIndex)
			{
				return destIndex;
			}
			if (index == destIndex)
			{
				return sourceIndex;
			}
			return index;
		}
		if (index > sourceIndex && index > destIndex)
		{
			return index;
		}
		if (index < sourceIndex && index < destIndex)
		{
			return index;
		}
		if (index == sourceIndex)
		{
			return destIndex;
		}
		return (destIndex - sourceIndex < 0) ? (index + 1) : (index - 1);
	}

	private void CheckVarName(string varName)
	{
		if (StringUtil.GetByteCountWithZero(varName) > 126)
		{
			throw new VmException("input name is too long");
		}
		if (varName.Contains('?') || varName.Contains(';') || varName.Contains('.') || varName.Contains('/') || varName.Contains('\\') || varName.Contains('`') || varName.Contains('{') || varName.Contains('}') || varName.Contains('[') || varName.Contains(']') || varName.Contains('<') || varName.Contains('>') || varName.Contains('(') || varName.Contains(')'))
		{
			throw new VmException("input name contain special character");
		}
	}

	public void SetReportState(bool isEnable)
	{
		try
		{
			(DataSource as IVarModule).VarValueReportEnable = isEnable;
		}
		catch (VmException ex)
		{
			LogHelper.Error("SetReportState is error:" + ex.ToString());
			((IUserStringData)DataSource.Params)["EnableReport"] = (isEnable ? "true" : "false");
		}
	}

	public void AddVar(string varName, string varType, string defaultValue = "", string defaultGroupName = "")
	{
		if (string.IsNullOrEmpty(varName) || string.IsNullOrEmpty(varType))
		{
			throw new VmException(-536870888);
		}
		LogHelper.Info("AddVar: varname:" + varName + ",type:" + varType);
		CheckVarName(varName);
		if (string.IsNullOrEmpty(defaultGroupName))
		{
			defaultGroupName = strDefaultGroupName;
		}
		varName = RepairVarName(varName);
		VmIO vmIO = DataSource.Outputs.FirstOrDefault((VmIO x) => x.Name == varName);
		if (vmIO != null)
		{
			throw new Exception("name is already exit");
		}
		IMVS_VARIABLE iMVS_VARIABLE = new IMVS_VARIABLE
		{
			bCommEnable = false,
			chszValueName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszValueType = StringUtil.UTF8GetFixLenBytes(varType, 16),
			chsGroupInfo = StringUtil.UTF8GetFixLenBytes(defaultGroupName, 256),
			chszGuid = StringUtil.UTF8GetFixLenBytes(Guid.NewGuid().ToString("D"), 36)
		};
		if (varType == "int")
		{
			int result = 0;
			if (int.TryParse(defaultValue, out result))
			{
				defaultValue = result.ToString();
			}
		}
		else if (varType == "float")
		{
			float result2 = 0f;
			if (float.TryParse(defaultValue, out result2))
			{
				defaultValue = result2.ToString();
			}
		}
		iMVS_VARIABLE.chszValue = StringUtil.UTF8GetFixLenBytes(defaultValue, 256);
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_VARIABLE, Marshal.SizeOf(typeof(IMVS_VARIABLE)));
		((IUserBytesData)DataSource.Params)["AddVar"] = value;
		object ioType = GetIoType(varType);
		VmIO vmIO2 = AddIoByType(ioType, varName, DataSource.Outputs);
		vmIO2.Index = DataSource.Outputs.Count - 4;
	}

	public VmIO CheckVarAddIO(string varName, string varType, int nIndex)
	{
		if (string.IsNullOrEmpty(varName) || string.IsNullOrEmpty(varType))
		{
			return null;
		}
		varName = RepairVarName(varName);
		VmIO vmIO = DataSource.Outputs.FirstOrDefault((VmIO x) => x.Name == varName);
		if (vmIO == null)
		{
			object ioType = GetIoType(varType);
			VmIO vmIO2 = AddIoByType(ioType, varName, DataSource.Outputs);
			vmIO2.Index = nIndex;
			return vmIO2;
		}
		return vmIO;
	}

	public void DeleteVarFromGroup(string varName, bool isFromUI = true, VarRunParamInfo[] runParaminfo = null)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870888);
		}
		varName = RepairVarName(varName);
		DeleteIOLink(varName, isFromUI, runParaminfo);
	}

	public void DeleteVar(string varName, bool isFromUI = true, VarRunParamInfo[] runParaminfo = null, bool withVar = true)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870888);
		}
		LogHelper.Info("DeleteVar varName:" + varName);
		varName = RepairVarName(varName);
		if (withVar)
		{
			((IUserStringData)DataSource.Params)["DeleteVar"] = varName;
		}
		DeleteIO(varName, isFromUI, runParaminfo);
	}

	public void GetAllVar(ref IMVS_VARIABLE[] vARIABLEs)
	{
		string s = ((IUserStringData)DataSource.Params)["VarCount"];
		int result = 0;
		if (int.TryParse(s, out result) && result > 0)
		{
			byte[] array = ((IUserBytesData)DataSource.Params)["GlobalValueList"];
			if (array != null && array.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
			{
				List<IMVS_VARIABLE> list = ExternalCallHelper.BytesToStructs<IMVS_VARIABLE>(array);
				vARIABLEs = list.ToArray();
			}
		}
	}

	public void GetAllVar(ref List<VarItem> varList)
	{
		IMVS_VARIABLE[] vARIABLEs = new IMVS_VARIABLE[0];
		GetAllVar(ref vARIABLEs);
		for (int i = 0; i < vARIABLEs.Length; i++)
		{
			VarItem varItem = new VarItem();
			IMVS_VARIABLE iMVS_VARIABLE = vARIABLEs[i];
			varItem.Name = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueName);
			varItem.Type = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueType);
			varItem.Index = iMVS_VARIABLE.nIndex;
			varItem.IsCommInit = iMVS_VARIABLE.bCommEnable;
			varItem.IsCombination = iMVS_VARIABLE.bCombination;
			varItem.Value = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValue);
			varItem.RemarkInfo = StringUtil.UTF8GetString(iMVS_VARIABLE.chszRemark);
			varItem.GroupInfo = StringUtil.UTF8GetString(iMVS_VARIABLE.chsGroupInfo);
			varItem.Guid = StringUtil.UTF8GetString(iMVS_VARIABLE.chszGuid);
			varList.Add(varItem);
			if (vARIABLEs[i].stVarValue.nValueNum == 0)
			{
				continue;
			}
			if (varItem.Type == "int")
			{
				int[] array = new int[vARIABLEs[i].stVarValue.nValueNum];
				int num = (int)(vARIABLEs[i].stVarValue.nValueNum * Marshal.SizeOf(typeof(int)));
				byte[] array2 = new byte[num];
				Marshal.Copy(vARIABLEs[i].stVarValue.pIntValue, array2, 0, num);
				Buffer.BlockCopy(array2, 0, array, 0, array2.Length);
				varItem.Value = array[0].ToString();
			}
			else if (varItem.Type == "float")
			{
				float[] array3 = new float[vARIABLEs[i].stVarValue.nValueNum];
				int num2 = (int)(vARIABLEs[i].stVarValue.nValueNum * Marshal.SizeOf(typeof(float)));
				byte[] array4 = new byte[num2];
				Marshal.Copy(vARIABLEs[i].stVarValue.pFloatValue, array4, 0, num2);
				Buffer.BlockCopy(array4, 0, array3, 0, array4.Length);
				varItem.Value = array3[0].ToString();
			}
			else if (varItem.Type == "string")
			{
				ByteDataArray[] array5 = ExternalCallHelper.IntPtr2Structures<ByteDataArray>(vARIABLEs[i].stVarValue.pByteValue, (int)vARIABLEs[i].stVarValue.nValueNum);
				if (array5 != null && array5.Length != 0 && array5[0].nDataLen != 0)
				{
					byte[] array6 = new byte[array5[0].nDataLen];
					Marshal.Copy(array5[0].pData, array6, 0, array6.Length);
					string value = StringUtil.UTF8GetString(array6);
					varItem.Value = value;
				}
			}
			if (varItem.IsCombination && vARIABLEs[i].stVarValue.pIoValue != IntPtr.Zero)
			{
				varItem.SubIOInfos = new List<VarItem>();
				IMVS_SUB_VALUE[] array7 = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(vARIABLEs[i].stVarValue.pIoValue, (int)vARIABLEs[i].stVarValue.nValueNum);
				Array.ForEach(array7, (IMVS_SUB_VALUE x) =>
				{
					string name = StringUtil.UTF8GetString(x.chszValueName);
					string stringByType = GetStringByType(x.emValueType);
					varItem.SubIOInfos.Add(new VarItem
					{
						Name = name,
						Type = stringByType
					});
				});
			}
		}
	}

	public void ModifyType(string varName, string varType, bool isFromUI = true, VarRunParamInfo[] runParaminfo = null)
	{
		if (string.IsNullOrEmpty(varName) || string.IsNullOrEmpty(varType))
		{
			throw new VmException(-536870888);
		}
		varName = RepairVarName(varName);
		int num = -1;
		VmIO vmIO = DataSource.Outputs.FirstOrDefault((VmIO x) => x.Name == varName);
		if (vmIO == null)
		{
			throw new Exception("name is not exit");
		}
		num = vmIO.Index;
		object ioType = GetIoType(varType);
		if (ioType == null)
		{
			throw new Exception("type is not support");
		}
		object ioType2 = GetIoType(vmIO.TypeName);
		if (ioType2 == null)
		{
			throw new Exception("type is not support");
		}
		if (ioType == ioType2)
		{
			return;
		}
		LogHelper.Info("ModifyType: srcTyep:" + vmIO.TypeName + ",dstType:" + varType);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_TYPE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszChangeValue = StringUtil.UTF8GetFixLenBytes(varType, 256)
		};
		IntPtr zero = IntPtr.Zero;
		VmIO vmIO2 = null;
		if (ioType is CombinationIOTypeEnum || ioType2 is CombinationIOTypeEnum || ioType is CombinationIOTypeEnumEx || ioType2 is CombinationIOTypeEnumEx)
		{
			DeleteIO(varName, isFromUI, runParaminfo, isChangeIndex: false);
			vmIO2 = AddIoByType(ioType, varName, DataSource.Outputs);
			vmIO2.Index = num;
		}
		else
		{
			vmIO.TypeName = varType;
			try
			{
				UnBindGlobarVarItem(varName, isFromUI);
			}
			catch (Exception ex)
			{
				if (ex is VmException ex2)
				{
					LogHelper.Error("UnBindingGlobalVariable is error:" + ex2.errorMessage);
				}
				else
				{
					LogHelper.Error("UnBindingGlobalVariable is error:" + ex.ToString());
				}
			}
			LogHelper.Info($"ModifyType varName:{varName} varType:{varType},index:{vmIO.Index}");
			ChangeToSendReceiveData(vmIO.Index, 0, isDel: false, isReset: false, isSetTop: false, isChangeType: true);
			SetRunParamBind(varName, varType, runParaminfo, null);
		}
		IntPtr intPtr = IntPtr.Zero;
		if ((ioType is CombinationIOTypeEnum || ioType is CombinationIOTypeEnumEx) && vmIO2 != null)
		{
			List<VmIO> allLeafIO = vmIO2.GetAllLeafIO();
			IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[allLeafIO.Count];
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				array[num2].chszValueName = StringUtil.UTF8GetFixLenBytes(allLeafIO[num2].Name, 256);
				array[num2].emValueType = GetTypeByString(allLeafIO[num2].TypeName);
			}
			byte[] array2 = ExternalCallHelper.StructsToBytes(array);
			intPtr = Marshal.AllocHGlobal(array2.Length);
			Marshal.Copy(array2, 0, intPtr, array2.Length);
			iMVS_MODIFY_VAR.pBinaryValue = intPtr;
			iMVS_MODIFY_VAR.nValueNum = (uint)array.Length;
		}
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value);
		}
		catch (VmException ex3)
		{
			throw ex3;
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
				intPtr = IntPtr.Zero;
			}
		}
	}

	public void ModifyRemark(string varName, string varRemark)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870888);
		}
		varName = RepairVarName(varName);
		byte[] value = ExternalCallHelper.StructToBytes(new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_REMARK,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszChangeValue = StringUtil.UTF8GetFixLenBytes(varRemark, 256)
		}, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		SetBinaryData("ModifyVar", value);
	}

	public void ModifyVarName(string varName, string varNewName, VarRunParamInfo[] runParaminfo = null)
	{
		LogHelper.Info("ModifyVarName " + varName + " to " + varNewName);
		if (varName == varNewName)
		{
			return;
		}
		if (string.IsNullOrEmpty(varName) || string.IsNullOrEmpty(varNewName))
		{
			throw new VmException(-536870888);
		}
		CheckVarName(varNewName);
		varName = RepairVarName(varName);
		varNewName = RepairVarName(varNewName);
		VmIO vmIO = DataSource.Outputs?.FirstOrDefault((VmIO x) => x.Name == varNewName);
		if (vmIO != null)
		{
			throw new Exception("name is already exit");
		}
		byte[] value = ExternalCallHelper.StructToBytes(new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_NAME,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszChangeValue = StringUtil.UTF8GetFixLenBytes(varNewName, 256)
		}, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		SetBinaryData("ModifyVar", value);
		VmIO vmIO2 = null;
		try
		{
			vmIO2 = DataSource?.Outputs?[varName];
			if (vmIO2 != null)
			{
				vmIO2.Name = varNewName;
			}
		}
		catch (Exception ex)
		{
			VmException vmException = VmSolution.GetVmException(ex);
			if (vmException != null)
			{
				LogHelper.Error("change vario name [" + varName + "] to [" + varNewName + "] is exception:" + vmException.errorMessage);
			}
			else
			{
				LogHelper.Error("change vario name [" + varName + "] to [" + varNewName + "] is exception:" + ex.ToString());
			}
		}
		LogHelper.Info("ModifyVarName change result bind");
		ChangeBind(varName, varNewName, vmIO2?.TypeName);
		LogHelper.Info("ModifyVarName change runparam bind");
		SetRunParamBindForName(varNewName, vmIO2?.TypeName, runParaminfo);
	}

	public void ModifyVarCommInit(string varName, bool isInit)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870888);
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_COMMENABLE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256)
		};
		string str = (isInit ? "true" : "false");
		iMVS_MODIFY_VAR.chszChangeValue = StringUtil.UTF8GetFixLenBytes(str, 256);
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		SetBinaryData("ModifyVar", value);
	}

	private void SetRunParamBindForName(string varName, string varType, VarRunParamInfo[] linkModule)
	{
		if (linkModule == null)
		{
			return;
		}
		List<int> BindListId = new List<int>();
		Array.ForEach(linkModule, (VarRunParamInfo x) =>
		{
			BindListId.Add(x.nModuleID);
		});
		List<VmModule> vmModulesByIDArray = GetVmModulesByIDArray(BindListId);
		foreach (VarRunParamInfo item in linkModule)
		{
			if (!item.IsSupportSub || varType != item.Type)
			{
				continue;
			}
			VmModule vmModule = vmModulesByIDArray.FirstOrDefault((VmModule p) => p.ID == item.nModuleID);
			if (vmModule != null)
			{
				if (vmModule.ParamSubManager.SetModuleParamBinding(item.Name, item.Type, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", DataSource.ID, varName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL, "All"))
				{
					vmModule.UserData["@" + item.Name] = StringUtil.UTF8GetBytesPadZero("true");
					(item.Owner as ISubInfo).SetInit();
				}
				else
				{
					LogHelper.Error("SetModuleParamBinding is failed,name:" + item.Name);
				}
			}
		}
	}

	private void SetRunParamBind(string varName, string varType, VarRunParamInfo[] oldModule, VarRunParamInfo[] linkModule)
	{
		if (oldModule != null)
		{
			List<int> BindListId = new List<int>();
			Array.ForEach(oldModule, (VarRunParamInfo x) =>
			{
				BindListId.Add(x.nModuleID);
			});
			List<VmModule> vmModulesByIDArray = GetVmModulesByIDArray(BindListId);
			foreach (VarRunParamInfo item in oldModule)
			{
				if (!item.IsSupportSub)
				{
					continue;
				}
				VmModule vmModule = vmModulesByIDArray.FirstOrDefault((VmModule p) => p.ID == item.nModuleID);
				if (vmModule != null)
				{
					if (vmModule.ParamSubManager.DelModuleParamBinding(item.Name, item.Type))
					{
						vmModule.UserData["@" + item.Name] = StringUtil.UTF8GetBytesPadZero("false");
						(item.Owner as ISubInfo).SetInit();
					}
					else
					{
						LogHelper.Error("DelModuleParamBinding is failed,name:" + item.Name);
					}
				}
			}
		}
		if (linkModule == null)
		{
			return;
		}
		List<int> BindListId2 = new List<int>();
		Array.ForEach(linkModule, (VarRunParamInfo x) =>
		{
			BindListId2.Add(x.nModuleID);
		});
		List<VmModule> vmModulesByIDArray2 = GetVmModulesByIDArray(BindListId2);
		foreach (VarRunParamInfo item2 in linkModule)
		{
			if (!item2.IsSupportSub || varType != item2.Type)
			{
				continue;
			}
			VmModule vmModule2 = vmModulesByIDArray2.FirstOrDefault((VmModule p) => p.ID == item2.nModuleID);
			if (vmModule2 == null)
			{
				continue;
			}
			string moduleParamBinding = vmModule2.ParamSubManager.GetModuleParamBinding(item2.Name);
			if (!string.IsNullOrEmpty(moduleParamBinding))
			{
				string[] array = moduleParamBinding.Split(' ');
				if (array.Length > 1 && int.TryParse(array[0], out var result) && result != DataSource.ID && (ServerSDKManager.serverSDKManager.mModuleManager.CheckLocalVariableModule(result) || result == 13000))
				{
					string text = array[1].Split('.')[1];
					int num3 = text.IndexOf('[', 0);
					if (num3 > 0)
					{
						text = text.Substring(0, num3);
						VariableManager variableManager = new VariableManager();
						variableManager.ModifyLinkModuleByParamBind(text, new VarRunParamInfo
						{
							nModuleID = (int)vmModule2.ID,
							Name = item2.Name,
							Type = item2.Type
						}, isBind: false, result);
					}
				}
			}
			if (vmModule2.ParamSubManager.SetModuleParamBinding(item2.Name, item2.Type, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", DataSource.ID, varName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL, "All"))
			{
				vmModule2.UserData["@" + item2.Name] = StringUtil.UTF8GetBytesPadZero("true");
				(item2.Owner as ISubInfo).SetInit();
			}
			else
			{
				LogHelper.Error("SetModuleParamBinding is failed,name:" + item2.Name);
			}
		}
	}

	public void ModifyLinkModuleForSub(string varName, string varType, VarRunParamInfo[] oldModule, VarRunParamInfo[] linkModule)
	{
		ModifyLinkModule(varName, linkModule);
		SetRunParamBind(varName, varType, oldModule, linkModule);
	}

	public void ModifyLinkModuleByParamBind(string varName, VarRunParamInfo linkModule, bool isBind, int varID = -1)
	{
		if (string.IsNullOrEmpty(varName) || linkModule == null)
		{
			LogHelper.Error("param is error");
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_LINKMODULEBYPARAM,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszChangeValue = StringUtil.UTF8GetFixLenBytes(isBind ? "bind" : "unbind", 256)
		};
		IntPtr zero = IntPtr.Zero;
		List<IMVS_LINKMODULE> list = new List<IMVS_LINKMODULE>();
		list.Add(new IMVS_LINKMODULE
		{
			chszRunParamName = StringUtil.UTF8GetFixLenBytes(linkModule.Name, 128),
			chszRunParamType = StringUtil.UTF8GetFixLenBytes(linkModule.Type, 16),
			nModuleID = linkModule.nModuleID
		});
		byte[] array = ExternalCallHelper.StructsToBytes(list);
		zero = Marshal.AllocHGlobal(array.Length);
		Marshal.Copy(array, 0, zero, array.Length);
		iMVS_MODIFY_VAR.pBinaryValue = zero;
		iMVS_MODIFY_VAR.nValueNum = 1u;
		byte[] array2 = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			if (DataSource != null)
			{
				((IUserBytesData)DataSource.Params)["ModifyVar"] = array2;
				return;
			}
			int num = ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData((uint)varID, "ModifyVar", array2);
			if (num == 0)
			{
				return;
			}
			throw new VmException(num);
		}
		catch (VmException ex)
		{
			LogHelper.Error("ModifyVar is exception:" + ex.errorCode);
		}
		finally
		{
			if (zero != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(zero);
				zero = IntPtr.Zero;
			}
		}
	}

	public void ModifyLinkModule(string varName, VarRunParamInfo[] linkModule)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870888);
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_LINKMODULE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256)
		};
		IntPtr intPtr = IntPtr.Zero;
		if (linkModule == null || linkModule.Length == 0)
		{
			iMVS_MODIFY_VAR.nValueNum = 0u;
		}
		else
		{
			List<IMVS_LINKMODULE> imvsModules = new List<IMVS_LINKMODULE>();
			Array.ForEach(linkModule, (VarRunParamInfo x) =>
			{
				imvsModules.Add(new IMVS_LINKMODULE
				{
					chszRunParamName = StringUtil.UTF8GetFixLenBytes(x.Name, 128),
					chszRunParamType = StringUtil.UTF8GetFixLenBytes(x.Type, 16),
					nModuleID = x.nModuleID
				});
			});
			byte[] array = ExternalCallHelper.StructsToBytes(imvsModules);
			intPtr = Marshal.AllocHGlobal(array.Length);
			Marshal.Copy(array, 0, intPtr, array.Length);
			iMVS_MODIFY_VAR.pBinaryValue = intPtr;
			iMVS_MODIFY_VAR.nValueNum = (uint)linkModule.Length;
		}
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			((IUserBytesData)DataSource.Params)["ModifyVar"] = value;
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
				intPtr = IntPtr.Zero;
			}
		}
	}

	public void ModifyIndex(string varName, int nIndex, bool bSetTop = false, int nStartIndex = 0)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870888);
		}
		varName = RepairVarName(varName);
		byte[] value = ExternalCallHelper.StructToBytes(new IMVS_MODIFY_VAR
		{
			enumAction = (bSetTop ? MODIFYACTION.VAR_SETTOP : MODIFYACTION.VAR_INDEX),
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszChangeValue = (bSetTop ? StringUtil.UTF8GetFixLenBytes($"{nStartIndex}_{nIndex}", 256) : StringUtil.UTF8GetFixLenBytes(nIndex.ToString(), 256))
		}, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		((IUserBytesData)DataSource.Params)["ModifyVar"] = value;
	}

	public void GetGroupInfo(List<string> groupInfo)
	{
		if (groupInfo == null)
		{
			groupInfo = new List<string>();
		}
		string text = ((IUserStringData)DataSource.Params)["VarGroupInfo"];
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		string[] array = text.Split('\v');
		Array.ForEach(array, (string x) =>
		{
			if (!string.IsNullOrEmpty(x))
			{
				groupInfo.Add(x);
			}
		});
	}

	public void ExportVar(string strExportPath)
	{
		if (string.IsNullOrEmpty(strExportPath))
		{
			throw new VmException(-536870911);
		}
		((IUserBytesData)DataSource.Params)["GlobalValueExport"] = StringUtil.UTF8GetBytesPadZero(strExportPath);
	}

	public void ImportVar(string strImportFilePath, bool isFromUI = true, Dictionary<string, VarRunParamInfo[]> runParaminfo = null, Action delIOAct = null)
	{
		if (string.IsNullOrEmpty(strImportFilePath))
		{
			throw new VmException(-536870911);
		}
		((IUserBytesData)DataSource.Params)["GlobalValueImport"] = StringUtil.UTF8GetBytesPadZero(strImportFilePath);
		for (int num = DataSource.Outputs.Count - 1; num >= 0; num--)
		{
			VmIO vmIO = DataSource.Outputs[num];
			if (vmIO.CustomVisibleA)
			{
				try
				{
					UnBindGlobarVarItem(vmIO.Name, isFromUI);
				}
				catch (Exception ex)
				{
					if (ex is VmException ex2)
					{
						LogHelper.Error("UnBindingGlobalVariable is error:" + ex2.errorMessage);
					}
					else
					{
						LogHelper.Error("UnBindingGlobalVariable is error:" + ex.ToString());
					}
				}
				ChangeToSendReceiveData(vmIO.Index);
				if (runParaminfo.ContainsKey(vmIO.Name))
				{
					SetRunParamBind(vmIO.Name, vmIO.TypeName, runParaminfo[vmIO.Name], null);
				}
			}
		}
		delIOAct?.Invoke();
		if (isFromUI)
		{
			ModuleVarDict.Clear();
		}
		List<VarItem> varList = new List<VarItem>();
		GetAllVar(ref varList);
		foreach (VarItem item in varList)
		{
			object ioType = GetIoType(item.Type);
			if (ioType == null)
			{
				throw new Exception("type is not support");
			}
			List<string> leafIoNames = null;
			if (item.IsCombination)
			{
				leafIoNames = item.SubIOInfos.Select((VarItem x) => x.Name).ToList();
			}
			VmIO vmIO2 = AddIoByType(ioType, item.Name, DataSource.Outputs, leafIoNames);
			if (vmIO2 != null)
			{
				vmIO2.Index = item.Index;
			}
		}
	}

	public void AddGroup(string groupName)
	{
		if (string.IsNullOrEmpty(groupName))
		{
			throw new VmException(-536870888);
		}
		((IUserStringData)DataSource.Params)["AddGroup"] = groupName;
	}

	public void DeleteGroup(string groupName)
	{
		if (string.IsNullOrEmpty(groupName))
		{
			throw new VmException(-536870888);
		}
		((IUserStringData)DataSource.Params)["DeleteGroup"] = groupName;
	}

	public void ReNameGroup(string oldGroupName, string newGroupName)
	{
		if (string.IsNullOrEmpty(oldGroupName) || string.IsNullOrEmpty(newGroupName))
		{
			throw new VmException(-536870888);
		}
		((IUserStringData)DataSource.Params)["ReNameGroup"] = oldGroupName + "\v" + newGroupName;
	}

	public void SetDefaultGroupInfo(string groupName)
	{
		if (string.IsNullOrEmpty(groupName))
		{
			throw new VmException(-536870888);
		}
		((IUserStringData)DataSource.Params)["InitGroupInfo"] = groupName;
	}

	private byte[] GetBinaryData(string paramName)
	{
		byte[] data = null;
		int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(DataSource.ID, paramName, ref data);
		if (binaryData != 0)
		{
			throw new VmException(binaryData);
		}
		return data;
	}

	private void SetBinaryData(string paramName, byte[] value)
	{
		int num = ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData(DataSource.ID, paramName, value);
		if (num != 0)
		{
			throw new VmException(num);
		}
	}

	private int GetStringBinaryData(uint moduleId, string paramName, ref byte[] data, uint recvTimeOut = 3000u)
	{
		ImvsSdkDefine.IMVS_GET_BINARY_LENGTH_INFO strValue = new ImvsSdkDefine.IMVS_GET_BINARY_LENGTH_INFO
		{
			strName = StringUtil.UTF8GetFixLenBytes(paramName, 256)
		};
		int num = ServerSDKAPI.IMVS_GetBinaryLength(CVmObjectBase.mhandle, moduleId, ref strValue, recvTimeOut);
		if (num != 0)
		{
			return num;
		}
		if (strValue.nBinaryLength == 0)
		{
			return 0;
		}
		ImvsSdkDefine.IMVS_GET_BINARY_DATA_INFO strValue2 = new ImvsSdkDefine.IMVS_GET_BINARY_DATA_INFO
		{
			strName = StringUtil.UTF8GetFixLenBytes(paramName, 256),
			nBinaryDataMallocSize = strValue.nBinaryLength,
			nBinaryLength = strValue.nBinaryLength
		};
		data = new byte[strValue.nBinaryLength];
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try
		{
			strValue2.pBinaryData = gCHandle.AddrOfPinnedObject();
			if (!gCHandle.IsAllocated)
			{
				return -536870911;
			}
			num = ServerSDKAPI.IMVS_GetBinaryData(CVmObjectBase.mhandle, moduleId, ref strValue2, recvTimeOut);
			if (num != 0)
			{
				data = null;
				return num;
			}
			gCHandle.Free();
		}
		catch (Exception)
		{
			throw;
		}
		finally
		{
			if (gCHandle.IsAllocated)
			{
				gCHandle.Free();
			}
		}
		return 0;
	}

	public string GetVarValueString(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			throw new VmException(-536870911);
		}
		string text = "";
		string paramName = "GlobalScipt-" + varName;
		byte[] data = new byte[0];
		int stringBinaryData = GetStringBinaryData(13000u, paramName, ref data);
		if (stringBinaryData != 0)
		{
			VmException ex = new VmException(stringBinaryData);
			throw ex;
		}
		return StringUtil.UTF8GetString(data);
	}

	public int SetVarValue(string varName, string varValue)
	{
		if (string.IsNullOrEmpty(varName) || varValue == null)
		{
			return -536870911;
		}
		varName = RepairVarName(varName);
		string text = "GlobalScipt-" + varName;
		int num = 0;
		return (Encoding.UTF8.GetByteCount(varValue) <= 256) ? ServerSDKAPI.IMVS_SetParamValue(CVmObjectBase.mhandle, DataSource.ID, text, varValue) : ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData(DataSource.ID, text, StringUtil.UTF8GetBytesPadZero(varValue));
	}

	public void SetVarIntValue(string varName, int[] intList, string subIOName = "")
	{
		if (string.IsNullOrEmpty(varName) || intList == null || intList.Length < 1)
		{
			throw new VmException(-536870911);
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_VALUE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			chszChangeValue = StringUtil.UTF8GetFixLenBytes("int", 256)
		};
		if (!string.IsNullOrEmpty(subIOName))
		{
			iMVS_MODIFY_VAR.chszSubVarName = StringUtil.UTF8GetFixLenBytes(subIOName, 256);
		}
		iMVS_MODIFY_VAR.nValueNum = (uint)intList.Length;
		List<byte> list = new List<byte>();
		foreach (int value in intList)
		{
			list.AddRange(BitConverter.GetBytes(value));
		}
		IntPtr intPtr = Marshal.AllocHGlobal((int)(iMVS_MODIFY_VAR.nValueNum * Marshal.SizeOf(typeof(int))));
		Marshal.Copy(list.ToArray(), 0, intPtr, list.Count);
		iMVS_MODIFY_VAR.pBinaryValue = intPtr;
		byte[] value2 = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value2);
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
			intPtr = IntPtr.Zero;
		}
	}

	public void SetVarFloatValue(string varName, float[] floatList, string subIOName = "")
	{
		if (string.IsNullOrEmpty(varName) || floatList == null || floatList.Length < 1)
		{
			throw new VmException(-536870911);
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_VALUE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256)
		};
		if (!string.IsNullOrEmpty(subIOName))
		{
			iMVS_MODIFY_VAR.chszSubVarName = StringUtil.UTF8GetFixLenBytes(subIOName, 256);
		}
		iMVS_MODIFY_VAR.chszChangeValue = StringUtil.UTF8GetFixLenBytes("float", 256);
		iMVS_MODIFY_VAR.nValueNum = (uint)floatList.Length;
		List<byte> list = new List<byte>();
		foreach (float value in floatList)
		{
			list.AddRange(BitConverter.GetBytes(value));
		}
		IntPtr intPtr = Marshal.AllocHGlobal((int)(iMVS_MODIFY_VAR.nValueNum * Marshal.SizeOf(typeof(float))));
		Marshal.Copy(list.ToArray(), 0, intPtr, list.Count);
		iMVS_MODIFY_VAR.pBinaryValue = intPtr;
		byte[] value2 = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value2);
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
			intPtr = IntPtr.Zero;
		}
	}

	public void SetVarStringValue(string varName, string[] stringList, string subIOName = "")
	{
		if (string.IsNullOrEmpty(varName) || stringList == null || stringList.Length < 1)
		{
			throw new VmException(-536870911);
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_VALUE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256)
		};
		if (!string.IsNullOrEmpty(subIOName))
		{
			iMVS_MODIFY_VAR.chszSubVarName = StringUtil.UTF8GetFixLenBytes(subIOName, 256);
		}
		iMVS_MODIFY_VAR.chszChangeValue = StringUtil.UTF8GetFixLenBytes("string", 256);
		iMVS_MODIFY_VAR.nValueNum = (uint)stringList.Length;
		ByteDataArray[] array = new ByteDataArray[stringList.Length];
		for (int i = 0; i < stringList.Length; i++)
		{
			byte[] array2 = StringUtil.UTF8GetBytesPadZero(stringList[i]);
			array[i].pData = Marshal.AllocHGlobal(array2.Length);
			array[i].nDataLen = (uint)array2.Length;
			array[i].nDataSize = (uint)array2.Length;
			Marshal.Copy(array2, 0, array[i].pData, array2.Length);
		}
		List<byte> list = new List<byte>();
		ByteDataArray[] array3 = array;
		foreach (ByteDataArray byteDataArray in array3)
		{
			list.AddRange(DataTransf.Structure2Bytes(byteDataArray));
		}
		IntPtr intPtr = Marshal.AllocHGlobal((int)(iMVS_MODIFY_VAR.nValueNum * Marshal.SizeOf(typeof(ByteDataArray))));
		Marshal.Copy(list.ToArray(), 0, intPtr, list.Count);
		iMVS_MODIFY_VAR.pBinaryValue = intPtr;
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value);
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			for (int k = 0; k < array.Length; k++)
			{
				Marshal.FreeHGlobal(array[k].pData);
				array[k].pData = IntPtr.Zero;
			}
			Marshal.FreeHGlobal(intPtr);
			intPtr = IntPtr.Zero;
		}
	}

	public void SetVarByteValue(string varName, byte[] byteList, string subIOName = "")
	{
		if (string.IsNullOrEmpty(varName) || byteList == null || byteList.Length < 1)
		{
			throw new VmException(-536870911);
		}
		varName = RepairVarName(varName);
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_VALUE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256)
		};
		if (!string.IsNullOrEmpty(subIOName))
		{
			iMVS_MODIFY_VAR.chszSubVarName = StringUtil.UTF8GetFixLenBytes(subIOName, 256);
		}
		iMVS_MODIFY_VAR.chszChangeValue = StringUtil.UTF8GetFixLenBytes("byte", 256);
		IntPtr intPtr = Marshal.AllocHGlobal(byteList.Length);
		Marshal.Copy(byteList, 0, intPtr, byteList.Length);
		iMVS_MODIFY_VAR.nValueNum = (uint)byteList.Length;
		iMVS_MODIFY_VAR.pBinaryValue = intPtr;
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value);
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
			intPtr = IntPtr.Zero;
		}
	}

	private void SetVarImageValue(string varName, IntPtr ptrData, int nDataLen, string subIOName = "")
	{
		if (string.IsNullOrEmpty(varName) || nDataLen < 1)
		{
			throw new VmException(-536870911);
		}
		IMVS_MODIFY_VAR iMVS_MODIFY_VAR = new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_VALUE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256)
		};
		if (!string.IsNullOrEmpty(subIOName))
		{
			iMVS_MODIFY_VAR.chszSubVarName = StringUtil.UTF8GetFixLenBytes(subIOName, 256);
		}
		if (!string.IsNullOrEmpty(subIOName))
		{
			iMVS_MODIFY_VAR.chszSubVarName = StringUtil.UTF8GetFixLenBytes(subIOName, 256);
		}
		iMVS_MODIFY_VAR.nValueNum = (uint)nDataLen;
		iMVS_MODIFY_VAR.pBinaryValue = ptrData;
		byte[] value = ExternalCallHelper.StructToBytes(iMVS_MODIFY_VAR, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value);
		}
		catch (VmException ex)
		{
			throw ex;
		}
	}

	private void GetOutPutIO(string strParaName, Action<ImvsSdkDefine.IMVS_MODULE_IO_INFO> act)
	{
		int num = -536870657;
		IntPtr pstModuIOInfo = IntPtr.Zero;
		ImvsSdkDefine.IMVS_MODULE_IO_INFO_LIST structure = new ImvsSdkDefine.IMVS_MODULE_IO_INFO_LIST();
		num = ServerSDKAPI.IMVS_GetModuOutputIOInfo(CVmObjectBase.mhandle, DataSource.ID, 2u, ref pstModuIOInfo);
		if (num != 0)
		{
			VmException ex = new VmException(num);
			throw ex;
		}
		if (IntPtr.Zero == pstModuIOInfo)
		{
			VmException ex2 = new VmException(-536870910);
			throw ex2;
		}
		ExternalCallHelper.PtrToStructureWithoutFree(pstModuIOInfo, ref structure);
		Marshal.FreeHGlobal(pstModuIOInfo);
		pstModuIOInfo = IntPtr.Zero;
		bool flag = false;
		for (int i = 0; i < structure.nNum; i++)
		{
			ImvsSdkDefine.IMVS_MODULE_IO_INFO obj = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(structure.pstModuIoInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
			string text = Encoding.UTF8.GetString(obj.szParamName, 0, obj.szParamName.Length).TrimEnd(default(char));
			if (strParaName == text)
			{
				act?.Invoke(obj);
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			VmException ex3 = new VmException(-536870881);
			throw ex3;
		}
	}

	public void SetVarImageValue(string varName, ImageBaseData stImageData)
	{
		if (string.IsNullOrEmpty(varName) || stImageData == null || (17301505 == stImageData.Pixelformat && (int)stImageData.DataLen < stImageData.Width * stImageData.Height) || ((35127316 == stImageData.Pixelformat || 35127329 == stImageData.Pixelformat) && (int)stImageData.DataLen < stImageData.Width * stImageData.Height * 3))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strImageParam = new string[1];
		string[] strIntParam = new string[3];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("IMAGE" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex3 = new VmException(-536870129);
				throw ex3;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (1 != iMVS_MODULE_IO_INFO.nDataType)
				{
					VmException ex4 = new VmException(-536870129);
					throw ex4;
				}
				if ("int" == Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
				{
					strIntParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
				else
				{
					if (!("image" == Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char))))
					{
						VmException ex5 = new VmException(-536870129);
						throw ex5;
					}
					strImageParam[0] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
			}
		});
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[4];
		string str = strIntParam[0];
		array[0].chszValueName = StringUtil.UTF8GetFixLenBytes(str, 256);
		array[0].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_INT;
		array[0].nValueNum = 1u;
		array[0].pIntValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(int)) * array[0].nValueNum));
		List<byte> list = new List<byte>();
		list.AddRange(DataTransf.IntToByte(stImageData.Width));
		Marshal.Copy(list.ToArray(), 0, array[0].pIntValue, list.Count);
		string str2 = strIntParam[1];
		array[1].chszValueName = StringUtil.UTF8GetFixLenBytes(str2, 256);
		array[1].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_INT;
		array[1].nValueNum = 1u;
		array[1].pIntValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(int)) * array[1].nValueNum));
		list = new List<byte>();
		list.AddRange(DataTransf.IntToByte(stImageData.Height));
		Marshal.Copy(list.ToArray(), 0, array[1].pIntValue, list.Count);
		string str3 = strIntParam[2];
		array[2].chszValueName = StringUtil.UTF8GetFixLenBytes(str3, 256);
		array[2].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_INT;
		array[2].nValueNum = 1u;
		int num = 0;
		if (17301505 == stImageData.Pixelformat || 35127316 == stImageData.Pixelformat || 35127329 == stImageData.Pixelformat)
		{
			num = stImageData.Pixelformat;
			array[2].pIntValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(int)) * array[2].nValueNum));
			list = new List<byte>();
			list.AddRange(DataTransf.IntToByte(num));
			Marshal.Copy(list.ToArray(), 0, array[2].pIntValue, list.Count);
			array[3].chszValueName = StringUtil.UTF8GetFixLenBytes(strImageParam[0], 256);
			array[3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_IMAGE;
			array[3].nValueNum = 1u;
			ByteDataArray[] array2 = new ByteDataArray[array[3].nValueNum];
			for (int num2 = 0; num2 < array2.Length; num2++)
			{
				array2[num2].pData = stImageData.ImageDataPtr;
				array2[num2].nDataLen = stImageData.DataLen;
			}
			list = new List<byte>();
			ByteDataArray[] array3 = array2;
			foreach (ByteDataArray byteDataArray in array3)
			{
				list.AddRange(DataTransf.Structure2Bytes(byteDataArray));
			}
			IntPtr intPtr = Marshal.AllocHGlobal(array2.Length * Marshal.SizeOf(typeof(ByteDataArray)));
			Marshal.Copy(list.ToArray(), 0, intPtr, list.Count);
			array[3].pByteValue = intPtr;
			SetSubIOInfo(text, array);
			return;
		}
		ReleaseIntptr(array[0].pIntValue);
		ReleaseIntptr(array[1].pIntValue);
		VmException ex2 = new VmException(-536870911);
		throw ex2;
	}

	public void SetVarPointValue(string varName, List<VM.PlatformSDKCS.PointF> stPointF)
	{
		if (string.IsNullOrEmpty(varName) || stPointF == null || stPointF.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strFloatParam = new string[2];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("POINT" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num2 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (1 != iMVS_MODULE_IO_INFO.nDataType)
				{
					VmException ex3 = new VmException(-536870129);
					throw ex3;
				}
				if ("float" == Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
				{
					strFloatParam[num2++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
			}
		});
		float[] array = new float[stPointF.Count];
		float[] array2 = new float[stPointF.Count];
		for (int num = 0; num < stPointF.Count; num++)
		{
			array[num] = stPointF[num].X;
			array2[num] = stPointF[num].Y;
		}
		IMVS_SUB_VALUE[] array3 = new IMVS_SUB_VALUE[2];
		string str = strFloatParam[0];
		array3[0].chszValueName = StringUtil.UTF8GetFixLenBytes(str, 256);
		array3[0].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
		array3[0].nValueNum = (uint)array.Length;
		array3[0].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array3[0].nValueNum));
		Marshal.Copy(array, 0, array3[0].pFloatValue, array.Length);
		string str2 = strFloatParam[1];
		array3[1].chszValueName = StringUtil.UTF8GetFixLenBytes(str2, 256);
		array3[1].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
		array3[1].nValueNum = (uint)array2.Length;
		array3[1].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array3[1].nValueNum));
		Marshal.Copy(array2, 0, array3[1].pFloatValue, array2.Length);
		SetSubIOInfo(text, array3);
	}

	public void SetVarBox(string varName, List<RectBox> stRoiBox)
	{
		if (string.IsNullOrEmpty(varName) || stRoiBox == null || stRoiBox.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[5];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("ROIBOX" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
				else if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex4 = new VmException(-536870129);
						throw ex4;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex5 = new VmException(-536870129);
							throw ex5;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stRoiBox.Count]);
		}
		for (int num2 = 0; num2 < stRoiBox.Count; num2++)
		{
			list[0][num2] = stRoiBox[num2].CenterPoint.X;
			list[1][num2] = stRoiBox[num2].CenterPoint.Y;
			list[2][num2] = stRoiBox[num2].BoxWidth;
			list[3][num2] = stRoiBox[num2].BoxHeight;
			list[4][num2] = stRoiBox[num2].Angle;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarAnnulus(string varName, List<Annulus> stAnnulus)
	{
		if (string.IsNullOrEmpty(varName) || stAnnulus == null || stAnnulus.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[6];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("ROIANNULUS" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
				else if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex4 = new VmException(-536870129);
						throw ex4;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex5 = new VmException(-536870129);
							throw ex5;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stAnnulus.Count]);
		}
		for (int num2 = 0; num2 < stAnnulus.Count; num2++)
		{
			list[0][num2] = stAnnulus[num2].CenterPoint.X;
			list[1][num2] = stAnnulus[num2].CenterPoint.Y;
			list[2][num2] = stAnnulus[num2].InnerRadius;
			list[3][num2] = stAnnulus[num2].OuterRadius;
			list[4][num2] = stAnnulus[num2].StartAngle;
			list[5][num2] = stAnnulus[num2].AngleExtend;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarCircle(string varName, List<Circle> stCircle)
	{
		if (string.IsNullOrEmpty(varName) || stCircle == null || stCircle.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[3];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("CIRCLE" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex4 = new VmException(-536870129);
							throw ex4;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
				else if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex5 = new VmException(-536870129);
						throw ex5;
					}
					strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stCircle.Count]);
		}
		for (int num2 = 0; num2 < stCircle.Count; num2++)
		{
			list[0][num2] = stCircle[num2].Radius;
			list[1][num2] = stCircle[num2].CenterPoint.X;
			list[2][num2] = stCircle[num2].CenterPoint.Y;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarEllipse(string varName, List<Ellipse> stEllipse)
	{
		if (string.IsNullOrEmpty(varName) || stEllipse == null || stEllipse.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[5];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("ELLIPSE" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex4 = new VmException(-536870129);
							throw ex4;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
				else if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex5 = new VmException(-536870129);
						throw ex5;
					}
					strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stEllipse.Count]);
		}
		for (int num2 = 0; num2 < stEllipse.Count; num2++)
		{
			list[0][num2] = stEllipse[num2].CenterPoint.X;
			list[1][num2] = stEllipse[num2].CenterPoint.Y;
			list[2][num2] = stEllipse[num2].MajorRadius;
			list[3][num2] = stEllipse[num2].MinorRadius;
			list[4][num2] = stEllipse[num2].Angle;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarLine(string varName, List<Line> stLine)
	{
		if (string.IsNullOrEmpty(varName) || stLine == null || stLine.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[4];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("LINE" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex4 = new VmException(-536870129);
							throw ex4;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stLine.Count]);
		}
		for (int num2 = 0; num2 < stLine.Count; num2++)
		{
			list[0][num2] = stLine[num2].StartPoint.X;
			list[1][num2] = stLine[num2].StartPoint.Y;
			list[2][num2] = stLine[num2].EndPoint.X;
			list[3][num2] = stLine[num2].EndPoint.Y;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarRect(string varName, List<RectF> stRectF)
	{
		if (string.IsNullOrEmpty(varName) || stRectF == null || stRectF.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[4];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("Rect" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
				else if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex4 = new VmException(-536870129);
						throw ex4;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex5 = new VmException(-536870129);
							throw ex5;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stRectF.Count]);
		}
		for (int num2 = 0; num2 < stRectF.Count; num2++)
		{
			list[0][num2] = stRectF[num2].RectPoint.X;
			list[1][num2] = stRectF[num2].RectPoint.Y;
			list[2][num2] = stRectF[num2].RectWidth;
			list[3][num2] = stRectF[num2].RectHeight;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarPointset(string varName, List<MatchOutline> stMatchOutline)
	{
	}

	public void SetVarFixture(string varName, List<Fixture> stFixture)
	{
		if (string.IsNullOrEmpty(varName) || stFixture == null || stFixture.Count == 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[10];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("FIXTURE" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num4 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
				else if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex4 = new VmException(-536870129);
						throw ex4;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex5 = new VmException(-536870129);
							throw ex5;
						}
						strParam[num4++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
			}
		});
		List<float[]> list = new List<float[]>();
		for (int num = 0; num < strParam.Length; num++)
		{
			list.Add(new float[stFixture.Count]);
		}
		for (int num2 = 0; num2 < stFixture.Count; num2++)
		{
			list[0][num2] = stFixture[num2].stInitPoint.X;
			list[1][num2] = stFixture[num2].stInitPoint.Y;
			list[2][num2] = stFixture[num2].fInitAngle;
			list[3][num2] = stFixture[num2].fInitScaleX;
			list[4][num2] = stFixture[num2].fInitScaleY;
			list[5][num2] = stFixture[num2].stRunPoint.X;
			list[6][num2] = stFixture[num2].stRunPoint.Y;
			list[7][num2] = stFixture[num2].fRunAngle;
			list[8][num2] = stFixture[num2].fRunScaleX;
			list[9][num2] = stFixture[num2].fRunScaleY;
		}
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[list.Count];
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num3], 256);
			array[num3].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array[num3].nValueNum = (uint)list[num3].Length;
			array[num3].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[num3].nValueNum));
			Marshal.Copy(list[num3], 0, array[num3].pFloatValue, list[num3].Length);
		}
		SetSubIOInfo(text, array);
	}

	public void SetVarPolygon(string varName, List<Polygon> stPolygon)
	{
		if (string.IsNullOrEmpty(varName) || stPolygon == null || stPolygon.Count <= 0)
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string text = RepairVarName(varName);
		string[] strParam = new string[3];
		GetOutPutIO(text, (ImvsSdkDefine.IMVS_MODULE_IO_INFO stModuleIOInfo) =>
		{
			if ("ROIPOLYGON" != Encoding.Default.GetString(stModuleIOInfo.szParamType).TrimEnd(default(char)))
			{
				VmException ex2 = new VmException(-536870129);
				throw ex2;
			}
			int num8 = 0;
			for (int i = 0; i < stModuleIOInfo.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(stModuleIOInfo.pstCombInfo + i * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
				if (2 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("POINT" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex3 = new VmException(-536870129);
						throw ex3;
					}
					for (int j = 0; j < iMVS_MODULE_IO_INFO.nNum; j++)
					{
						ImvsSdkDefine.IMVS_MODULE_IO_INFO iMVS_MODULE_IO_INFO2 = (ImvsSdkDefine.IMVS_MODULE_IO_INFO)Marshal.PtrToStructure(iMVS_MODULE_IO_INFO.pstCombInfo + j * Marshal.SizeOf(typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO)), typeof(ImvsSdkDefine.IMVS_MODULE_IO_INFO));
						if ("float" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamType).TrimEnd(default(char)))
						{
							VmException ex4 = new VmException(-536870129);
							throw ex4;
						}
						strParam[num8++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO2.szParamName).TrimEnd(default(char));
					}
				}
				else if (1 == iMVS_MODULE_IO_INFO.nDataType)
				{
					if ("int" != Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamType).TrimEnd(default(char)))
					{
						VmException ex5 = new VmException(-536870129);
						throw ex5;
					}
					strParam[num8++] = Encoding.Default.GetString(iMVS_MODULE_IO_INFO.szParamName).TrimEnd(default(char));
				}
			}
		});
		int[] array = new int[stPolygon.Count];
		int num = 0;
		for (int num2 = 0; num2 < stPolygon.Count; num2++)
		{
			array[num2] = stPolygon[num2].VertexPoints.Length;
			num = array[num2] + num;
		}
		List<float[]> list = new List<float[]>();
		int num3 = 0;
		for (int num4 = 0; num4 < 2; num4++)
		{
			list.Add(new float[num]);
		}
		for (int num5 = 0; num5 < stPolygon.Count; num5++)
		{
			for (int num6 = 0; num6 < stPolygon[num5].VertexPoints.Length; num6++)
			{
				num3 = num5 * stPolygon[num5].VertexPoints.Length + num6;
				list[0][num3] = stPolygon[num5].VertexPoints[num6].X;
				list[1][num3] = stPolygon[num5].VertexPoints[num6].Y;
			}
		}
		IMVS_SUB_VALUE[] array2 = new IMVS_SUB_VALUE[3];
		array2[0].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[0], 256);
		array2[0].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_INT;
		array2[0].nValueNum = (uint)array.Length;
		array2[0].pIntValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(int)) * array2[0].nValueNum));
		Marshal.Copy(array, 0, array2[0].pIntValue, array.Length);
		for (int num7 = 1; num7 < array2.Length; num7++)
		{
			array2[num7].chszValueName = StringUtil.UTF8GetFixLenBytes(strParam[num7], 256);
			array2[num7].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
			array2[num7].nValueNum = (uint)list[num7 - 1].Length;
			array2[num7].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array2[num7].nValueNum));
			Marshal.Copy(list[num7 - 1], 0, array2[num7].pFloatValue, list[num7 - 1].Length);
		}
		SetSubIOInfo(text, array2);
	}

	private void SetSubIOInfo(string varName, IMVS_SUB_VALUE[] stSubIOInfo, Action releaseAct = null)
	{
		List<byte> list = new List<byte>();
		foreach (IMVS_SUB_VALUE iMVS_SUB_VALUE in stSubIOInfo)
		{
			list.AddRange(DataTransf.Structure2Bytes(iMVS_SUB_VALUE));
		}
		IntPtr intPtr = Marshal.AllocHGlobal(stSubIOInfo.Length * Marshal.SizeOf(typeof(IMVS_SUB_VALUE)));
		Marshal.Copy(list.ToArray(), 0, intPtr, list.Count);
		byte[] value = ExternalCallHelper.StructToBytes(new IMVS_MODIFY_VAR
		{
			enumAction = MODIFYACTION.VAR_VALUE,
			chszVarName = StringUtil.UTF8GetFixLenBytes(varName, 256),
			nValueNum = (uint)stSubIOInfo.Length,
			pBinaryValue = intPtr
		}, Marshal.SizeOf(typeof(IMVS_MODIFY_VAR)));
		try
		{
			SetBinaryData("ModifyVar", value);
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			releaseAct?.Invoke();
			for (int j = 0; j < stSubIOInfo.Length; j++)
			{
				ReleaseIntptr(stSubIOInfo[j].pIntValue);
				ReleaseIntptr(stSubIOInfo[j].pFloatValue);
				ReleaseIntptr(stSubIOInfo[j].pByteValue);
			}
			ReleaseIntptr(intPtr);
		}
	}

	private void CheckVarType(byte[] chszValueType, string typeName)
	{
		if (StringUtil.UTF8GetString(chszValueType) != typeName)
		{
			throw new VmException(-536870129);
		}
	}

	public string[] GetVarBaseTypeValue(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		VmIO vmIO = DataSource.Outputs.FirstOrDefault((VmIO x) => x.Name == varName);
		if (vmIO == null)
		{
			LogHelper.Error(varName + " name is not exit");
			return null;
		}
		if (vmIO.TypeName == "int")
		{
			return Array.ConvertAll(GetVarIntValue(varName).pIntVal, (int x) => x.ToString());
		}
		if (vmIO.TypeName == "float")
		{
			return Array.ConvertAll(GetVarFloatValue(varName).pFloatVal, (float x) => x.ToString());
		}
		if (vmIO.TypeName == "string")
		{
			return GetVarStringValue(varName);
		}
		return null;
	}

	public IntDataArray GetVarIntValue(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		IntDataArray result = default;
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "int");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				int[] pIntVal = ExternalCallHelper.IntPtr2Ts<int>(iMVS_VARIABLE.stVarValue.pIntValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				result.pIntVal = pIntVal;
				result.nValueNum = (int)iMVS_VARIABLE.stVarValue.nValueNum;
			}
		}
		return result;
	}

	public FloatDataArray GetVarFloatValue(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		FloatDataArray result = default;
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "float");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				float[] pFloatVal = ExternalCallHelper.IntPtr2Ts<float>(iMVS_VARIABLE.stVarValue.pFloatValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				result.pFloatVal = pFloatVal;
				result.nValueNum = (int)iMVS_VARIABLE.stVarValue.nValueNum;
			}
		}
		return result;
	}

	public string[] GetVarStringValue(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		string[] array = null;
		varName = RepairVarName(varName);
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "string");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				ByteDataArray[] array2 = ExternalCallHelper.IntPtr2Structures<ByteDataArray>(iMVS_VARIABLE.stVarValue.pByteValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				array = new string[iMVS_VARIABLE.stVarValue.nValueNum];
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					if (array2[i].nDataLen != 0)
					{
						byte[] array3 = new byte[array2[i].nDataLen];
						Marshal.Copy(array2[i].pData, array3, 0, array3.Length);
						string text = StringUtil.UTF8GetString(array3);
						array[i] = StringUtil.UTF8GetString(array3);
					}
				}
			}
		}
		return array;
	}

	public ByteArrayData GetVarByteStringValue(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		ByteArrayData result = default;
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "byte");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				ByteDataArray[] array = ExternalCallHelper.IntPtr2Structures<ByteDataArray>(iMVS_VARIABLE.stVarValue.pByteValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				if (array[0].nDataLen != 0)
				{
					byte[] array2 = new byte[array[0].nDataLen];
					Marshal.Copy(array[0].pData, array2, 0, array2.Length);
					result.nDataLen = array[0].nDataLen;
					result.pData = array2;
				}
			}
		}
		return result;
	}

	public ImageBaseData GetVarImageData(string varName, bool isMemoryCopy = false, bool isGetImage = true)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		ImageBaseData imageBaseData = new ImageBaseData();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "IMAGE");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_INT)
					{
						int num = 0;
						if (array[i].nValueNum != 0 && array[i].pIntValue != IntPtr.Zero)
						{
							int[] array2 = ExternalCallHelper.IntPtr2Ts<int>(array[i].pIntValue, (int)array[i].nValueNum);
							if (array2 == null || array2.Length < 0)
							{
								VmException ex2 = new VmException(-536870888);
								throw ex2;
							}
							num = array2[0];
							if (text.ToLower().Contains("width"))
							{
								imageBaseData.Width = num;
							}
							else if (text.ToLower().Contains("height"))
							{
								imageBaseData.Height = num;
							}
							else if (text.ToLower().Contains("format"))
							{
								imageBaseData.Pixelformat = num;
							}
						}
					}
					else
					{
						if (!((array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_IMAGE) & isGetImage))
						{
							continue;
						}
						if (isMemoryCopy)
						{
							byte[] varByteValueAlloc = GetVarByteValueAlloc(varName, text);
							if (varByteValueAlloc != null)
							{
								imageBaseData.ImageData = varByteValueAlloc;
								imageBaseData.DataLen = (uint)varByteValueAlloc.Length;
							}
						}
						else
						{
							if (array[i].nValueNum == 0 || !(array[i].pByteValue != IntPtr.Zero))
							{
								continue;
							}
							ByteDataArray[] array3 = ExternalCallHelper.IntPtr2Structures<ByteDataArray>(array[i].pByteValue, (int)array[i].nValueNum);
							ByteDataArray[] array4 = array3;
							for (int j = 0; j < array4.Length; j++)
							{
								ByteDataArray byteDataArray = array4[j];
								if (!(byteDataArray.pData == IntPtr.Zero) && byteDataArray.nDataLen != 0)
								{
									byte[] array5 = new byte[byteDataArray.nDataLen];
									Marshal.Copy(byteDataArray.pData, array5, 0, (int)byteDataArray.nDataLen);
									imageBaseData.ImageData = array5;
									imageBaseData.DataLen = byteDataArray.nDataLen;
									break;
								}
							}
						}
					}
				}
			}
		}
		return imageBaseData;
	}

	public List<VM.PlatformSDKCS.PointF> GetVarPointArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<VM.PlatformSDKCS.PointF> list = new List<VM.PlatformSDKCS.PointF>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "POINT");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				float[] array2 = new float[0];
				float[] array3 = new float[0];
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array4 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (text.ToLower().Contains("x"))
						{
							array2 = array4;
						}
						else if (text.ToLower().Contains("y"))
						{
							array3 = array4;
						}
					}
				}
				if (array2.Length != array3.Length)
				{
					VmException ex2 = new VmException(-536870888);
					throw ex2;
				}
				for (int j = 0; j < array2.Length; j++)
				{
					list.Add(new VM.PlatformSDKCS.PointF
					{
						X = array2[j],
						Y = array3[j]
					});
				}
			}
		}
		return list;
	}

	public List<Line> GetVarLineArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<Line> list = new List<Line>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "LINE");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 4)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					Line line = new Line
					{
						StartPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						EndPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[2][j],
							Y = list2[3][j]
						}
					};
					double num2 = Math.Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X) * 180.0 / Math.PI;
					line.Angle = (float)num2;
					list.Add(line);
				}
			}
		}
		return list;
	}

	public List<Circle> GetVarCircleArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<Circle> list = new List<Circle>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0 && StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueType).ToLower() == "circle")
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 3)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new Circle
					{
						CenterPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						Radius = list2[2][j]
					});
				}
			}
		}
		return list;
	}

	public List<Ellipse> GetVarEllipseArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<Ellipse> list = new List<Ellipse>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "ELLIPSE");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 5)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new Ellipse
					{
						CenterPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						MajorRadius = list2[2][j],
						MinorRadius = list2[3][j],
						Angle = list2[4][j]
					});
				}
			}
		}
		return list;
	}

	public List<RectBox> GetVarRectBoxArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<RectBox> list = new List<RectBox>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "ROIBOX");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 5)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new RectBox
					{
						CenterPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						BoxWidth = list2[2][j],
						BoxHeight = list2[3][j],
						Angle = list2[4][j]
					});
				}
			}
		}
		return list;
	}

	public List<RectF> GetVarRectFArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<RectF> list = new List<RectF>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "Rect");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 4)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new RectF
					{
						RectPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						RectWidth = list2[2][j],
						RectHeight = list2[3][j]
					});
				}
			}
		}
		return list;
	}

	public List<Annulus> GetVarAnnulusArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<Annulus> list = new List<Annulus>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "ROIANNULUS");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 6)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new Annulus
					{
						CenterPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						InnerRadius = list2[2][j],
						OuterRadius = list2[3][j],
						StartAngle = list2[4][j],
						AngleExtend = list2[5][j]
					});
				}
			}
		}
		return list;
	}

	public MatchOutline GetVarPointset(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		MatchOutline matchOutline = new MatchOutline();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "pointset");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				ByteDataArray[] array = ExternalCallHelper.IntPtr2Structures<ByteDataArray>(iMVS_VARIABLE.stVarValue.pByteValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				if (array[0].nDataLen != 0)
				{
					byte[] array2 = new byte[array[0].nDataLen];
					Marshal.Copy(array[0].pData, array2, 0, array2.Length);
					int num = Marshal.SizeOf(typeof(MatchPoint));
					int num2 = array2.Length / num;
					IntPtr intPtr = Marshal.AllocHGlobal(num);
					int num3 = 0;
					for (int i = 0; i < array2.Length; i += num)
					{
						Marshal.Copy(array2, i, intPtr, num);
						matchOutline.MatchOutlinePoints.Add((MatchPoint)Marshal.PtrToStructure(intPtr, typeof(MatchPoint)));
						num3++;
					}
					Marshal.FreeHGlobal(intPtr);
				}
			}
		}
		return matchOutline;
	}

	public List<Fixture> GetVarFixtureArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<Fixture> list = new List<Fixture>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "FIXTURE");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT && array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
					{
						float[] array2 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
						if (num > 0 && num != array2.Length)
						{
							VmException ex2 = new VmException(-536870888);
							throw ex2;
						}
						num = array2.Length;
						list2.Add(array2);
					}
				}
				if (list2.Count < 10)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new Fixture
					{
						stInitPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][j],
							Y = list2[1][j]
						},
						fInitAngle = list2[2][j],
						fInitScaleX = list2[3][j],
						fInitScaleY = list2[4][j],
						stRunPoint = new VM.PlatformSDKCS.PointF
						{
							X = list2[5][j],
							Y = list2[6][j]
						},
						fRunAngle = list2[7][j],
						fRunScaleX = list2[8][j],
						fRunScaleY = list2[9][j]
					});
				}
			}
		}
		return list;
	}

	public List<Polygon> GetVarPolygonArray(string varName)
	{
		if (string.IsNullOrEmpty(varName))
		{
			VmException ex = new VmException(-536870911);
			throw ex;
		}
		varName = RepairVarName(varName);
		List<Polygon> list = new List<Polygon>();
		byte[] binaryData = GetBinaryData("VarValue-null-" + varName);
		if (binaryData != null && binaryData.Length % Marshal.SizeOf(typeof(IMVS_VARIABLE)) == 0)
		{
			IMVS_VARIABLE iMVS_VARIABLE = ExternalCallHelper.BytesToStruct<IMVS_VARIABLE>(binaryData, 0, binaryData.Length);
			CheckVarType(iMVS_VARIABLE.chszValueType, "ROIPOLYGON");
			if (iMVS_VARIABLE.stVarValue.nValueNum != 0)
			{
				IMVS_SUB_VALUE[] array = ExternalCallHelper.IntPtr2Structures<IMVS_SUB_VALUE>(iMVS_VARIABLE.stVarValue.pIoValue, (int)iMVS_VARIABLE.stVarValue.nValueNum);
				int[] array2 = new int[0];
				List<float[]> list2 = new List<float[]>();
				int num = 0;
				for (int i = 0; i < iMVS_VARIABLE.stVarValue.nValueNum; i++)
				{
					string text = StringUtil.UTF8GetString(array[i].chszValueName);
					if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT)
					{
						if (array[i].nValueNum != 0 && array[i].pFloatValue != IntPtr.Zero)
						{
							float[] array3 = ExternalCallHelper.IntPtr2Ts<float>(array[i].pFloatValue, (int)array[i].nValueNum);
							if (num > 0 && num != array3.Length)
							{
								VmException ex2 = new VmException(-536870888);
								throw ex2;
							}
							num = array3.Length;
							list2.Add(array3);
						}
					}
					else if (array[i].emValueType == IO_VALUE_TYPE.IO_VALUE_TYPE_INT && array[i].nValueNum != 0 && array[i].pIntValue != IntPtr.Zero)
					{
						array2 = ExternalCallHelper.IntPtr2Ts<int>(array[i].pIntValue, (int)array[i].nValueNum);
					}
				}
				if (list2.Count < 2 || array2 == null)
				{
					VmException ex3 = new VmException(-536870888);
					throw ex3;
				}
				int num2 = 0;
				for (int j = 0; j < array2.Length; j++)
				{
					Polygon polygon = new Polygon();
					polygon.VertexPoints = new VM.PlatformSDKCS.PointF[array2[j]];
					for (int k = 0; k < array2[j]; k++)
					{
						polygon.VertexPoints[k] = new VM.PlatformSDKCS.PointF
						{
							X = list2[0][k],
							Y = list2[1][k]
						};
					}
					num2 = array2[j];
					list.Add(polygon);
				}
			}
		}
		return list;
	}

	private static void ReleaseIntptr(IntPtr ptr)
	{
		if (ptr != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(ptr);
			ptr = IntPtr.Zero;
		}
	}

	public static int AssemblySubIOValue<T>(List<T[]> tArray, Dictionary<string, IO_VALUE_TYPE> strNameType, Func<IntPtr, int, int> func = null)
	{
		int result = 0;
		if (tArray == null || tArray.Count <= 0)
		{
			return -536870911;
		}
		if (tArray.Count != strNameType.Count)
		{
			return -536870911;
		}
		List<IntPtr> list = new List<IntPtr>();
		IMVS_SUB_VALUE[] array = new IMVS_SUB_VALUE[tArray.Count];
		for (int i = 0; i < tArray.Count; i++)
		{
			if (tArray[i] is int[] && strNameType.ElementAt(i).Value == IO_VALUE_TYPE.IO_VALUE_TYPE_INT)
			{
				int[] array2 = tArray[i] as int[];
				array[i].chszValueName = StringUtil.UTF8GetFixLenBytes(strNameType.ElementAt(i).Key, 256);
				array[i].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_INT;
				array[i].nValueNum = (uint)array2.Length;
				array[i].pIntValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(int)) * array[i].nValueNum));
				Marshal.Copy(array2, 0, array[i].pIntValue, array2.Length);
			}
			else if (tArray[i] is float[] && strNameType.ElementAt(i).Value == IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT)
			{
				float[] array3 = tArray[i] as float[];
				array[i].chszValueName = StringUtil.UTF8GetFixLenBytes(strNameType.ElementAt(i).Key, 256);
				array[i].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_FLOAT;
				array[i].nValueNum = (uint)array3.Length;
				array[i].pFloatValue = Marshal.AllocHGlobal((int)(Marshal.SizeOf(typeof(float)) * array[i].nValueNum));
				Marshal.Copy(array3, 0, array[i].pFloatValue, array3.Length);
			}
			else if (tArray[i] is string[] && strNameType.ElementAt(i).Value == IO_VALUE_TYPE.IO_VALUE_TYPE_STRING)
			{
				string[] array4 = tArray[i] as string[];
				array[i].chszValueName = StringUtil.UTF8GetFixLenBytes(strNameType.ElementAt(i).Key, 256);
				array[i].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_STRING;
				array[i].nValueNum = (uint)array4.Length;
				ByteDataArray[] array5 = new ByteDataArray[array[i].nValueNum];
				for (int j = 0; j < array5.Length; j++)
				{
					byte[] array6 = StringUtil.UTF8GetBytesPadZero(array4[j]);
					array5[j].pData = Marshal.AllocHGlobal(array6.Length);
					array5[j].nDataLen = (uint)array6.Length;
					Marshal.Copy(array6, 0, array5[j].pData, array6.Length);
					list.Add(array5[j].pData);
				}
				List<byte> list2 = new List<byte>();
				ByteDataArray[] array7 = array5;
				foreach (ByteDataArray byteDataArray in array7)
				{
					list2.AddRange(DataTransf.Structure2Bytes(byteDataArray));
				}
				IntPtr intPtr = Marshal.AllocHGlobal(array5.Length * Marshal.SizeOf(typeof(ByteDataArray)));
				Marshal.Copy(list2.ToArray(), 0, intPtr, list2.Count);
				array[i].pByteValue = intPtr;
			}
			else if (tArray[i] is byte[])
			{
				byte[] array8 = tArray[i] as byte[];
				array[i].chszValueName = StringUtil.UTF8GetFixLenBytes(strNameType.ElementAt(i).Key, 256);
				array[i].emValueType = IO_VALUE_TYPE.IO_VALUE_TYPE_BYTE;
				array[i].nValueNum = 1u;
				ByteDataArray[] array9 = new ByteDataArray[array[i].nValueNum];
				for (int l = 0; l < array9.Length; l++)
				{
					array9[l].pData = Marshal.AllocHGlobal(array8.Length);
					array9[l].nDataLen = (uint)array8.Length;
					Marshal.Copy(array8, 0, array9[l].pData, array8.Length);
					list.Add(array9[l].pData);
				}
				List<byte> list3 = new List<byte>();
				ByteDataArray[] array10 = array9;
				foreach (ByteDataArray byteDataArray2 in array10)
				{
					list3.AddRange(DataTransf.Structure2Bytes(byteDataArray2));
				}
				IntPtr intPtr2 = Marshal.AllocHGlobal(array9.Length * Marshal.SizeOf(typeof(ByteDataArray)));
				Marshal.Copy(list3.ToArray(), 0, intPtr2, list3.Count);
				array[i].pByteValue = intPtr2;
			}
			else if (tArray[i] is IntPtrData[])
			{
				IntPtrData[] array11 = tArray[i] as IntPtrData[];
				array[i].chszValueName = StringUtil.UTF8GetFixLenBytes(strNameType.ElementAt(i).Key, 256);
				array[i].emValueType = strNameType.ElementAt(i).Value;
				array[i].nValueNum = (uint)array11.Length;
				ByteDataArray[] array12 = new ByteDataArray[array[i].nValueNum];
				for (int n = 0; n < array12.Length; n++)
				{
					array12[n].pData = array11[n].ptrData;
					array12[n].nDataLen = array11[n].nDataLen;
				}
				List<byte> list4 = new List<byte>();
				ByteDataArray[] array13 = array12;
				foreach (ByteDataArray byteDataArray3 in array13)
				{
					list4.AddRange(DataTransf.Structure2Bytes(byteDataArray3));
				}
				IntPtr intPtr3 = Marshal.AllocHGlobal(array12.Length * Marshal.SizeOf(typeof(ByteDataArray)));
				Marshal.Copy(list4.ToArray(), 0, intPtr3, list4.Count);
				array[i].pByteValue = intPtr3;
			}
		}
		List<byte> list5 = new List<byte>();
		IMVS_SUB_VALUE[] array14 = array;
		foreach (IMVS_SUB_VALUE iMVS_SUB_VALUE in array14)
		{
			list5.AddRange(DataTransf.Structure2Bytes(iMVS_SUB_VALUE));
		}
		IntPtr intPtr4 = Marshal.AllocHGlobal(array.Length * Marshal.SizeOf(typeof(IMVS_SUB_VALUE)));
		Marshal.Copy(list5.ToArray(), 0, intPtr4, list5.Count);
		try
		{
			if (func != null)
			{
				result = func(intPtr4, array.Length);
			}
		}
		catch (VmException ex)
		{
			throw ex;
		}
		finally
		{
			foreach (IntPtr item in list)
			{
				ReleaseIntptr(item);
			}
			for (int num3 = 0; num3 < array.Length; num3++)
			{
				ReleaseIntptr(array[num3].pIntValue);
				ReleaseIntptr(array[num3].pFloatValue);
				ReleaseIntptr(array[num3].pByteValue);
			}
			ReleaseIntptr(intPtr4);
		}
		return result;
	}

	private byte[] GetVarByteValueAlloc(string varIoName, string varSubIoName)
	{
		string text = "VarIOValue-null";
		text = ((!string.IsNullOrEmpty(varSubIoName)) ? (text + "\v" + varIoName + "\v" + varSubIoName) : (text + "\v" + varIoName));
		return GetBinaryData(text);
	}

	private int[] GetVarIntValueAlloc(string varIoName, string varSubIoName = null)
	{
		int[] array = new int[0];
		byte[] varByteValueAlloc = GetVarByteValueAlloc(varIoName, varSubIoName);
		if (varByteValueAlloc != null && varByteValueAlloc.Length != 0)
		{
			int num = varByteValueAlloc.Length / Marshal.SizeOf(typeof(int));
			array = new int[num];
			Buffer.BlockCopy(varByteValueAlloc, 0, array, 0, varByteValueAlloc.Length);
		}
		return array;
	}

	private float[] GetVarFloatValueAlloc(string varIoName, string varSubIoName = null)
	{
		float[] array = new float[0];
		byte[] varByteValueAlloc = GetVarByteValueAlloc(varIoName, varSubIoName);
		if (varByteValueAlloc != null && varByteValueAlloc.Length != 0)
		{
			int num = varByteValueAlloc.Length / Marshal.SizeOf(typeof(float));
			array = new float[num];
			Buffer.BlockCopy(varByteValueAlloc, 0, array, 0, varByteValueAlloc.Length);
		}
		return array;
	}

	private string[] GetVarStringValueAlloc(string varIoName, string varSubIoName = null)
	{
		string[] result = new string[0];
		byte[] varByteValueAlloc = GetVarByteValueAlloc(varIoName, varSubIoName);
		if (varByteValueAlloc != null && varByteValueAlloc.Length != 0)
		{
			string text = Encoding.UTF8.GetString(varByteValueAlloc).TrimEnd(default(char));
			result = text.Split(default(char));
		}
		return result;
	}

	private void FixName(ref string name)
	{
		if (name.Length > 32)
		{
			name = name.Substring(0, 32);
		}
	}

	private void DeleteIOLink(string varName, bool isFromUI = true, VarRunParamInfo[] runParaminfo = null)
	{
		VmIO vmIO = DataSource.Outputs[varName];
		int index = vmIO.Index;
		string typeName = vmIO.TypeName;
		if (vmIO == null)
		{
			return;
		}
		try
		{
			UnBindGlobarVarItem(varName, isFromUI);
		}
		catch (Exception ex)
		{
			if (!(ex is VmException ex2))
			{
				throw ex;
			}
			LogHelper.Error("UnBindingGlobalVariable is error:" + ex2.errorMessage);
		}
		LogHelper.Info($"DeleteIOLink varName:{varName} varType:{typeName},index:{index}");
		ChangeToSendReceiveData(index);
		SetRunParamBind(varName, typeName, runParaminfo, null);
	}

	private void DeleteIO(string varName, bool isFromUI = true, VarRunParamInfo[] runParaminfo = null, bool isChangeIndex = true)
	{
		LogHelper.Info("DeleteIO varName:" + varName);
		VmIO vmIO = DataSource.Outputs[varName];
		int index = vmIO.Index;
		string typeName = vmIO.TypeName;
		if (isChangeIndex)
		{
			foreach (VmIO output in DataSource.Outputs)
			{
				if (output.CustomVisibleA && output.Index > index)
				{
					output.Index--;
				}
			}
		}
		if (vmIO == null)
		{
			return;
		}
		try
		{
			UnBindGlobarVarItem(varName, isFromUI);
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				LogHelper.Error("UnBindingGlobalVariable is error:" + ex2.errorMessage);
			}
			else
			{
				LogHelper.Error("UnBindingGlobalVariable is error:" + ex.ToString());
			}
		}
		try
		{
			vmIO.RemoveSelf();
		}
		catch (Exception ex3)
		{
			LogHelper.Error("DeleteIO RemoveSelf is exception:" + ex3.ToString());
		}
		LogHelper.Info($"DeleteIO varName:{varName} varType:{typeName},index:{index}");
		ChangeToSendReceiveData(index);
		SetRunParamBind(varName, typeName, runParaminfo, null);
	}

	public bool IsCombinationIO(string typeName)
	{
		object ioType = GetIoType(typeName);
		if (ioType != null && (typeName == "pointset" || typeName == "byte"))
		{
			return true;
		}
		return ioType != null && (ioType is CombinationIOTypeEnum || ioType is CombinationIOTypeEnumEx);
	}

	private object GetIoType(string typeName)
	{
		object obj = null;
		try
		{
			if (Enum.TryParse<LeafIOTypeEnum>(typeName.ToUpper(), out var result))
			{
				obj = result;
			}
			else
			{
				obj = ((!Enum.TryParse<LeafIOTypeEnum>(typeName, out result)) ? null : ((object)result));
			}
		}
		catch
		{
			obj = null;
		}
		if (obj == null)
		{
			obj = Enum.Parse(typeof(CombinationIOTypeEnum), typeName);
		}
		if (obj == null && CheckAdditionExist.IsExistAddition)
		{
			obj = Enum.Parse(typeof(CombinationIOTypeEnumEx), typeName);
		}
		return obj;
	}

	private VmIO AddIoByType(object ioType, string ioName, IList TargetVMIOs, List<string> leafIoNames = null)
	{
		if (ioType is LeafIOTypeEnum)
		{
			return AddLeafIO(ioType.ToString(), DataSource, TargetVMIOs, ioName);
		}
		if (ioType is CombinationIOTypeEnum || ioType is CombinationIOTypeEnumEx)
		{
			return AddCombinationIO(ioType.ToString(), DataSource, TargetVMIOs, ioName, leafIoNames);
		}
		return null;
	}

	private VmIO AddLeafIO(string typeName, object parent, IList parentList, string ioName)
	{
		FixName(ref ioName);
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = ioName,
			Root = DataSource,
			Parent = parent,
			TypeName = typeName.ToLower()
		};
		parentList.Add(dynamicVmIO);
		return dynamicVmIO;
	}

	private VmIO AddCombinationIO(string typeName, object parent, IList parentList, string ioName, List<string> leafIoNames = null)
	{
		CombinRoot instance = CombinRoot.GetInstance();
		Combination obj = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == typeName);
		return AddIOFromCombination(obj, parent, parentList, ioName, leafIoNames);
	}

	private VmIO AddIOFromCombination(object obj, object parent, IList parentList, string ioName = null, List<string> leafIoNames = null)
	{
		if (obj is Combination combination)
		{
			string name = ((ioName == null) ? combination.Name : ioName);
			FixName(ref name);
			DynamicVmIO dynamicVmIO = new DynamicVmIO
			{
				Name = GetUniqueName(name),
				Root = DataSource,
				Parent = parent,
				TypeName = combination.Style
			};
			parentList.Add(dynamicVmIO);
			foreach (object filter2 in combination.Filters)
			{
				AddIOFromCombination(filter2, dynamicVmIO, dynamicVmIO.Childs, null, leafIoNames);
			}
			return dynamicVmIO;
		}
		if (obj is Filter filter)
		{
			string strFindIOName = filter.NameA;
			if (leafIoNames != null)
			{
				string text = leafIoNames.FirstOrDefault((string x) => x.Contains(strFindIOName));
				if (text != null)
				{
					strFindIOName = text;
					leafIoNames.Remove(text);
				}
				else
				{
					strFindIOName = GetUniqueName(strFindIOName);
				}
			}
			else
			{
				strFindIOName = GetUniqueName(strFindIOName);
			}
			AddLeafIO(filter.ValueTypeA, parent, parentList, strFindIOName);
		}
		return null;
	}

	private string GetUniqueName(string startName)
	{
		IEnumerable<string> source = from p in DataSource.Inputs.GetAllIO().Concat(DataSource.Outputs.GetAllIO())
			select p.Name;
		string text = startName;
		if (!source.Contains("%" + text + "%"))
		{
			return text;
		}
		int num = 0;
		while (true)
		{
			text = startName + num;
			if (!source.Contains("%" + text + "%"))
			{
				break;
			}
			num++;
		}
		return text;
	}
}
