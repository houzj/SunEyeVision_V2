using System;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class SetModuleParamBindOperation : IOperation
{
	private const int filterStrLen = 64;

	private bool IsShellModule = false;

	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetModuleParamBindOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null || !(Params[0] is ButtonSelecter buttonSelecter))
		{
			return;
		}
		string operationParamsA = buttonSelecter.DropOpenSetterA.OperationParamsA;
		string text = Params[1] as string;
		if (!(((buttonSelecter.GetTopBaseNode() is IOwner owner) ? owner.Owner : null) is VmModule vmModule) || ExtraParams.Length == 0)
		{
			return;
		}
		string paramName = ExtraParams[0];
		bool flag = true;
		bool flag2 = true;
		if (string.IsNullOrEmpty(text))
		{
			flag2 = false;
			flag = vmModule.ParamSubManager.DelModuleParamBinding(paramName, operationParamsA);
			vmModule.SetLinkWithVarNull(buttonSelecter.OldCurValue, (int)vmModule.ID, paramName, operationParamsA);
		}
		else
		{
			if (text.Length > 64)
			{
				string[] strArrMsg = new string[3]
				{
					AppLocalizationService.GetLang("Lang_ContentTooLarge"),
					AppLocalizationService.GetLang("长度上限"),
					64.ToString()
				};
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), strArrMsg, MessageIcons.Error);
				return;
			}
			if ((MathHelper.IsNumber(text) || MathHelper.IsConst(text)) && text != "TRIGGER_STRING" && text != "TRIGGER_STRING[]")
			{
				flag = vmModule.ParamSubManager.SetModuleParamBinding(paramName, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_CONSTANT, text);
			}
			else if (text.Equals(VmSolution.ExternIO.Name + "[]") || text == VmSolution.ExternIO.Name)
			{
				flag = vmModule.ParamSubManager.SetModuleParamBinding(paramName, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_GLOBAL, "", 0u, VmSolution.ExternIO.Name);
			}
			else
			{
				string text2 = "";
				int num = text.IndexOf('[', 0);
				if (num > 0)
				{
					text2 = text.Substring(num);
					text2 = text2.Replace("[", "").Replace("]", "");
					text = text.Remove(text.IndexOf('[', 0));
				}
				VmIO vmIO = vmModule.GetIoByPathFromAround(text);
				if (vmIO == null)
				{
					vmIO = VmSolution.Instance[text] as VmIO;
				}
				if (vmIO == null)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
					return;
				}
				if (text2.Contains('.'))
				{
					VmIO ioByPathFromAround = vmModule.GetIoByPathFromAround(text2);
					if (ioByPathFromAround == null)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
						return;
					}
					flag = vmModule.ParamSubManager.SetModuleParamBinding(paramName, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", vmIO.Root.ID, vmIO.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_VARIABLE, "", ioByPathFromAround.Root.ID, ioByPathFromAround.Name);
				}
				else if (MathHelper.IsNumber(text2))
				{
					flag = vmModule.ParamSubManager.SetModuleParamBinding(paramName, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", vmIO.Root.ID, vmIO.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_CONSTANT, text2);
				}
				else
				{
					vmModule.SetLinkWithVarNull(buttonSelecter.OldCurValue, (int)vmModule.ID, paramName, operationParamsA);
					flag = vmModule.ParamSubManager.SetModuleParamBinding(paramName, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", vmIO.Root.ID, vmIO.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL, "All");
					vmModule.SetLinkWithVar(paramName, operationParamsA, vmIO);
				}
			}
		}
		if (!flag & flag2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
			buttonSelecter.SetPropertyValueByNameNotrigger("CurValueA", null);
		}
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
					if (float.TryParse(val, out result3))
					{
						return true;
					}
					break;
				}
				case "double":
				{
					double result = 0.0;
					if (double.TryParse(val, out result))
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

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
