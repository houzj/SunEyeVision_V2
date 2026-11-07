using System;
using System.Collections.ObjectModel;
using Hik.Util;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class GetGlobalModuleListOperation : IOperation
{
	public static string strChoice = "Lang_PlzChoose";

	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetGlobalModuleListOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			string type = "";
			if (ExtraParams.Length != 0)
			{
				type = ExtraParams[0];
			}
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", GetModuleList(type));
			parentNode.SetTriggerDisable("CurValueA");
			parentNode.OnPropertyChanged("CurValueA");
			parentNode.SetTriggerEnable();
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}

	private ObservableCollection<object> GetModuleList(string type)
	{
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		EnumEntry item = new EnumEntry
		{
			DisplayName = strChoice,
			Value = -1
		};
		observableCollection.Add(item);
		ImvsSdkDefine.IMVS_MODULE_INFO_LIST? allModuleList = ServerSDKManager.serverSDKManager.mSolutionManager.GetAllModuleList();
		if (allModuleList.HasValue)
		{
			for (int i = 0; i < allModuleList.Value.nNum; i++)
			{
				ImvsSdkDefine.IMVS_MODULE_INFO iMVS_MODULE_INFO = allModuleList.Value.astModuleInfo[i];
				if (type == iMVS_MODULE_INFO.nModuleType.ToString())
				{
					EnumEntry enumEntry = new EnumEntry();
					enumEntry.DisplayNameA = iMVS_MODULE_INFO.nNodeID + " " + Hik.Util.StringUtil.UTF8GetString(iMVS_MODULE_INFO.strDisplayName);
					enumEntry.DescriptionA = Hik.Util.StringUtil.UTF8GetString(iMVS_MODULE_INFO.strDisplayName);
					enumEntry.ValueA = (int)iMVS_MODULE_INFO.nNodeID;
					observableCollection.Add(enumEntry);
				}
			}
		}
		return observableCollection;
	}
}
