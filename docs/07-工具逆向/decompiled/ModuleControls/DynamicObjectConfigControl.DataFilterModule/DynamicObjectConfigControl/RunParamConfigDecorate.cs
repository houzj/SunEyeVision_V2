using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.BaseData;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.UI.Data;
using DynamicObjectConfigControl.XmlData;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

public class RunParamConfigDecorate : NotifyPropertyChangedBase, IDecorate
{
	internal VmModuleSet Owner;

	private bool _IsForGroup;

	private string _TypeName;

	private DynamicNameValueItemOfGroupCategory _GroupName;

	private DynamicNameValueItemOfGroup _DecorateSource;

	private ICommand _RelationModulesConfigCommand = null;

	private ICommand _SyncCommand = null;

	private DelegateCommand<string> _TextChangedCommand = null;

	public bool IsForGroup
	{
		get
		{
			return _IsForGroup;
		}
		set
		{
			_IsForGroup = value;
		}
	}

	public string Name
	{
		get
		{
			return DecorateSource.Name;
		}
		set
		{
			string text = null;
			text = ((!value.StartsWith("%")) ? AppLocalizationService.UnDoTranslate(value) : value);
			if (!(DecorateSource.Name != text))
			{
				return;
			}
			if (CheckName(text))
			{
				try
				{
					ServerSDKManager.serverSDKManager.mModuleManager.ModifyDynamicParam(Owner.ID, Name, text);
					ChangeUserDataName(Name, text);
					DecorateSource.NameA = text;
				}
				catch (VmException vmException)
				{
					ShowMessage.ShowVmExceptionMessage(vmException);
					((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
				}
			}
			((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
		}
	}

	public string Path => GetPath(Name, Owner);

	public string TypeName
	{
		get
		{
			if (_TypeName == null)
			{
				_TypeName = GroupRunParamConfigData.SelectableList.Where((GroupRunParamSelectableTypeItem p) => p.TypeList.Contains(DecorateSource.CloneSource.GetType().Name)).Last().Name;
			}
			return _TypeName;
		}
	}

	public DynamicNameValueItemOfGroupCategory GroupName
	{
		get
		{
			if (DecorateSource != null)
			{
				return DecorateSource.Group;
			}
			return _GroupName;
		}
		set
		{
			if (DecorateSource != null)
			{
				if (DecorateSource.Group != value)
				{
					DecorateSource.Group = value;
					((NotifyPropertyChangedBase)this).OnPropertyChanged("GroupName");
				}
			}
			else if (_GroupName != value)
			{
				_GroupName = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("GroupName");
			}
		}
	}

	public Dictionary<VmModule, List<string>> RelationModules { get; } = new Dictionary<VmModule, List<string>>();

	public string RelationModulesStr
	{
		get
		{
			List<VmModule> list = new List<VmModule>();
			RelationModules.Clear();
			try
			{
				ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST? dynamicParamRelationByKey = ServerSDKManager.serverSDKManager.mModuleManager.GetDynamicParamRelationByKey(Owner.ID, Name);
				if (dynamicParamRelationByKey.HasValue)
				{
					ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST value = dynamicParamRelationByKey.Value;
					for (int i = 0; i < value.nNum; i++)
					{
						ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION iMVS_DYNAMIC_PARAM_RELATION = value.stRelationList[i];
						uint id = iMVS_DYNAMIC_PARAM_RELATION.nObjectID;
						string item = StringUtil.UTF8GetString(iMVS_DYNAMIC_PARAM_RELATION.strName);
						VmModule vmModule = Owner.Modules.FirstOrDefault((VmModule p) => p.ID == id);
						if (vmModule != null && !list.Contains(vmModule))
						{
							list.Add(vmModule);
						}
						if (RelationModules.TryGetValue(vmModule, out var value2))
						{
							value2.Add(item);
							continue;
						}
						RelationModules.Add(vmModule, new List<string> { item });
					}
				}
			}
			catch
			{
			}
			return string.Join(",", list);
		}
	}

	public DynamicNameValueItemOfGroup DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
			}
		}
	}

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value as DynamicNameValueItemOfGroup;
		}
	}

	public ICommand RelationModulesConfigCommand => _RelationModulesConfigCommand ?? (_RelationModulesConfigCommand = (ICommand)new DelegateCommand<DialogHandler>((Action<DialogHandler>)RelationModulesConfigCommandExecute));

	public ICommand SyncCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _SyncCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)SyncCommandExcute);
				ICommand command2 = (ICommand)val;
				_SyncCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedExecute)));

	public static string[] GetPathInfo(string name, VmModuleSet moduleSet)
	{
		string key = $"{VmModuleSet.HeadPathPre}{name}";
		return StringUtil.UTF8GetString(moduleSet.UserData[key]).Split('.');
	}

	public static string GetPath(string name, VmModuleSet moduleSet)
	{
		string[] pathInfo = GetPathInfo(name, moduleSet);
		string arg = pathInfo[0];
		string arg2 = pathInfo[2];
		return $"{arg}.{arg2}";
	}

	internal Dictionary<uint, string> GetRelationDic()
	{
		Dictionary<uint, string> dictionary = null;
		ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST? dynamicParamRelationByKey = ServerSDKManager.serverSDKManager.mModuleManager.GetDynamicParamRelationByKey(Owner.ID, Name);
		if (dynamicParamRelationByKey.HasValue)
		{
			ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST value = dynamicParamRelationByKey.Value;
			if (value.nNum >= 1)
			{
				dictionary = new Dictionary<uint, string>();
				for (int i = 0; i < value.nNum; i++)
				{
					ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION iMVS_DYNAMIC_PARAM_RELATION = value.stRelationList[i];
					uint nObjectID = iMVS_DYNAMIC_PARAM_RELATION.nObjectID;
					string value2 = StringUtil.UTF8GetString(iMVS_DYNAMIC_PARAM_RELATION.strName);
					dictionary.Add(nObjectID, value2);
				}
				return dictionary;
			}
		}
		return null;
	}

	internal void ClearRelationLeftFirst(out bool isOnlyOne, out Dictionary<uint, string> relation)
	{
		ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST? dynamicParamRelationByKey = ServerSDKManager.serverSDKManager.mModuleManager.GetDynamicParamRelationByKey(Owner.ID, Name);
		if (dynamicParamRelationByKey.HasValue)
		{
			ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST value = dynamicParamRelationByKey.Value;
			if (value.nNum <= 1)
			{
				isOnlyOne = true;
				relation = null;
				return;
			}
			isOnlyOne = false;
			relation = new Dictionary<uint, string>();
			for (int i = 0; i < value.nNum; i++)
			{
				ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION iMVS_DYNAMIC_PARAM_RELATION = value.stRelationList[i];
				uint nObjectID = iMVS_DYNAMIC_PARAM_RELATION.nObjectID;
				string text = StringUtil.UTF8GetString(iMVS_DYNAMIC_PARAM_RELATION.strName);
				relation.Add(nObjectID, text);
				if (i > 0)
				{
					ServerSDKManager.serverSDKManager.mModuleManager.DeleteDynamicParamRelation(Owner.ID, Name, nObjectID, text);
				}
			}
		}
		else
		{
			isOnlyOne = false;
			relation = null;
		}
	}

	internal void SetGroupID(int id)
	{
		DecorateSource.SetGroupID(id);
	}

	internal void DeleteFirstRelationConnectAfter(Dictionary<uint, string> dic)
	{
		bool flag = true;
		foreach (KeyValuePair<uint, string> item in dic)
		{
			if (flag)
			{
				flag = false;
				ServerSDKManager.serverSDKManager.mModuleManager.DeleteDynamicParamRelation(Owner.ID, Name, item.Key, item.Value);
			}
			else
			{
				ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParamRelation(Owner.ID, Name, item.Key, item.Value);
			}
		}
	}

	internal void RecoverRelation(Dictionary<uint, string> dic)
	{
		bool flag = true;
		foreach (KeyValuePair<uint, string> item in dic)
		{
			if (flag)
			{
				flag = false;
			}
			else
			{
				ServerSDKManager.serverSDKManager.mModuleManager.DeleteDynamicParamRelation(Owner.ID, Name, item.Key, item.Value);
			}
		}
		foreach (KeyValuePair<uint, string> item2 in dic)
		{
			ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParamRelation(Owner.ID, Name, item2.Key, item2.Value);
		}
	}

	private bool CheckName(string newName)
	{
		if (newName.Length == 2)
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameEmpty", MessageIcons.Error, isTranslate: true);
			return false;
		}
		if (!VmIODecorate.CommonNameCheck(newName))
		{
			return false;
		}
		if (DecorateSource.GetParentNode<GroupRunParam>().Collection.Any((DynamicNameValueItemOfGroup p) => p.Name == newName))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameExited", MessageIcons.Error, isTranslate: true);
			return false;
		}
		return true;
	}

	private void RelationModulesConfigCommandExecute(DialogHandler method)
	{
		string key = $"{VmModuleSet.HeadPathPre}{Name}";
		string[] array = StringUtil.UTF8GetString(Owner.UserData[key]).Split('.');
		string strModuleName = array[0];
		string text = array[1];
		ModulesPickData modulesPickData = new ModulesPickData();
		modulesPickData.Node = Owner;
		modulesPickData.StrModuleName = strModuleName;
		modulesPickData.ParamName = text;
		modulesPickData.ParamDisplayName = DecorateSource.CloneSource.DisplayName;
		modulesPickData.Init();
		ObservableCollection<object> observableCollection = ((modulesPickData.EnumEntrysA.Count == 0) ? null : (modulesPickData.EnumEntrysA[0] as Holder).EnumEntrysA);
		List<Holder> list = new List<Holder>();
		if (observableCollection != null)
		{
			foreach (Holder item in observableCollection.OfType<Holder>())
			{
				NameValueItem nameValueItem = item.HolderTarget as NameValueItem;
				VmModule vmModule = nameValueItem.ParamsSource as VmModule;
				if (!RelationModules.ContainsKey(vmModule))
				{
					continue;
				}
				if (vmModule is IVmModuleSet)
				{
					if (RelationModules[vmModule].Contains(nameValueItem.Name))
					{
						item.CheckEnabled = true;
						list.Add(item);
					}
				}
				else
				{
					item.CheckEnabled = true;
					list.Add(item);
				}
			}
		}
		if (!(method.Invoke((object)modulesPickData) ?? false))
		{
			return;
		}
		if (observableCollection != null)
		{
			foreach (Holder item2 in observableCollection.OfType<Holder>())
			{
				if (!item2.CheckEnabled.Value)
				{
					continue;
				}
				list.Remove(item2);
				NameValueItem nameValueItem2 = item2.HolderTarget as NameValueItem;
				VmModule vmModule2 = nameValueItem2.ParamsSource as VmModule;
				if (vmModule2 is IVmModuleSet)
				{
					if (!RelationModules.TryGetValue(vmModule2, out var value))
					{
						value = new List<string>();
						RelationModules.Add(vmModule2, value);
					}
					if (!value.Contains(nameValueItem2.Name))
					{
						value.Add(nameValueItem2.Name);
						ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParamRelation(Owner.ID, Name, vmModule2.ID, nameValueItem2.Name);
					}
				}
				else if (!RelationModules.ContainsKey(vmModule2))
				{
					RelationModules.Add(vmModule2, new List<string> { text });
					ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParamRelation(Owner.ID, Name, vmModule2.ID, nameValueItem2.Name);
				}
			}
			foreach (Holder item3 in list)
			{
				NameValueItem nameValueItem3 = item3.HolderTarget as NameValueItem;
				VmModule vmModule3 = nameValueItem3.ParamsSource as VmModule;
				if (vmModule3 is IVmModuleSet)
				{
					RelationModules[vmModule3].Remove(nameValueItem3.Name);
					if (RelationModules[vmModule3].Count == 0)
					{
						RelationModules.Remove(vmModule3);
					}
				}
				else
				{
					RelationModules.Remove(vmModule3);
				}
				ServerSDKManager.serverSDKManager.mModuleManager.DeleteDynamicParamRelation(Owner.ID, Name, vmModule3.ID, nameValueItem3.Name);
			}
		}
		((NotifyPropertyChangedBase)this).OnPropertyChanged("RelationModulesStr");
	}

	private void SyncCommandExcute()
	{
		ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST? dynamicParamRelationByKey = ServerSDKManager.serverSDKManager.mModuleManager.GetDynamicParamRelationByKey(Owner.ID, Name);
		if (dynamicParamRelationByKey.HasValue && dynamicParamRelationByKey.Value.nNum <= 1)
		{
			HikMessageBox.Show(string.Format("{0}", AppLocalizationService.GetLang("Lang_SyncCheck")));
			return;
		}
		try
		{
			if (DecorateSource.CloneSource is IGroupRunParamWithMultiParams multiParams)
			{
				DynamicNameValueItemOfGroup.SyncGroupRunParamWithMultiParams(multiParams, Owner, DecorateSource);
			}
			else
			{
				string value = DecorateSource.CloneSource.Value;
				DecorateSource.CloneSource.SetParamUnitDirect(value);
			}
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
			return;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.StackTrace.ToString());
			HikMessageBox.Show(ex.Message);
			return;
		}
		HikMessageBox.Show(string.Format("{0}{1}{2}", AppLocalizationService.GetLang("Data_Sync"), (AppLocalizationService.GetLangType() == "zh-cn") ? "" : " ", AppLocalizationService.GetLang("成功")));
	}

	private void ChangeUserDataName(string oldName, string newName)
	{
		string key = $"{VmModuleSet.HeadValuePre}{newName}";
		string key2 = $"{VmModuleSet.HeadValuePre}{oldName}";
		Owner.UserData[key] = Owner.UserData[key2];
		Owner.UserData[key2] = null;
		string key3 = $"{VmModuleSet.HeadPathPre}{newName}";
		string key4 = $"{VmModuleSet.HeadPathPre}{oldName}";
		Owner.UserData[key3] = Owner.UserData[key4];
		Owner.UserData[key4] = null;
		string key5 = $"{VmModuleSet.GroupIDPre}{newName}";
		string key6 = $"{VmModuleSet.GroupIDPre}{oldName}";
		Owner.UserData[key5] = Owner.UserData[key6];
		Owner.UserData[key6] = null;
	}

	private void TextChangedExecute(string newName)
	{
		Name = newName;
	}

	public override string ToString()
	{
		if (_DecorateSource == null)
		{
			return null;
		}
		return Name;
	}
}
