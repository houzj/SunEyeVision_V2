using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class Holder : IOutputTree, IPropertyChanged, INotifyPropertyChanged
{
	public static string NullName = "RunParam_None";

	private string _DisplayName;

	private object _HolderTarget;

	private IOutputTree _parentTree;

	protected ObservableCollection<object> EnumEntrys = new ObservableCollection<object>();

	private bool outputEnabled;

	private bool? checkEnabled = false;

	public string DisplayNameA
	{
		get
		{
			return _DisplayName;
		}
		set
		{
			_DisplayName = value;
		}
	}

	public string Name => DisplayNameA;

	public object HolderTarget
	{
		get
		{
			return _HolderTarget;
		}
		set
		{
			_HolderTarget = value;
		}
	}

	public IOutputTree ParentTree
	{
		get
		{
			return _parentTree;
		}
		set
		{
			_parentTree = value;
		}
	}

	public virtual ObservableCollection<object> EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
		}
	}

	public bool OutputEnabled
	{
		get
		{
			if (EnumEntrysA.Count == 0)
			{
				return ParentTree != null && outputEnabled;
			}
			return EnumEntrysA.Any((object p) => p is IOutputTree outputTree && outputTree.OutputEnabled);
		}
		set
		{
			if (EnumEntrysA.Count == 0)
			{
				outputEnabled = value;
				OnPropertyChanged("OutputEnabled");
				OnPropertyChangedParentTree("OutputEnabled");
			}
			foreach (IOutputTree item in EnumEntrysA.OfType<IOutputTree>())
			{
				if (item is DynamicIO && (DynamicIO.overRun & value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetCnfg"), AppLocalizationService.GetLang("超限"), MessageIcons.Error);
					break;
				}
				if (item is Holder { EnumEntrysA: var enumEntrysA } && enumEntrysA.FirstOrDefault((object p) => p is DynamicIO) != null && (DynamicIO.overRun & value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetCnfg"), AppLocalizationService.GetLang("超限"), MessageIcons.Error);
					break;
				}
				item.OutputEnabled = value;
				OnPropertyChanged("OutputEnabled");
				OnPropertyChangedParentTree("OutputEnabled");
			}
		}
	}

	public bool? CheckEnabled
	{
		get
		{
			if (EnumEntrysA.Count == 0)
			{
				return (ParentTree == null) ? new bool?(false) : checkEnabled;
			}
			if (EnumEntrysA.All((object p) => p is IOutputTree { CheckEnabled: var flag } && flag == false))
			{
				return false;
			}
			if (EnumEntrysA.All((object p) => p is IOutputTree { CheckEnabled: var flag } && flag == true))
			{
				return true;
			}
			return null;
		}
		set
		{
			if (EnumEntrysA.Count == 0)
			{
				checkEnabled = value;
				OutputEnabled = value.HasValue && value.Value;
				OnPropertyChanged("CheckEnabled");
				OnPropertyChangedParentTree("CheckEnabled");
			}
			foreach (IOutputTree item in EnumEntrysA.OfType<IOutputTree>())
			{
				if (item is DynamicIO)
				{
					if (!DynamicIO.overRun || object.Equals(false, value))
					{
						item.OutputEnabled = value.HasValue && value.Value;
						OnPropertyChanged("CheckEnabled");
						OnPropertyChangedParentTree("CheckEnabled");
					}
					else if (DynamicIO.overRun)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetCnfg"), AppLocalizationService.GetLang("超限"), MessageIcons.Error);
						break;
					}
				}
				else
				{
					item.CheckEnabled = value;
					item.OutputEnabled = value.HasValue && value.Value;
					OnPropertyChanged("CheckEnabled");
					OnPropertyChangedParentTree("CheckEnabled");
				}
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public void OnPropertyChangedParentTree(string name)
	{
		ParentTree?.OnPropertyChanged(name);
		ParentTree?.OnPropertyChangedParentTree(name);
	}

	public bool OutputNeedAdd(ObservableCollection<object> collection)
	{
		foreach (object item in collection)
		{
			if (!(item as IOutputTree).OutputEnabled)
			{
				return false;
			}
			if (item is Holder && !OutputNeedAdd((item as Holder).EnumEntrysA))
			{
				return false;
			}
		}
		return true;
	}
}
