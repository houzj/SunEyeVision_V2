using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Util.Model;
using Hik.Xml;
using HikExternalCall.Common;
using HikPcUI.ImageView;
using VM.Controls;
using VM.ModuleControl;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace FeatureMatchCommonControl;

public class FeaturepatMatchConfigPanalDataBase : NameValueItem, IGroupRunParamWithMultiParams, IGroupRunParam
{
	private Assembly assembly = null;

	protected virtual string OpenFileDialogFilter => "(*.hpmxml)|*.hpmxml";

	public Assembly Assembly
	{
		get
		{
			if (assembly == null)
			{
				string currentPath = CurrentPathHelper.Instance.GetCurrentPath();
				currentPath += "Module(sp)\\x64\\.CommonControl\\PatCreateCSharp.dll";
				try
				{
					assembly = Assembly.UnsafeLoadFrom(currentPath);
				}
				catch (Exception arg)
				{
					LogHelper.objLog.Error($"Get assembly error:{arg},assembly path={currentPath}");
				}
			}
			return assembly;
		}
	}

	public FeaturepatMatchConfigPanalDataDecorate Decorate => new FeaturepatMatchConfigPanalDataDecorate
	{
		DecorateSource = this
	};

	string IGroupRunParam.DisplayForGroupType => "ConfigDialog";

	protected virtual FeaturepatMatchDialogDataBase GetModelData()
	{
		return null;
	}

	internal ModelItem CreateItemData(string modelName, int index)
	{
		return new ModelItem(this, ParamsSource as IVMRun, index)
		{
			Name = modelName,
			modelData = GetModelData()
		};
	}

	public virtual void ExternGetModelUIDataByModelName(FeaturepatMatchDialogDataBase featurepatMatchDialogDataBase, string modelName)
	{
	}

