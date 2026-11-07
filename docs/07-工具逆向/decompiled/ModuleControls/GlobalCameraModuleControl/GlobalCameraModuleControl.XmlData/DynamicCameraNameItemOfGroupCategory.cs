using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Xml;
using Apps.Data;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;
using VM.Controls;
using VM.PlatformSDKCS;

namespace GlobalCameraModuleControl.XmlData;

public class DynamicCameraNameItemOfGroupCategory : NotifyPropertyChangedBase
{
	public ICameraDynamicParam Parent;

	public ObservableCollection<DynamicCameraNameItemOfGroup> collection = new ObservableCollection<DynamicCameraNameItemOfGroup>();

	private string _Name;

	private bool isSelected;

	private bool? checkEnabled = false;

	private bool isExpanded = true;

	private DelegateCommand<string> _TextChangedCommand = null;

	private DelegateCommand _DelGroupCommand = null;

	public ObservableCollection<DynamicCameraNameItemOfGroup> Collection
	{
		get
		{
			return collection;
		}
		set
		{
			if (collection != value)
			{
				collection = value;
				OnPropertyChanged("Collection");
			}
		}
	}

	public string Name
	{
		get
		{
			return _Name;
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NameEmpty"));
			}
			else if (_Name != value)
			{
				_Name = value;
				OnPropertyChanged("Name");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			if (isSelected != value)
			{
				isSelected = value;
				OnPropertyChanged("IsSelected");
			}
		}
	}

	public bool? CheckEnabled
	{
		get
		{
			int num = Collection.Where((DynamicCameraNameItemOfGroup x) => x.CheckEnabled == true).Count();
			if (Enumerable.Count(Collection) != 0)
			{
				if (num == 0)
				{
					checkEnabled = false;
				}
				else if (num < Enumerable.Count(Collection))
				{
					checkEnabled = null;
				}
				else
				{
					checkEnabled = true;
				}
			}
			return checkEnabled;
		}
		set
		{
			if (checkEnabled == value)
			{
				return;
			}
			checkEnabled = value;
			foreach (DynamicCameraNameItemOfGroup item in Collection)
			{
				item.CheckEnabled = value;
			}
			OnPropertyChanged("CheckEnabled");
		}
	}

	public bool IsExpanded
	{
		get
		{
			return isExpanded;
		}
		set
		{
			if (isExpanded != value)
			{
				isExpanded = value;
				OnPropertyChanged("IsExpanded");
			}
		}
	}

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedExecute)));

	public ICommand DelGroupCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _DelGroupCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)DelGroupCommandExecute);
				DelegateCommand val3 = val2;
				_DelGroupCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public override string ToString()
	{
		return Name;
	}

	private void TextChangedExecute(string newName)
	{
		Name = newName;
		foreach (DynamicCameraNameItemOfGroup item in Collection)
		{
			item.TreeGroupName = newName;
			try
			{
				item.Owner.Params["ModifyParamGroupName"] = item.NameA + ";" + newName;
			}
			catch
			{
			}
		}
	}

	public void DelGroupCommandExecute()
	{
		DialogResult dialogResult = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConfirmDeleteGroup"), MessageIcons.Question);
		if (dialogResult != DialogResult.Ok)
		{
			return;
		}
		XmlDocument xmlDocument = new XmlDocument();
		if (Parent.Owner.UserData["MvsConfig"] != null)
		{
			string text = StringUtil.UTF8GetString(Parent.Owner.UserData["MvsConfig"]);
			if (!string.IsNullOrEmpty(text))
			{
				xmlDocument.LoadXml(text);
			}
			foreach (DynamicCameraNameItemOfGroup item in Collection)
			{
				try
				{
					item.Owner.Params["DeleteNodeName"] = item.NameA;
					XmlNode xmlNode = xmlDocument.SelectSingleNode("Favorites");
					if (xmlNode != null)
					{
						XmlNodeList childNodes = xmlNode.ChildNodes;
						foreach (XmlNode item2 in childNodes)
						{
							if (item2.SelectSingleNode("Name")?.InnerText == item.NameA)
							{
								xmlNode.RemoveChild(item2);
								break;
							}
						}
					}
				}
				catch
				{
				}
				Parent.Collections.Remove(item);
			}
			xmlDocument.Save("C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml");
			Parent.Owner.UserData["MvsConfig"] = StringUtil.UTF8GetBytes(xmlDocument.InnerXml);
		}
		Parent.GroupCollection.Remove(this);
	}
}
