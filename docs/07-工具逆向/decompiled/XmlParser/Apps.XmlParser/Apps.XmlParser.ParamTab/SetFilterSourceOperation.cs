using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class SetFilterSourceOperation : IOperation
{
	private const int filterStrLen = 256;

	private bool IsShellModule = false;

	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetFilterSourceOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			if (ExtraParams == null)
			{
				return;
			}
			NameValueItem nameValueItem = Params[0] as NameValueItem;
			int num = 0;
			ButtonSelecter buttonSelecter = nameValueItem as ButtonSelecter;
			if (buttonSelecter != null)
			{
				num = buttonSelecter.Type;
			}
			if (nameValueItem == null)
			{
				return;
			}
			string filterString = Params[1] as string;
			if (nameValueItem == null)
			{
				return;
			}
			BaseNode baseNode = nameValueItem.GetTopBaseNode() as BaseNode;
			if (!((baseNode as IOwner)?.Owner is VmModule vmModule) || ExtraParams.Length == 0)
			{
				return;
			}
			string text = ExtraParams[0];
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			VmIO iO = vmModule.GetIO(text);
			if (iO == null && baseNode is SubAlgorithmTabRoot subAlgorithmTabRoot)
			{
				iO = vmModule.GetIO(AppLocalizationService.UnDoTranslate(text + "_" + subAlgorithmTabRoot.HashCode));
			}
			if (iO == null && vmModule is VmModuleSet vmModuleSet)
			{
				vmModule = vmModuleSet.ProcedureCommunicaitonModule as VmModule;
				iO = vmModule.GetIO(text);
			}
			if (iO == null)
			{
				string lang = AppLocalizationService.GetLang("Lang_IO_Message");
				HikMessageBox.Show(lang + ":" + AppLocalizationService.GetLang(text), MessageIcons.Warning);
				return;
			}
			VmIO outputFilter = null;
			string BindingIndex = null;
			bool isBindGlobalVar = false;
			if (!GetOutputInfo(vmModule, filterString, ref outputFilter, ref BindingIndex, ref isBindGlobalVar))
			{
				if (buttonSelecter != null)
				{
					buttonSelecter.CurValueA = buttonSelecter.OldCurValue;
				}
				return;
			}
			if (outputFilter != null)
			{
				if (outputFilter != VmSolution.ExternIO && outputFilter.Root == null)
				{
					outputFilter.Root = vmModule;
				}
				if (string.IsNullOrEmpty(outputFilter.TypeName))
				{
					outputFilter.TypeName = iO.TypeName;
				}
				if (outputFilter is VmConstantIO vmConstantIO && !CheckValue(vmConstantIO.Name, iO.TypeName))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
					return;
				}
			}
			iO.RelateIO = outputFilter;
			iO.RelateIndex = GetBindIndex(vmModule, BindingIndex);
			string[] array = text.Split('_');
			if (array.Length == 2)
			{
				string objName = array[1].Substring(0, array[1].Length - 1);
				((VmSolution.Instance[((IVmFullPath)vmModule).FullPath + ".[Display]"] is ParamRoot paramRoot) ? (from a in paramRoot.GetDisplayList().OfType<Object>()
					where objName == AppLocalizationService.GetLang(a.NameA)
					select a).FirstOrDefault() : null)?.OnValueChanged(new PropertyEventArgs
				{
					PropertyName = nameValueItem.NameA
				});
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message + ex.StackTrace);
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public static bool CheckValue(string val, string paramType)
	{
		try
		{
			if (string.IsNullOrEmpty(paramType))
			{
				return true;
			}
			string[] array = paramType.Split(';');
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				switch (array2[i])
				{
				case "string":
					return true;
				case "int":
				{
					int result2 = 0;
					if (int.TryParse(val, out result2))
					{
						return true;
					}
					break;
				}
				case "float":
				{
					float result3 = 0f;
					if (float.TryParse(val, out result3) && !float.IsNaN(result3))
					{
						return true;
					}
					break;
				}
				case "double":
				{
					double result = 0.0;
					if (double.TryParse(val, out result) && !double.IsNaN(result))
					{
						return true;
					}
					break;
				}
				}
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	public static VmIO GetBindIndex(VmModule node, string bindingIndex)
	{
		VmIO vmIO = null;
		if (!string.IsNullOrEmpty(bindingIndex))
		{
			if (bindingIndex.Contains('.'))
			{
				vmIO = node.GetIoByPathFromAround(bindingIndex);
			}
			else if (MathHelper.IsNumber(bindingIndex))
			{
				vmIO = new VmConstantIO
				{
					Name = bindingIndex
				};
			}
			if (vmIO == null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
			}
		}
		return vmIO;
	}

	public static bool GetOutputInfo(VmModule node, string filterString, ref VmIO outputFilter, ref string BindingIndex, ref bool isBindGlobalVar)
	{
		if (!string.IsNullOrEmpty(filterString))
		{
			if (filterString.Length > 256)
			{
				string[] strArrMsg = new string[3]
				{
					AppLocalizationService.GetLang("Lang_ContentTooLarge"),
					AppLocalizationService.GetLang("长度上限"),
					256.ToString()
				};
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), strArrMsg, MessageIcons.Error);
				return false;
			}
			if ((MathHelper.IsNumber(filterString) || MathHelper.IsConst(filterString)) && filterString != "TRIGGER_STRING" && filterString != "TRIGGER_STRING[]")
			{
				outputFilter = new VmConstantIO
				{
					Name = filterString
				};
			}
			else if (filterString.Equals(VmSolution.ExternIO.Name + "[]") || filterString == VmSolution.ExternIO.Name)
			{
				outputFilter = VmSolution.ExternIO;
			}
			else
			{
				int num = filterString.IndexOf('[', 0);
				if (num > 0)
				{
					BindingIndex = filterString.Substring(num);
					BindingIndex = BindingIndex.Replace("[", "").Replace("]", "");
					filterString = filterString.Remove(filterString.IndexOf('[', 0));
				}
				outputFilter = node.GetIoByPathFromAround(filterString);
				if (outputFilter == null)
				{
					outputFilter = VmSolution.Instance[filterString] as VmIO;
				}
				if (outputFilter == null)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
					return false;
				}
			}
		}
		return true;
	}

	public static void SetFilterSource(VmModule node, VmIO inputFilter, string filterString, bool isShellModule = false)
	{
		VmIO outputFilter = null;
		string BindingIndex = null;
		bool isBindGlobalVar = false;
		if (GetOutputInfo(node, filterString, ref outputFilter, ref BindingIndex, ref isBindGlobalVar))
		{
			inputFilter.RelateIO = outputFilter;
		}
	}

	private static void ClearDicByFilter(Dictionary<Filter, IndexFilter> dic, Filter filter)
	{
		if (dic.Keys.Contains(filter))
		{
			dic[filter] = null;
		}
	}

	private static void SetDicByFilter(Dictionary<Filter, IndexFilter> dic, Filter filter, Filter outputFilter, string bindingIndex)
	{
		if (dic.Keys.Contains(filter))
		{
			filter.RelativeIndexStringName = bindingIndex;
			dic[filter] = new IndexFilter(bindingIndex, outputFilter);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