	internal bool GetModelUIDataByModelName(byte[] fileData, ModelItem itemData, string modelName)
	{
		try
		{
			IVmModule vmModule = ParamsSource as IVmModule;
			LogHelper.Info($"Basic data Length={fileData.Length}.");
			((BaseNode)(object)itemData.modelData).loadXml(fileData, false);
			if (itemData.modelData.CurImageDataA != null && itemData.modelData.CurImageDataA.ImageBuffer == null)
			{
				LogHelper.Info("Get image data start.");
				itemData.modelData.CurImageDataKeyStr = modelName + "_CurImageData";
				itemData.modelData.ItemOwner = this;
			}
			if (itemData.modelData.otherMask == null)
			{
				LogHelper.Info("Get othermask data start.");
				string key = modelName + "_OtherMask";
				itemData.modelData.otherMask = InnerGetByteUserDataValue(key);
				if (itemData.modelData.otherMask != null)
				{
					LogHelper.Info($"Othermask data Length={itemData.modelData.otherMask.Length}.");
				}
				LogHelper.Info("Get othermask data end.");
			}
			ExternGetModelUIDataByModelName(itemData.modelData, modelName);
			if (itemData.modelData.PointsData == null || itemData.modelData.PointsData.Count == 0)
			{
				LogHelper.Info("Get points data start.");
				string key2 = modelName + "_PointsData";
				byte[] array = InnerGetByteUserDataValue(key2);
				string key3 = modelName + "_PointsNums";
				byte[] array2 = InnerGetByteUserDataValue(key3);
				bool flag = array2 != null;
				if (flag)
				{
					string s = Encoding.UTF8.GetString(array2);
					flag = int.TryParse(s, out var result) && result > 0;
				}
				if (!flag)
				{
					itemData.modelData.PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
					object obj = SerializationUnit.DeserializeObject(array);
					if (obj is IEnumerable enumerable)
					{
						foreach (object item in enumerable)
						{
							dynamic val = item;
							itemData.modelData.PointsData.Add(new MVBPATMATCH_EDGEPOINT_OUT
							{
								x = val.x,
								y = val.y,
								score_flag = val.score_flag,
								weight_flag = val.weight_flag
							});
						}
					}
				}
				else
				{
					List<MVBPATMATCH_EDGEPOINT_OUT> list = ExternalCallHelper.BytesToStructs<MVBPATMATCH_EDGEPOINT_OUT>(array);
					itemData.modelData.PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>(list);
				}
				if (array != null)
				{
					LogHelper.Info($"Points data Length={array.Length}.");
				}
				LogHelper.Info("Get points data end.");
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error($"Get userdata error:{ex.Message}");
			return false;
		}
		return true;
	}

	protected virtual bool AddModelToModule(ModelItem itemData)
	{
		string empty = string.Empty;
		try
		{
			empty = InnerGetStringParamValue("GetNextAddModelIndex");
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Get next index error:{ex.Message}.");
			return false;
		}
		int result = -1;
		if (int.TryParse(empty, out result))
		{
			LogHelper.objLog.Info($"Get index of mode={result}.");
			itemData.Id = result + 1;
			if (string.IsNullOrEmpty(itemData.Name))
			{
				itemData.Name = AppLocalizationService.GetLang("Lang_NewTemp") + itemData.Id;
			}
			LogHelper.objLog.Info($"Want to set model name:{itemData.Name}.");
			try
			{
				InnerSetStringParamValue("SpareModelName", itemData.Name);
			}
			catch (Exception ex2)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
				LogHelper.objLog.Error($"Set model name error:{ex2.Message}");
				return false;
			}
			LogHelper.objLog.Info($"Want to set model data, Length={itemData.modelData.ModelData.Length}.");
			try
			{
				InnerSetByteParamValue("AddSpareModel", itemData.modelData.ModelData);
				AddUserData(result, itemData.modelData);
			}
			catch (Exception ex3)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
				LogHelper.objLog.Error($"Set model data error:{ex3.Message}");
				try
				{
					LogHelper.objLog.Info($"Try clear current model,index={result}");
					InnerSetStringParamValue("DeleteCandidateByIndex", result.ToString());
				}
				catch (Exception ex4)
				{
					LogHelper.objLog.Error($"Clear fail:{ex4.Message}");
				}
				return false;
			}
			return true;
		}
		LogHelper.objLog.Error($"Get index of model error, value is {empty}.");
		return false;
	}

	protected void AddUserData(int index, FeaturepatMatchDialogDataBase modelData)
	{
		string text = "SolModelFile_" + index;
		if (ParamsSource is IVmModule)
		{
			FeaturepatMatchDialogDataBase val = modelData.DeepCopyModelData(true, false);
			byte[] bytes = Encoding.UTF8.GetBytes(((BaseNode)(object)val).ToXmlStringWithHead(true));
			if (bytes == null)
			{
				throw new Exception("Can't get basicData");
			}
			string key = text + "_BasicData";
			InnerSetByteUserDataValue(key, bytes);
			string key2 = text + "_CurImageData";
			InnerSetByteUserDataValue(key2, modelData.CurImageDataA?.ImageBuffer);
			string key3 = text + "_OtherMask";
			InnerSetByteUserDataValue(key3, modelData.otherMask);
			string key4 = text + "_PointsData";
			InnerSetByteUserDataValue(key4, ExternalCallHelper.StructsToBytes(modelData.PointsData));
			if (modelData.PointsData.Count > 0)
			{
				string key5 = text + "_PointsNums";
				InnerSetByteUserDataValue(key5, Encoding.UTF8.GetBytes(modelData.PointsData.Count.ToString()));
			}
			ExternAddUserData(index, modelData);
		}
	}

	protected virtual void ExternAddUserData(int index, FeaturepatMatchDialogDataBase modelData)
	{
	}

	public virtual void LoadModel(IList list)
	{
	}

	internal bool GetIsBusy()
	{
		if (!(ParamsSource is IVMRun { IsRunning: var isRunning }))
		{
			return false;
		}
		return isRunning;
	}

	protected virtual void LoadModelToList(ModelItem itemData, IList list, bool needBuildModelData = true, bool needAddtoModule = false)
	{
		if (needBuildModelData)
		{
			LogHelper.objLog.Info("Rebuild model");
			int outLinePoints = FeatureMatchHelper.GetOutLinePoints(itemData.modelData);
			if (outLinePoints != 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CPRebuidErr"), MessageIcons.Warning);
				LogHelper.objLog.Error($"Create pat fail:errorcode={outLinePoints}");
			}
			if (AddModelToModule(itemData))
			{
				list.Add(itemData);
				LogHelper.objLog.Info("Add model to list succeed.");
			}
			else
			{
				LogHelper.objLog.Info("Add model to list fail.");
			}
		}
		else
		{
			LogHelper.objLog.Info("Add model directly.");
			if (!needAddtoModule || AddModelToModule(itemData))
			{
				list.Add(itemData);
				LogHelper.objLog.Info("Add model to list succeed.");
			}
		}
	}

	protected ModelItem AddModelToModuleCommandExecute(object obj, IList list)
	{
		ModelItem modelItem = CreateItemData("", 0);
		modelItem.modelData = (FeaturepatMatchDialogDataBase)((obj is FeaturepatMatchDialogDataBase) ? obj : null);
		if (AddModelToModule(modelItem))
		{
			list.Add(modelItem);
			LogHelper.objLog.Info("Creat new model succeed.");
			return modelItem;
		}
		LogHelper.objLog.Info("Create new model fail.");
		return null;
	}

	public virtual ModelItem CreateModel(bool isNew, IList list, ModelItem selectedItem)
	{
		return null;
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return new FeaturepatMatchForGroupDecorate
		{
			DecorateSource = this
		};
	}

	object IGroupRunParamWithMultiParams.GetAllData()
	{
		FeaturepatMatchDialogUIDataStore featurepatMatchDialogUIDataStore = new FeaturepatMatchDialogUIDataStore();
		FeaturepatMatchConfigPanalDataDecorate featurepatMatchConfigPanalDataDecorate = new FeaturepatMatchConfigPanalDataDecorate
		{
			DecorateSource = this
		};
		ObservableCollection<object> modelList = featurepatMatchConfigPanalDataDecorate.ModelList;
		featurepatMatchDialogUIDataStore.items = modelList.OfType<ModelItem>().ToArray();
		featurepatMatchDialogUIDataStore.ModuleDatas = new byte[modelList.Count][];
		for (int i = 0; i < modelList.Count; i++)
		{
			ImageData curImageDataA = featurepatMatchDialogUIDataStore.items[i].modelData.CurImageDataA;
			byte[] array = InnerGetByteParamValue($"CandidateData{i}");
			featurepatMatchDialogUIDataStore.ModuleDatas[i] = array;
		}
		return featurepatMatchDialogUIDataStore;
	}

	void IGroupRunParamWithMultiParams.SetAllData(object data)
	{
		if (data is FeaturepatMatchDialogUIDataStore featurepatMatchDialogUIDataStore)
		{
			for (int i = 0; i < featurepatMatchDialogUIDataStore.items.Length; i++)
			{
				ModelItem modelItem = featurepatMatchDialogUIDataStore.items[i];
				modelItem.modelData.ModelData = featurepatMatchDialogUIDataStore.ModuleDatas[i];
				AddModelToModule(modelItem);
				modelItem.modelData.ModelData = null;
			}
		}
	}

	void IGroupRunParamWithMultiParams.Clear()
	{
		FeaturepatMatchConfigPanalDataDecorate featurepatMatchConfigPanalDataDecorate = new FeaturepatMatchConfigPanalDataDecorate
		{
			DecorateSource = this
		};
		featurepatMatchConfigPanalDataDecorate.DeleteAllModels(isShowWarn: false);
	}
}
