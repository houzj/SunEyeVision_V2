using System;
using System.Text;
using Apps.Localization;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ProtocolInputParam : BridgeItemBase, IBottomCommand
{
	public void ContinueRunExcute()
	{
		throw new NotImplementedException();
	}

	public object ExecuteFunction(object obj = null)
	{
		bool bRret = false;
		SetIOParams(out bRret);
		return bRret;
	}

	public void OkExcute()
	{
		throw new NotImplementedException();
	}

	public void ResetExcute()
	{
		throw new NotImplementedException();
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
		DataAssembleModuleDynamicIO dataAssembleModuleDynamicIO = BridgeObjA as DataAssembleModuleDynamicIO;
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			if (dataAssembleModuleDynamicIO == null)
			{
				return;
			}
			foreach (DynamicIO item in dataAssembleModuleDynamicIO.ItemsA)
			{
				stringBuilder.Append(item.NameA);
				stringBuilder.Append("\r");
			}
			if (string.IsNullOrEmpty(stringBuilder.ToString()))
			{
				bRret = true;
				return;
			}
			int num = 0;
			try
			{
				vmModule.Params["SubscribeField"] = stringBuilder.ToString();
			}
			catch (Exception)
			{
				num = -1;
			}
			bRret = num == 0;
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show("Params Settings", ex2.Message, MessageIcons.Error);
			bRret = false;
		}
	}
}
