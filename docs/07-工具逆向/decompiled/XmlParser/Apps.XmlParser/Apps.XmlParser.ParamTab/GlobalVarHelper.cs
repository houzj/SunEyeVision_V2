using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.Variable;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GlobalVarHelper
{
	private static string strChoice = AppLocalizationService.GetLang("Lang_PlzChoose");

	public static int ModuNodeID { get; set; }

	public static string ModuDisplayName { get; set; }

	public static string ModuName { get; set; }

	public static string CommunicationAddress
	{
		get
		{
			if (string.IsNullOrWhiteSpace(ModuName) || ModuNodeID == 0)
			{
				return "";
			}
			return ModuNodeID + "+" + ModuName;
		}
	}

	public static IList GetSlectGlobalVar(VmModule objModule)
	{
		IList list = new List<EnumEntry>();
		VmModule globalVarModule = VmModuleExtern.GetGlobalVarModule();
		bool flag = globalVarModule != null && Enumerable.Count(globalVarModule.Outputs) > 0;
		IVmModule localVarModule = objModule.GetLocalVarModule();
		bool flag2 = localVarModule != null && Enumerable.Count((localVarModule as VmModule).Outputs) > 0;
		if (flag | flag2)
		{
			EnumEntry enumEntry = new EnumEntry();
			enumEntry.DisplayName = AppLocalizationService.GetLang("Lang_VariableManager");
			enumEntry.Description = AppLocalizationService.GetLang("Lang_VariableManager");
			enumEntry.Value = 1;
			list.Add(enumEntry);
		}
		if (list.Count > 0)
		{
			EnumEntry value = new EnumEntry
			{
				DisplayName = AppLocalizationService.GetLang("Lang_PlzChoose"),
				Description = AppLocalizationService.GetLang("Lang_PlzChoose"),
				Value = 0
			};
			list.Insert(0, value);
		}
		return list;
	}

	public static void InitGlobalVarOutputIO(VmModule vmModule)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		try
		{
			VariableManager variableManager = new VariableManager(vmModule);
			VariableManager.IMVS_VARIABLE[] vARIABLEs = new VariableManager.IMVS_VARIABLE[0];
			variableManager.GetAllVar(ref vARIABLEs);
			List<string> list = new List<string>();
			if (vARIABLEs.Length != 0)
			{
				try
				{
					for (int i = 0; i < vARIABLEs.Length; i++)
					{
						VariableManager.IMVS_VARIABLE iMVS_VARIABLE = vARIABLEs[i];
						string text = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueName);
						string varType = StringUtil.UTF8GetString(iMVS_VARIABLE.chszValueType);
						if (vmModule.Outputs.GetIO(text) == null)
						{
							variableManager.CheckVarAddIO(text, varType, iMVS_VARIABLE.nIndex);
						}
						list.Add(text);
						dictionary.Add(text, iMVS_VARIABLE.nIndex);
					}
					List<string> list2 = vmModule.Outputs.Select((VmIO p) => p.Name).ToList();
					foreach (string item in list2)
					{
						if (!VmModule.fTimeStamp.Contains(item) && list.FindIndex((string x) => x == item) < 0)
						{
							vmModule.Outputs.GetIO(item)?.Dispose();
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception ex2)
		{
			LogHelper.Error("InitGlobalVarOutputIO is error,ex:" + ex2.Message);
		}
		if (vmModule.Outputs == null)
		{
			return;
		}
		int num = 0;
		foreach (VmIO output in vmModule.Outputs)
		{
			if (!VmModule.fTimeStamp.Contains(output.Name))
			{
				num++;
				output.Index = (dictionary.ContainsKey(output.Name) ? dictionary[output.Name] : num);
			}
		}
	}
}
