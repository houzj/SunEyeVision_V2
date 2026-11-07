using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Apps.Json;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VM.Utility;
using VMControls.Interface;

namespace CalculatorModuleControl;

public static class Helper
{
	private const string VariableTemplate = "<{0}:<base64 {1} />>";

	private const string POINT = "POINT";

	private static readonly string[] RegexEscapecharacters = new string[13]
	{
		"^", "$", ".", "(", ")", "*", "?", "+", "[", "]",
		"{", "}", "|"
	};

	public static string VariableToModuleString(string input, VmModule moduleTag, string[] filterParams, Dictionary<string, VmIO> filterstore)
	{
		if (moduleTag == null)
		{
			return input;
		}
		VmProcedure root = moduleTag.Root;
		if (root == null)
		{
			return input;
		}
		IEnumerable<VmModule> enumerable = moduleTag.GetFrontParamItems().OfType<VmModule>();
		List<VmIO> list = new List<VmIO>();
		if (filterParams == null)
		{
			filterParams = new string[3] { "string", "int", "float" };
		}
		bool flag = filterParams.Contains("POINT");
		if (enumerable != null)
		{
			foreach (VmModule item in enumerable)
			{
				List<VmIO> list2 = new List<VmIO>();
				if (item.Outputs == null || item.Outputs.Count == 0)
				{
					continue;
				}
				list.AddRange(item.Outputs.GetAllLeafIO());
				if (flag)
				{
					list.AddRange(from a in item.Outputs.GetAllTrunkIO()
						where a.TypeName == "POINT"
						select a);
				}
			}
		}
		if (moduleTag.Parent is IVMGroup && moduleTag.Parent is VmModule)
		{
			VmModule vmModule = moduleTag.Parent as VmModule;
			if (vmModule.Inputs != null && vmModule.Inputs.Count > 0)
			{
				list.AddRange(vmModule.Inputs.GetAllLeafIO());
				if (flag)
				{
					list.AddRange(from a in vmModule.Inputs.GetAllTrunkIO()
						where a.TypeName == "POINT"
						select a);
				}
			}
		}
		else if (moduleTag.Parent is IVmProcedure && moduleTag.Parent is VmModule { Inputs: not null } vmModule2 && vmModule2.Inputs.Count > 0)
		{
			list.AddRange(vmModule2.Inputs.GetAllLeafIO());
			if (flag)
			{
				list.AddRange(from a in vmModule2.Inputs.GetAllTrunkIO()
					where a.TypeName == "POINT"
					select a);
			}
		}
		VmModule vmModule3 = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 13000);
		if (vmModule3 != null)
		{
			VmIOCollection outputs = vmModule3.Outputs;
			if (outputs != null)
			{
				list.AddRange(outputs.GetAllLeafIO());
				if (flag)
				{
					list.AddRange(from a in outputs.GetAllTrunkIO()
						where a.TypeName == "POINT"
						select a);
				}
			}
		}
		IVmModule localVarModule = moduleTag.GetLocalVarModule();
		if (localVarModule != null)
		{
			VmIOCollection outputs2 = (localVarModule as VmModule).Outputs;
			if (outputs2 != null)
			{
				list.AddRange(outputs2.GetAllLeafIO());
				if (flag)
				{
					list.AddRange(from a in outputs2.GetAllTrunkIO()
						where a.TypeName == "POINT"
						select a);
				}
			}
		}
		int index = ((filterstore != null) ? filterstore.Count : 0);
		foreach (VmIO filter in list)
		{
			if (filter == null)
			{
				continue;
			}
			string pattern = $"<\\s*{GetEscapeString(filter.ModulePathName)}\\s*>";
			input = Regex.Replace(input, pattern, (Match m) =>
			{
				string text = ("CalculatorInput" + index).String2Base64();
				if (filterstore != null && !filterstore.Keys.Contains(text))
				{
					filterstore.Add(text, filter);
					int num = index;
					index = num + 1;
				}
				return $"<{filter.TypeName}:<base64 {text} />>";
			});
		}
		return input;
	}

	public static string GetEscapeString(string input)
	{
		string[] regexEscapecharacters = RegexEscapecharacters;
		foreach (string text in regexEscapecharacters)
		{
			if (input.Contains(text))
			{
				input = Regex.Replace(input, "\\" + text, (Match m) => m.Value.Replace(m.Value, $"\\{m.Value}"));
			}
		}
		return input;
	}

	public static T DeSerializationFromJson<T>(this string jsonstring) where T : class, new()
	{
		T result = null;
		try
		{
			result = JsonConvert.DeserializeObject(jsonstring, typeof(T)) as T;
			return result;
		}
		catch (Exception)
		{
		}
		return result;
	}

	public static string SerializationToJson<T>(this T source) where T : class
	{
		return JsonConvert.SerializeObject((object)source);
	}

	public static string ToJson(this object source)
	{
		return JsonConvert.SerializeObject(source);
	}
}
