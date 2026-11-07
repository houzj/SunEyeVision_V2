using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apps.Localization;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ProtocolOutputParam : BridgeItemBase, IBottomCommand
{
	private Dictionary<string, string> typeDict = new Dictionary<string, string>
	{
		{ "0", "int" },
		{ "1", "float" },
		{ "2", "string" }
	};

	private Dictionary<string, string> paramsDict = new Dictionary<string, string>();

	public object ExecuteFunction(object obj = null)
	{
		if (obj != null && obj is bool flag)
		{
			bool isOK = false;
			if (flag)
			{
				RestOutputParams(out isOK);
			}
			return isOK;
		}
		return null;
	}

	public void RestOutputParams(out bool isOK)
	{
		isOK = true;
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("获取") + AppLocalizationService.GetLang("ModuleInfo") + AppLocalizationService.GetLang("失败"), MessageIcons.Error);
			isOK = false;
			return;
		}
		string text = "";
		int num = 0;
		try
		{
			text = vmModule.Params["OutputField"];
		}
		catch
		{
			num = -1;
		}
		if (num != 0 || string.IsNullOrEmpty(text))
		{
			HikMessageBox.Show("获取输出参数错误，请检查脚本文件", MessageIcons.Error);
			isOK = false;
		}
		else
		{
			if (!(BridgeObjA is DataAnalysisModuleDynamicIO { ItemsA: var itemsA } dataAnalysisModuleDynamicIO))
			{
				return;
			}
			IEnumerable<object> source = itemsA.Where((object p) => (p as DynamicIO).CustomVisibleA);
			string[] array = source.Select((object x) => (x as DynamicIO).NameA).ToArray();
			paramsDict.Clear();
			while (text.Contains("\r"))
			{
				string text2 = "";
				string text3 = "";
				int num2 = text.IndexOf("\r");
				int num3 = text.IndexOf("\n");
				text2 = text.Substring(0, num3);
				text3 = text.Substring(num3 + 1, num2 - num3 - 1);
				text = text.Substring(num2 + 1);
				if (!string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text3))
				{
					if (paramsDict.ContainsKey(text2))
					{
						HikMessageBox.Show("输出参数存在同名，请检查脚本文件", MessageIcons.Error);
						isOK = false;
						break;
					}
					paramsDict.Add(text2, text3);
				}
			}
			string[] array2 = array;
			foreach (string delete in array2)
			{
				object obj2 = source.FirstOrDefault((object p) => (p as DynamicIO).NameA == delete);
				if (obj2 != null)
				{
					dataAnalysisModuleDynamicIO.RemoveIO(obj2);
				}
			}
			array = source.Select((object x) => (x as DynamicIO).NameA).ToArray();
			foreach (KeyValuePair<string, string> item in paramsDict)
			{
				if (!array.Contains(item.Key))
				{
					dataAnalysisModuleDynamicIO.AddIO(item.Key, item.Value);
				}
			}
			isOK = true;
		}
	}

	public void SelfRunExcute()
	{
		throw new NotImplementedException();
	}

	public void SetIOParams(out bool bRret)
	{
		bRret = true;
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("获取") + AppLocalizationService.GetLang("ModuleInfo") + AppLocalizationService.GetLang("失败"), MessageIcons.Error);
			bRret = false;
			return;
		}
		VmIOCollection outputs = vmModule.Outputs;
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			if (outputs == null)
			{
				return;
			}
			foreach (VmIO item in outputs)
			{
				if (item.CustomVisibleA)
				{
					stringBuilder.Append(item.Name);
					stringBuilder.Append("\n");
					stringBuilder.Append(item.TypeName);
					stringBuilder.Append("\r");
				}
			}
			if (string.IsNullOrEmpty(stringBuilder.ToString()))
			{
				bRret = true;
				return;
			}
			int num = 0;
			try
			{
				vmModule.Params["OutputField"] = stringBuilder.ToString();
			}
			catch
			{
				num = -1;
			}
			bRret = num == 0;
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Params Settings", ex.Message, MessageIcons.Error);
			bRret = false;
		}
	}
}
