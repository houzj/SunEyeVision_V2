using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class SetFeatureRelatedIOOperation : IOperation
{
	private const int filterStrLen = 64;

	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working { get; }

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}

	public SetFeatureRelatedIOOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null || Params == null || Params.Length < 2 || !(Params[0] is NameValueItem nameValueItem))
		{
			return;
		}
		int num = 0;
		ButtonSelecter buttonSelecter = nameValueItem as ButtonSelecter;
		if (buttonSelecter != null)
		{
			num = buttonSelecter.Type;
		}
		string filterString = Params[1] as string;
		BaseNode baseNode = nameValueItem.GetTopBaseNode() as BaseNode;
		if (!((baseNode as IOwner)?.Owner is VmModule vmModule) || ExtraParams.Length <= 1)
		{
			return;
		}
		string text = ExtraParams[0];
		VmIO vmIO = null;
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		Object parentNode = nameValueItem.GetParentNode<Object>();
		Feature feature = parentNode?.GetFeatureByName(text);
		if (feature == null)
		{
			return;
		}
		vmIO = feature.vmIOs.OfType<VmIO>()?.FirstOrDefault((VmIO p) => ExtraParams.Contains(p.TypeName));
		if (vmIO == null)
		{
			vmIO = new DynamicVmIO
			{
				Name = GetUniqueName(vmModule, text + "MappingIO"),
				Root = vmModule,
				Parent = vmModule,
				TypeName = ExtraParams[1],
				Visible = false,
				VisibleInResultTree = false,
				CustomVisibleA = false
			};
			vmModule.Outputs.Add(vmIO);
			feature.SetMapping(vmIO.Name);
			parentNode?.InitVmIOs();
			if (vmModule.StrModuleName.Equals("ShellModule"))
			{
				try
				{
					((IUserStringData)vmModule.Params)["AddPositionIO"] = vmIO.Name;
				}
				catch (VmException ex)
				{
					string text2 = "AddPositionIO error, " + AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
					text2 = text2 + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
					LogHelper.moduleLog.Error(text2);
				}
			}
		}
		VmIO outputFilter = null;
		string BindingIndex = null;
		bool isBindGlobalVar = false;
		if (!SetFilterSourceOperation.GetOutputInfo(vmModule, filterString, ref outputFilter, ref BindingIndex, ref isBindGlobalVar))
		{
			buttonSelecter?.SetPropertyValueByNameNotrigger("CurValueA", buttonSelecter.OldCurValue);
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
				outputFilter.TypeName = vmIO.TypeName;
			}
			if (outputFilter is VmConstantIO vmConstantIO && !SetFilterSourceOperation.CheckValue(vmConstantIO.Name, vmIO.TypeName))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
				return;
			}
			if (vmIO.TypeName != outputFilter.TypeName)
			{
				vmIO.TypeName = outputFilter.TypeName;
			}
		}
		SetIoParams(vmModule);
		IEnumerable enumerable = null;
		enumerable = ((!(outputFilter is VmConstantIO vmConstantIO2)) ? vmIO.Value : vmConstantIO2.Value);
		vmIO.RelateIO = outputFilter;
		vmIO.RelateIndex = SetFilterSourceOperation.GetBindIndex(vmModule, BindingIndex);
		vmIO.Value = enumerable;
		parentNode?.ClearValueStore();
		parentNode?.UpdateValue(new PropertyEventArgs
		{
			PropertyName = nameValueItem.NameA
		});
	}

	private void SetIoParams(VmModule module)
	{
		try
		{
			(module as IVMGroup)?.SetIoParams();
			(module as IVmProcedure)?.SetIoParams();
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
		}
	}

	private string GetUniqueName(VmModule module, string startName)
	{
		IEnumerable<string> source = from p in module.Inputs.GetAllIO().Concat(module.Outputs.GetAllIO())
			select p.Name;
		string text = startName;
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
