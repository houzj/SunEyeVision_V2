using System.Collections.Generic;

namespace FeatureMatchCommonControl;

internal class FeaturepatMatchDialogUIDataStore
{
	internal class SubData
	{
		public Dictionary<string, object> userDataDic = new Dictionary<string, object>();
	}

	internal ModelItem[] items;

	internal byte[][] ModuleDatas;

	internal List<SubData> subDatas = new List<SubData>();
}
