using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Xml;
using Apps.ColorFun;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using FeatureMatchCommonControl;
using Hik.Xml;
using IMVSHPFeatureMatchModuControl;
using Microsoft.Win32;
using ModelCommonHelper;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class FeaturepatMatchConfigPanalData : FeaturepatMatchConfigPanalDataBase
{
	protected override string OpenFileDialogFilter => "(*.hpmxml)|*.hpmxml";

	protected override void ExternAddUserData(int index, FeaturepatMatchDialogDataBase modelData)
	{
		string text = "SolModelFile_" + index;
		if (((NameValueItem)this).ParamsSource is IVmModule vmModule)
		{
			string key = text + "_OtherMask2";
			vmModule.UserData[key] = (modelData as HPFeaturepatMatchDialogData).otherMask2;
		}
	}

	protected override FeaturepatMatchDialogDataBase GetModelData()
	{
		return (FeaturepatMatchDialogDataBase)(object)new HPFeaturepatMatchDialogData();
	}

	protected virtual bool AnalysisModelItem(string fPath, string oldRootNodeName, ModelItem itemData)
	{
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Expected Obj, but got Unknown
		//IL_01fd: Expected Obj, but got Unknown
		using (FileStream fileStream = new FileStream(fPath, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			if (fileStream.Length > 1073741824)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LengthError"), MessageIcons.Error);
				return false;
			}
		}
		byte[] array = null;
		try
		{
			array = EncryptionAndDecryptionHelper.GetLibFile(fPath);
			if (array == null)
			{
				return false;
			}
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + " " + AppLocalizationService.GetLang(ErrorCodeInfo.GetErrorInfo(errorCode)), MessageIcons.Error);
			return false;
		}
		catch (Exception ex2)
		{
			string text = AppLocalizationService.GetLang("读取样本库失败") + " :" + AppLocalizationService.GetLang("读取文件失败");
			LogHelper.Error(text);
			if (ex2 is EndOfStreamException)
			{
				text = AppLocalizationService.GetLang("读取样本库失败") + " :" + AppLocalizationService.GetLang("文件格式异常") + "!";
			}
			HikMessageBox.Show(text, MessageIcons.Error, isTranslate: true);
			return false;
		}
		XmlDocument xmlDocument = new XmlDocument();
		using (MemoryStream memoryStream = new MemoryStream(array))
		{
			memoryStream.Flush();
			memoryStream.Position = 0L;
			xmlDocument.Load(memoryStream);
		}
		if (xmlDocument.LastChild.Name == oldRootNodeName)
		{
			((BaseNode)(object)itemData.modelData).loadXml(fPath, false);
		}
		else if (xmlDocument.LastChild.Name == "VM.ModuleControl.SDKModelData")
		{
			SDKFeatureMatchData val = new SDKFeatureMatchData
			{
				UserInterfaceData = (SDKFeatureMatchUIData)(object)new SDKHPFeatureMatchUIData(),
				ModuleModelData = (object)new SDKFeaturetMatchModelData()
			};
			XmlNode xmlNode = xmlDocument.LastChild.SelectSingleNode("SDKImportData");
			if (xmlNode == null)
			{
				throw new Exception("Node SDKImportData not found");
			}
			XmlNode xmlNode2 = xmlNode.SelectSingleNode("ModuleModelData");
			if (xmlNode2 == null)
			{
				throw new Exception("Node ModuleModelData not found");
			}
			object moduleModelData = val.ModuleModelData;
			((BaseNode)((moduleModelData is SDKFeaturetMatchModelData) ? moduleModelData : null))?.loadXmlByString(xmlNode2.OuterXml, false);
			if (val.ModuleModelData != null)
			{
				object moduleModelData2 = val.ModuleModelData;
				SDKFeaturetMatchModelData val2;
				if ((val2 = (SDKFeaturetMatchModelData)((moduleModelData2 is SDKFeaturetMatchModelData) ? moduleModelData2 : null)) != null)
				{
					itemData.modelData.ModelData = val2.AddSpareModel_ByteArray;
				}
			}
			XmlNode xmlNode3 = xmlNode.SelectSingleNode("UserInterfaceData");
			if (xmlNode3 == null)
			{
				throw new Exception("Node UserInterfaceData not found");
			}
			((BaseNode)(object)(val.UserInterfaceData as SDKHPFeatureMatchUIData))?.loadXmlByString(xmlNode3.OuterXml, false);
			if (val.UserInterfaceData != null)
			{
				byte[] basicData_ByteArray = val.UserInterfaceData.BasicData_ByteArray;
				if (basicData_ByteArray != null && basicData_ByteArray.Length != 0)
				{
					string result = Encoding.UTF8.GetString(basicData_ByteArray);
					((BaseNode)(object)itemData.modelData).loadXmlByString(result, true);
					byte[] curImageData_ByteArray = val.UserInterfaceData.CurImageData_ByteArray;
					if (itemData.modelData.CurImageDataA != null)
					{
						itemData.modelData.CurImageDataA.Buffer = curImageData_ByteArray;
					}
					byte[] otherMask_ByteArray = val.UserInterfaceData.OtherMask_ByteArray;
					itemData.modelData.otherMask = otherMask_ByteArray;
					if (val.UserInterfaceData is SDKHPFeatureMatchUIData { OtherMask2_ByteArray: var otherMask2_ByteArray })
					{
						(itemData.modelData as HPFeaturepatMatchDialogData).otherMask2 = otherMask2_ByteArray;
						if (otherMask2_ByteArray != null)
						{
							(itemData.modelData as HPFeaturepatMatchDialogData).IsUsePen = true;
						}
					}
				}
			}
		}
		Type type = ((FeaturepatMatchConfigPanalDataBase)this).Assembly?.GetType("PatCreateCSharp.PatCreateManagerCSharp");
		PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		dynamic value = property.GetValue(null, null);
		string s = xmlDocument.ToXml(omitXmlDeclaration: false);
		value.CreatePatByFeatureData(Encoding.UTF8.GetBytes(s), itemData.modelData);
		return true;
	}

	protected virtual string GetOldModelRootNodeName()
	{
		return "Apps.XmlParser.ParamTab.HPFeaturepatMatchDialogData";
	}

	public override void LoadModel(IList list)
	{
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected Obj, but got Unknown
		LogHelper.objLog.Info("Load model strat.");
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = ((FeaturepatMatchConfigPanalDataBase)this).OpenFileDialogFilter
		};
		string text = ConfigurationHelper.INIGetDefaultOpenPath((((NameValueItem)this).ParamsSource as IVmModule)?.ModuleDir);
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(openFileDialog, text);
		LogHelper.objLog.Info($"Get initpath:{text}.");
		if (openFileDialog.ShowDialog() == true)
		{
			string fileName = openFileDialog.FileName;
			string text2 = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
			LogHelper.objLog.Info($"Want to load file:{text2}.");
			if (text2.Length > 63)
			{
				text2 = text2.Substring(0, 63);
				LogHelper.objLog.Info($"Model name is longer than 63, cut to {text2}.");
			}
			ModelItem val = new ModelItem((NameValueItem)(object)this, ((NameValueItem)this).ParamsSource as IVMRun)
			{
				Name = text2,
				modelData = ((FeaturepatMatchConfigPanalDataBase)this).GetModelData()
			};
			LogHelper.objLog.Info("Load xml start.");
			try
			{
				if (AnalysisModelItem(fileName, GetOldModelRootNodeName(), val))
				{
					((FeaturepatMatchConfigPanalDataBase)this).LoadModelToList(val, list, false, true);
				}
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error($"LoadModule Err:{ex.ToString()}.");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadErrtoDecode"), MessageIcons.Error);
			}
			if (File.Exists(fileName))
			{
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(fileName), (((NameValueItem)this).ParamsSource as IVmModule)?.ModuleDir);
			}
			LogHelper.objLog.Info("Load xml end.");
		}
		LogHelper.objLog.Info("Load model end.");
	}

	public override void ExternGetModelUIDataByModelName(FeaturepatMatchDialogDataBase featurepatMatchDialogDataBase, string modelName)
	{
		if ((featurepatMatchDialogDataBase as HPFeaturepatMatchDialogData).otherMask2 == null)
		{
			LogHelper.objLog.Info("Get othermask data start.");
			string key = modelName + "_OtherMask2";
			(featurepatMatchDialogDataBase as HPFeaturepatMatchDialogData).otherMask2 = ((NameValueItem)this).InnerGetByteUserDataValue(key);
			if ((featurepatMatchDialogDataBase as HPFeaturepatMatchDialogData).otherMask2 != null)
			{
				LogHelper.objLog.Info($"Othermask2 data Length={(featurepatMatchDialogDataBase as HPFeaturepatMatchDialogData).otherMask2.Length}.");
			}
			LogHelper.objLog.Info("Get othermask2 data end.");
		}
	}

	protected virtual HPFeaturepatMatchDialogUI GetFeaturepatMatchDialogUI()
	{
		return new HPFeaturepatMatchDialogUI();
	}

	public override ModelItem CreateModel(bool isNew, IList list, ModelItem selectedItem)
	{
		LogHelper.objLog.Info("Create model config window start.");
		ResourceDictionary resourceDictionary = new ResourceDictionary();
		resourceDictionary.Source = ((ResourceDictionary)(object)LocalizationResourceDictionary.Instance).Source;
		BaseWindow baseWindow = new BaseWindow
		{
			ShowInTaskbar = false,
			Height = 534.0,
			Width = 900.0,
			Title = AppLocalizationService.GetLang("Lang_ModelSet"),
			FontSize = 14.0,
			FontWeight = FontWeights.Normal,
			MinButtonVisibility = Visibility.Collapsed,
			MaxButtonVisibility = Visibility.Visible
		};
		baseWindow.Resources.MergedDictionaries.Add(resourceDictionary);
		ColorThemesResourceDictionary.GetInstance(baseWindow);
		HPFeaturepatMatchDialogUI featurepatMatchDialogUI = GetFeaturepatMatchDialogUI();
		featurepatMatchDialogUI.Owner = baseWindow;
		featurepatMatchDialogUI.OwnerModule = ((NameValueItem)this).ParamsSource as IVmModule;
		featurepatMatchDialogUI.OwnerModuleParam = ((NameValueItem)this).ParamsSource;
		featurepatMatchDialogUI.OwnerParam = this;
		baseWindow.Content = featurepatMatchDialogUI;
		if (featurepatMatchDialogUI.OwnerModule == null)
		{
			featurepatMatchDialogUI.HideCurImageButton();
		}
		if (isNew)
		{
			LogHelper.objLog.Info("Create a new model.");
			try
			{
				LogHelper.objLog.Info("Get current image.");
				featurepatMatchDialogUI.featurepatMatchDialogData = ((FeaturepatMatchConfigPanalDataBase)this).GetModelData() as HPFeaturepatMatchDialogData;
				featurepatMatchDialogUI.featurepatMatchDialogData.MatchPointLock = !isNew;
				featurepatMatchDialogUI.AddBindingCollectionEvent();
				featurepatMatchDialogUI.GetCurImage();
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), ex.Message, MessageIcons.Error);
				LogHelper.objLog.Error($"GetCurImageErr:{ex.Message}.");
				return null;
			}
			CreatModel.Instance.IsCreatingModel = true;
			if (baseWindow.ShowDialog() == true)
			{
				((FeaturepatMatchDialogDataBase)featurepatMatchDialogUI.featurepatMatchDialogData).ClearCompareSit();
				if (((FeaturepatMatchDialogDataBase)featurepatMatchDialogUI.featurepatMatchDialogData).ModelData == null)
				{
					return null;
				}
				ModelItem result = ((FeaturepatMatchConfigPanalDataBase)this).AddModelToModuleCommandExecute((object)featurepatMatchDialogUI.featurepatMatchDialogData, list);
				((IRunService)this)?.PreExecuteRunModel();
				CreatModel.Instance.IsCreatingModel = false;
				return result;
			}
			CreatModel.Instance.IsCreatingModel = false;
			LogHelper.objLog.Info("Create model end.");
		}
		else
		{
			LogHelper.objLog.Info("Edit model start.");
			if (selectedItem == null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoEdit"), MessageIcons.Error);
				LogHelper.objLog.Error("Selected model is empty, edit fail.");
				return null;
			}
			FeaturepatMatchDialogDataBase modelData = selectedItem.modelData;
			if (modelData.CurImageDataA == null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("模型不包含原图，不允许编辑"));
				return null;
			}
			if (modelData == null || !(modelData is HPFeaturepatMatchDialogData))
			{
				LogHelper.objLog.Error("CurModelData type error, edit fail.");
				return null;
			}
			featurepatMatchDialogUI.featurepatMatchDialogData = (HPFeaturepatMatchDialogData)(object)modelData;
			featurepatMatchDialogUI.featurepatMatchDialogData.MatchPointLock = !isNew;
			featurepatMatchDialogUI.AddBindingCollectionEvent();
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogUI.featurepatMatchDialogData).ReFreshData();
			((FrameworkElement)(object)featurepatMatchDialogUI).DataContext = modelData;
			if (featurepatMatchDialogUI.featurepatMatchDialogData.IsUsePen)
			{
				featurepatMatchDialogUI.isLoading = true;
			}
			LogHelper.objLog.Info("Save old data for rolling back.");
			FeaturepatMatchDialogDataBase modelData2 = modelData.DeepCopyModelData(false, false);
			featurepatMatchDialogUI.RefreshByModelData();
			FeatureMatchHelper.AdaptVersion((FeaturepatMatchDialogDataBase)(object)featurepatMatchDialogUI.featurepatMatchDialogData);
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogUI.featurepatMatchDialogData).SetCompareSit();
			CreatModel.Instance.IsCreatingModel = true;
			if (baseWindow.ShowDialog() == true)
			{
				((FeaturepatMatchDialogDataBase)featurepatMatchDialogUI.featurepatMatchDialogData).ClearCompareSit();
				if (selectedItem.modelData.ModelData == null)
				{
					return null;
				}
				LogHelper.objLog.Info($"Close model config window. Want to change model data,index={selectedItem.Id - 1}.");
				try
				{
					((NameValueItem)this).InnerSetStringParamValue("ChooseSpareModelIndex", (selectedItem.Id - 1).ToString());
				}
				catch (Exception ex2)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoEdit"), MessageIcons.Error);
					LogHelper.objLog.Error($"Choose model fail:{ex2.Message}.");
					selectedItem.modelData = modelData2;
					return null;
				}
				LogHelper.objLog.Info($"Want to change model data,data Length={selectedItem.modelData.ModelData.Length}.");
				try
				{
					((NameValueItem)this).InnerSetByteParamValue("ChangeSpareModel", selectedItem.modelData.ModelData);
					((FeaturepatMatchConfigPanalDataBase)this).AddUserData(selectedItem.Id - 1, selectedItem.modelData);
				}
				catch (Exception ex3)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Feature"), AppLocalizationService.GetLang("Lang_ModuErrtoAdd"), MessageIcons.Error);
					LogHelper.objLog.Error($"Change model data fail:{ex3.Message}.");
					selectedItem.modelData = modelData2;
					return null;
				}
				((IRunService)this)?.PreExecuteRunModel();
				CreatModel.Instance.IsCreatingModel = false;
				LogHelper.objLog.Info("Edit model succeed.");
			}
			else
			{
				CreatModel.Instance.IsCreatingModel = false;
				selectedItem.modelData = modelData2;
				LogHelper.objLog.Info("Abandon, roll back model data.");
			}
			((FeaturepatMatchDialogDataBase)featurepatMatchDialogUI.featurepatMatchDialogData).ModelData = null;
		}
		LogHelper.objLog.Info("Create model config window end.");
		return null;
	}
}
