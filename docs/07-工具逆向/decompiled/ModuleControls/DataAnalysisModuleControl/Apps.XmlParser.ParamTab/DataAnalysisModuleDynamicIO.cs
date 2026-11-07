using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Apps.Log;
using HikExternalCall.Common;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataAnalysisModuleDynamicIO : DynamicIOContainer<DataAnalysisDynamicIO>
{
	public DataAnalysisModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public int InitByteData()
	{
		try
		{
			byte[] array = ((IUserBytesData)DataSource.Params)["BytesData"];
			if (array == null)
			{
				return 0;
			}
			List<Analy_BYTES_RULE> list = ExternalCallHelper.BytesToStructs<Analy_BYTES_RULE>(array);
			if (list == null)
			{
				return 0;
			}
			for (int i = 0; i < list.Count; i++)
			{
				string paramName = StringUtil.UTF8GetString(list[i].chName);
				DataAnalysisDynamicIO dataAnalysisDynamicIO = (DataAnalysisDynamicIO)ItemsA.FirstOrDefault((object x) => (x as DynamicIO).NameA == paramName);
				if (dataAnalysisDynamicIO != null)
				{
					dataAnalysisDynamicIO._orderType = (int)list[i].eOrderType;
					(dataAnalysisDynamicIO.ByteStartEndValue.IntegersA[0] as Integer).CurValue = list[i].nStartIndex;
					(dataAnalysisDynamicIO.ByteStartEndValue.IntegersA[1] as Integer).CurValue = list[i].nEndIndex;
				}
			}
			return 0;
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("InitByteData error:" + ex.ToString());
			return -1;
		}
	}
}
