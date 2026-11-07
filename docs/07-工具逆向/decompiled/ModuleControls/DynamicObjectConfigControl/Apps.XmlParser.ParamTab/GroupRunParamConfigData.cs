using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Apps.Localization;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.Common;
using DynamicObjectConfigControl.UI.Data;
using DynamicObjectConfigControl.XmlData;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GroupRunParamConfigData : ParamConfigDataBase, IParamList
{
	private List<string> _ParamList;

	private IList _EnumEntrysA;

	private GroupRunParamSelectableTypeItem _ComboBoxSelectedItem = ParamTypeList.DefaultItem;

	private string _SearchWord = "";

	private DelegateCommand<object> _CheckCommand = null;

	private DelegateCommand<string> _TextChangedCommand = null;

	private ICommand _DelAllCommand = null;

	private ICommand _UpCommand = null;

	private ICommand _DownCommand = null;

	private ICommand _UpGroupCommand = null;

	private ICommand _DownGroupCommand = null;

	List<string> IParamList.ParamList
	{
		get
		{
			if (_ParamList == null)
			{
				_ParamList = new List<string>();
				foreach (RunParamConfigDecorate item in Collection)
				{
					if (!item.IsForGroup)
					{
						_ParamList.Add(item.UniquePath);
					}
				}
			}
			return _ParamList;
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			return _EnumEntrysA;
		}
		set
		{
			_EnumEntrysA = value;
			((NotifyPropertyChangedBase)this).OnPropertyChanged("EnumEntrysA");
		}
	}

	public IList SelectedType { get; } = ParamTypeList.SelectableList;

	public GroupRunParamSelectableTypeItem ComboBoxSelectedItem
	{
		get
		{
			return _ComboBoxSelectedItem;
		}
		set
		{
			if (_ComboBoxSelectedItem != value)
			{
				_ComboBoxSelectedItem = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("ComboBoxSelectedItem");
				AsyncLoadParams();
			}
		}
	}

	public string SearchWord
	{
		get
		{
			return _SearchWord;
		}
		set
		{
			if (_SearchWord != value)
			{
				_SearchWord = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("SearchWord");
				AsyncLoadParams();
			}
		}
	}

	public ICommand CheckCommand => (ICommand)(_CheckCommand ?? (_CheckCommand = new DelegateCommand<object>((Action<object>)CheckCommandExecute)));

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedCommandExecute)));

	public ICommand DelAllCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _DelAllCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)DelAllCommandExecute);
				ICommand command2 = (ICommand)val;
				_DelAllCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand UpCommand => _UpCommand ?? (_UpCommand = (ICommand)new DelegateCommand<object>((Action<object>)UpCommandExecute, (Func<object, bool>)CanUpCommandExecute, false));

	public ICommand DownCommand => _DownCommand ?? (_DownCommand = (ICommand)new DelegateCommand<object>((Action<object>)DownCommandExecute, (Func<object, bool>)CanDownCommandExecute, false));

	public ICommand UpGroupCommand => _UpGroupCommand ?? (_UpGroupCommand = (ICommand)new DelegateCommand<object>((Action<object>)UpGroupCommandExecute, (Func<object, bool>)CanUpGroupCommandExecute, false));

	public ICommand DownGroupCommand => _DownGroupCommand ?? (_DownGroupCommand = (ICommand)new DelegateCommand<object>((Action<object>)DownGroupCommandExecute, (Func<object, bool>)CanDownGroupCommandExecute, false));

	public Func<object, bool> CheckClosing => CheckClosingMethod;

	public void AsyncLoadParams()
	{
		Task.Run(() =>
		{
			RelateControl?.ShowProgressDialog("正在加载数据");
			_ParamList = null;
			RunParamPickData runParamPickData = new RunParamPickData();
			runParamPickData.Node = Owner;
			runParamPickData.ParamList = this;
			runParamPickData.FilterTypes = ComboBoxSelectedItem.TypeList;
			runParamPickData.SearchWord = SearchWord;
			runParamPickData.Init();
			int total = runParamPickData.EnumEntrysA.Count;
			int count = 0;
			int percent = 0;
			ExpendHolder.CheckedParams = new List<string>();
			runParamPickData.EnumEntrysA.OfType<ExpendHolder>().ToList().ForEach((ExpendHolder p) =>
			{
				ObservableCollection<object> enumEntrysA = p.EnumEntrysA;
				count++;
				percent = 10 + (int)((float)count / (float)total * 90f);
				RelateControl?.SetProgressValue(percent);
			});
			ExpendHolder.CheckedParams = null;
			EnumEntrysA = runParamPickData.EnumEntrysA;
			RelateControl?.CloseProgressDialog();
		});
	}

	protected void CheckCommandExecute(object source)
	{
		if (!(source is Holder { CheckEnabled: var checkEnabled } holder))
		{
			return;
		}
		if (checkEnabled.Value)
		{
			if (holder.HolderTarget is VmModule)
			{
				AddModule(holder);
			}
			else
			{
				AddParam(holder);
			}
		}
		else if (holder.HolderTarget is VmModule)
		{
			DeleteModule(holder);
		}
		else
		{
			DeleteParam(holder);
		}
	}

	private bool AddParam(Holder holder)
	{
		bool flag = false;
		if (holder != null && holder.HolderTarget is NameValueItem source)
		{
			flag = AddParam(source);
			if (!flag)
			{
				holder.CheckEnabled = false;
			}
		}
		return flag;
	}

	private bool AddModule(Holder holder)
	{
		if (holder == null)
		{
			return false;
		}
		bool flag = true;
		foreach (Holder item in holder.EnumEntrysA)
		{
			if (!flag)
			{
				item.CheckEnabled = false;
			}
			else
			{
				flag = AddParam(item);
			}
		}
		return flag;
	}

	private bool? DeleteParam(Holder holder)
	{
		if (holder == null)
		{
			return false;
		}
		bool? result = null;
		NameValueItem item = holder.HolderTarget as NameValueItem;
		if (item != null)
		{
			RunParamConfigDecorate runParamConfigDecorate = Collection.FirstOrDefault((RunParamConfigDecorate p) => !p.IsForGroup && ParamConfigDataBase.CheckParam(item, p.UniquePath));
			if (runParamConfigDecorate != null)
			{
				result = DeleteParam(runParamConfigDecorate);
				if (result.Value && !(item.ParamsSource is VmModuleSet) && item.ParamsSource is VmModule vmModule)
				{
					vmModule.UserData[VmModuleSet.HeadPathPre + item.DisplayName] = null;
				}
			}
		}
		if (result.HasValue && !result.Value)
		{
			holder.CheckEnabled = true;
		}
		return result;
	}

	private void DeleteModule(Holder holder)
	{
		if (holder == null)
		{
			return;
		}
		foreach (Holder item in holder.EnumEntrysA)
		{
			DeleteParam(item);
		}
	}

	private void TextChangedCommandExecute(string searchWord)
	{
		SearchWord = searchWord;
	}

	private void DelAllCommandExecute()
	{
		DeleteAllParams();
	}

	private bool CanUpCommandExecute(object data)
	{
		RunParamConfigDecorate runParamConfigDecorate = data as RunParamConfigDecorate;
		return runParamConfigDecorate.GroupName.Collection.IndexOf(runParamConfigDecorate.DecorateSource) > 0;
	}

	private void UpCommandExecute(object data)
	{
		UpDownParam(data, isUp: true);
	}

	private bool CanDownCommandExecute(object data)
	{
		RunParamConfigDecorate runParamConfigDecorate = data as RunParamConfigDecorate;
		return runParamConfigDecorate.GroupName.Collection.IndexOf(runParamConfigDecorate.DecorateSource) < runParamConfigDecorate.GroupName.Collection.Count - 1;
	}

	private void DownCommandExecute(object data)
	{
		UpDownParam(data, isUp: false);
	}

	private bool CanUpGroupCommandExecute(object data)
	{
		DynamicNameValueItemOfGroupCategory item = data as DynamicNameValueItemOfGroupCategory;
		if (Parent.GroupCollection == null)
		{
			return false;
		}
		return Parent.GroupCollection.IndexOf(item) > 0;
	}

	private void UpGroupCommandExecute(object data)
	{
		UpGroup(data);
	}

	private bool CanDownGroupCommandExecute(object data)
	{
		DynamicNameValueItemOfGroupCategory item = data as DynamicNameValueItemOfGroupCategory;
		if (Parent.GroupCollection == null)
		{
			return false;
		}
		return Parent.GroupCollection.IndexOf(item) < Parent.GroupCollection.Count - 1;
	}

	private void DownGroupCommandExecute(object data)
	{
		DownGroup(data);
	}

	private bool CheckClosingMethod(object data)
	{
		if (data is ModulesPickData modulesPickData)
		{
			ObservableCollection<object> observableCollection = ((modulesPickData.EnumEntrysA.Count == 0) ? null : (modulesPickData.EnumEntrysA[0] as Holder).EnumEntrysA);
			if (observableCollection != null)
			{
				bool flag = observableCollection.OfType<Holder>().Count((Holder p) => p.CheckEnabled.Value) > 128;
				if (flag)
				{
					HikMessageBox.Show(string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_ExceedConfigNumLimit"), 128), MessageIcons.Warning);
				}
				return !flag;
			}
		}
		return true;
	}
}
