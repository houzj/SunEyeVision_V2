using System;
using System.Collections.Generic;
using System.Windows.Input;
using Mvvm.Commands;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

/// <summary>
/// 数据集合模块动态IO项
/// </summary>
public class DataRecordDynamicIO : DynamicIO
{
	private DelegateCommand deeteIOCommandDataRecord;

	/// <summary>
	/// 支持变量类型
	/// </summary>
	public List<string> SelectValueBasicTypes => new List<string> { "int", "float" };

	public ICommand DeleteCommandDataRecord
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = deeteIOCommandDataRecord;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)DeleteIOCommandExecuteDataRecord);
				DelegateCommand val3 = val2;
				deeteIOCommandDataRecord = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	/// <summary>
	/// 下发数据项信息
	/// </summary>
	/// <returns></returns>
	public override int SetParamUnit()
	{
		int num = 0;
		foreach (DataRecordDynamicIO item in (GetParentNode() as DataRecordModuleDynamicIO)?.ItemsA)
		{
			if (item.CustomVisibleA)
			{
				ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "var" + num, item.NameA);
				ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "type" + num, item.ValueTypeA);
				num++;
			}
		}
		ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "DataCount", num.ToString());
		return 0;
	}

	private void DeleteIOCommandExecuteDataRecord()
	{
		(GetParentNode() as IDynamic)?.RemoveIO(this);
		ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "DeleteIO", NameA);
	}
}
