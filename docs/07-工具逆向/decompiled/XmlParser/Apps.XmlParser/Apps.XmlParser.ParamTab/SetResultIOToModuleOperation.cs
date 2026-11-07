using System;
using System.Collections;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class SetResultIOToModuleOperation : IOperation
{
	private const int filterStrLen = 256;

	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working { get; }

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}

	public SetResultIOToModuleOperation(string TName)
	{
		TargetName = TName;
	}

	private bool CheckValue(string val, string paramType)
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

	public static bool GetOutputInfo(VmModule node, string filterString, ref VmIO outputFilter, ref string BindingIndex, ref bool isBindGlobalVar, bool isShellModule = false)
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

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null || Params == null || Params.Length < 2)
		{
			return;
		}
		NameValueItem parentItem = Params[0] as NameValueItem;
		if (parentItem == null)
		{
			return;
		}
		int num = 0;
		ButtonSelecter buttonSelecter = parentItem as ButtonSelecter;
		if (buttonSelecter != null)
		{
			num = buttonSelecter.Type;
		}
		string text = Params[1] as string;
		if (parentItem == null)
		{
			return;
		}
		BaseNode baseNode = parentItem.GetTopBaseNode() as BaseNode;
		if (!((baseNode as IOwner)?.Owner is VmModule vmModule) || ExtraParams.Length <= 1)
		{
			return;
		}
		string text2 = ExtraParams[0];
		string typeName = ExtraParams[1];
		if (!string.IsNullOrEmpty(text2))
		{
			VmIO vmIO = vmModule.GetIO(text2);
			if (vmIO != null)
			{
				vmModule.Outputs.Remove(vmIO);
				vmIO.IsResultShow = true;
			}
			else
			{
				vmIO = new DynamicVmIO
				{
					Name = text2,
					Root = vmModule,
					Parent = vmModule,
					TypeName = typeName,
					IsResultShow = true,
					Visible = false,
					VisibleInResultTree = false,
					CustomVisibleA = false
				};
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				vmModule.Outputs.Add(vmIO);
			}
		}
		if (!string.IsNullOrEmpty(text2))
		{
			VmIO iO = vmModule.GetIO(text2);
			if (iO == null && baseNode is SubAlgorithmTabRoot subAlgorithmTabRoot)
			{
				iO = vmModule.GetIO(AppLocalizationService.UnDoTranslate(text2 + "_" + subAlgorithmTabRoot.HashCode));
			}
			VmIO outputFilter = null;
			string BindingIndex = null;
			bool isBindGlobalVar = false;
			bool isShellModule = false;
			if (!GetOutputInfo(vmModule, text, ref outputFilter, ref BindingIndex, ref isBindGlobalVar, isShellModule))
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
			if (iO != null)
			{
				IEnumerable enumerable = null;
				enumerable = ((!(outputFilter is VmConstantIO vmConstantIO2)) ? iO.Value : vmConstantIO2.Value);
				iO.RelateIO = outputFilter;
				iO.RelateIndex = GetBindIndex(vmModule, BindingIndex);
				iO.Value = enumerable;
			}
		}
		Object obj = ((VmSolution.Instance[((IVmFullPath)vmModule).FullPath + ".[Display]"] is ParamRoot paramRoot) ? (from a in paramRoot.GetDisplayList().OfType<Object>()
			where a.Name == "Show Text"
			select a).FirstOrDefault() : null);
		if (obj?.Features.Where((FeatureBase a) => a.Name == parentItem.Name).FirstOrDefault() is Feature feature)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				feature.SetMapping(text2);
			}
			else
			{
				feature.SetMapping("NULL");
			}
		}
		obj?.InitVmIOs();
	}
}
