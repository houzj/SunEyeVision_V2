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

namespace IMVSMarkInspModuVAControl;

public class FeaturepatMatchCompatible : FeaturepatMatchDialogData
{
	public override void UpdateModuleModel(IUserData userData, IParamsConfig param, string moduleTypeName)
	{
		if (userData == null || param == null)
		{
			LogHelper.moduleLog.Error("UpdateModuleModel param error");
			return;
		}
		LogHelper.moduleLog.Info("MarkInspModu UpdateModuleModel start");
		try
		{
			string key = moduleTypeName + "_TrainData";
			string key2 = moduleTypeName + "_TrainData_CreatImageBuffer";
			string text = moduleTypeName + "_TrainData_InheritImageBuffer";
			byte[] array = userData[key];
			if (array != null)
			{
				ModuleTrainData moduleTrainData = new ModuleTrainData();
				moduleTrainData.loadXml(array);
				byte[] array2 = userData[key2];
				byte[] array3 = userData[key2];
				if (array2 != null && ((FeaturepatMatchDataBase)(moduleTrainData.FeatureMatchData?)).CurImageData != null)
				{
					((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData.Buffer = array2;
				}
				if (array3 != null && moduleTrainData.InheritImageData != null)
				{
					moduleTrainData.InheritImageData.Buffer = array3;
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
				param.TimeOut = 2147483647u;
				string s = ((IUserStringData)param)["RoughMatchInfoSour"];
				if (int.Parse(s) == 0 && !moduleTrainData.FeatureMatchData.IsSameVersion())
				{
					LogHelper.moduleLog.Info("UpdateRoughModel start");
					moduleTrainData.FeatureMatchData.CreateModel();
					try
					{
						OCRTrainDataMgr.Instance.SetDatumImage(((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData, param);
						int num = 0;
						int num2 = 0;
						if (((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData != null)
						{
							num = ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData.Width;
							num2 = ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).CurImageData.Height;
						}
						((IUserBytesData)param)["SetRoughRoiData"] = ModuleCommonMethodMgr.Instance.GetOneRoiBuffer(moduleTrainData.CreateRoiParam, (float)num, (float)num2);
						((IUserBytesData)param)["RoughModelTrain"] = ((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).ModelData;
					}
					catch (Exception ex)
					{
						int result = 0;
						string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
						LogHelper.moduleLog.Error("RoughModelTrain error: " + text2);
					}
					LogHelper.moduleLog.Info("UpdateRoughModel end");
				}
				LogHelper.moduleLog.Info("UpdateExactModel start");
				try
				{
					((IUserBytesData)param)["AdjustMarkBox"] = moduleTrainData.CharRoiData;
					byte[] value = new byte[1];
					((IUserBytesData)param)["ExactModelTrain"] = value;
				}
				catch (Exception ex2)
				{
					int result2 = 0;
					string text3 = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result2) ? ErrorCodeInfo.GetErrorInfo(result2) : ex2.Message);
					LogHelper.moduleLog.Error("ExactModelTrain error: " + text3);
				}
				param.TimeOut = 3000u;
				LogHelper.moduleLog.Info("UpdateExactModel end");
				try
				{
					userData[key] = null;
					string key4 = moduleTypeName + "_PointsData";
					userData[key4] = null;
					array = Encoding.UTF8.GetBytes(moduleTrainData.ToXmlStringWithHead(IsShort: true));
					if (array != null)
					{
						userData[key] = array;
					}
					byte[] array4 = SerializationUnit.SerializeObject(((FeaturepatMatchDataBase)moduleTrainData.FeatureMatchData).PointsData);
					if (array4 != null)
					{
						userData[key4] = array4;
					}
				}
				catch (Exception ex3)
				{
					LogHelper.moduleLog.Error($"Save data to server error: {ex3.Message}");
				}
			}
		}
		catch (Exception ex4)
		{
			LogHelper.moduleLog.Error($"UpdateModuleModel error: {ex4.Message}");
		}
		LogHelper.moduleLog.Info("MarkInspModu UpdateModuleModel end");
	}
}
