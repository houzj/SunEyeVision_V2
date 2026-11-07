using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Apps.ErrorCode;
using Apps.Log;
using Hik.Util.Model;
using HikPcUI.ImageView;
using VM.ModuleControl;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSInspectModuControl;

public class FeaturepatMatchCompatible : FeaturepatMatchDialogData
{
	public override void UpdateModuleModel(IUserData userData, IParamsConfig param, string moduleTypeName)
	{
		if (userData == null || param == null)
		{
			LogHelper.moduleLog.Error("UpdateModuleModel param error");
			return;
		}
		LogHelper.moduleLog.Info("InspectModu UpdateModuleModel start");
		try
		{
			string key = moduleTypeName + "_TrainData";
			byte[] array = userData[key];
			if (array != null)
			{
				ModuleTrainData moduleTrainData = new ModuleTrainData();
				moduleTrainData.loadXml(array);
				if (((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData == null)
				{
					string key2 = moduleTypeName + "_ModelData";
					((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData = userData[key2];
				}
				if (((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData == null || ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData.Count == 0)
				{
					string key3 = moduleTypeName + "_PointsData";
					byte[] bytes = userData[key3];
					((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData = (ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>)SerializationUnit.DeserializeObject(bytes);
				}
				if (((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData == null)
				{
					((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
				}
				if (!moduleTrainData.FeatureMatchData.IsSameVersion())
				{
					moduleTrainData.FeatureMatchData.CreateModel();
					try
					{
						((IUserBytesData)param)["SetFixPoint"] = TrainDataMgr.Instance.GetPointBuffer((float)((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).FixPointX, (float)((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).FixPointY);
						((IUserBytesData)param)["AddMatchModelData"] = ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData;
					}
					catch (Exception ex)
					{
						int result = 0;
						string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
						LogHelper.moduleLog.Error("CoarsePositioning error: " + text);
					}
					try
					{
						string key4 = moduleTypeName + "_ModelData";
						userData[key4] = null;
						userData[key] = null;
						string key5 = moduleTypeName + "_PointsData";
						userData[key5] = null;
						if (((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData != null)
						{
							userData[key4] = ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData;
							((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData = null;
						}
						array = Encoding.UTF8.GetBytes(moduleTrainData.ToXmlStringWithHead(IsShort: true));
						if (array != null)
						{
							userData[key] = array;
						}
						byte[] array2 = SerializationUnit.SerializeObject(((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData);
						if (array2 != null)
						{
							userData[key5] = array2;
						}
					}
					catch (Exception ex2)
					{
						LogHelper.moduleLog.Error($"Save data to server error: {ex2.Message}");
					}
				}
			}
		}
		catch (Exception ex3)
		{
			LogHelper.moduleLog.Error($"UpdateModuleModel error: {ex3.Message}");
		}
		LogHelper.moduleLog.Info("InspectModu UpdateModuleModel end");
	}
}
