using System;
using System.Windows.Input;
using System.Xml;
using Apps.Localization;
using GlobalCameraModuleControl;
using GlobalCameraModuleControl.XmlData;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class DynamicCameraNameItemOfGroup : NameValueItem
{
	public ICameraDynamicParam Parent;

	private VmModule owner;

	private string _displayName;

	private DynamicCameraNameItemOfGroupCategory _Group;

	private bool? checkEnabled = false;

	private DelegateCommand<string> _TextChangedCommand = null;

	private DelegateCommand _DelGroupCommand = null;

	public NameValueItem Source { get; set; }

	internal VmModule Owner
	{
		get
		{
			if (owner == null)
			{
				owner = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
			}
			return owner;
		}
	}

	public override string DisplayNameA
	{
		get
		{
			_displayName = Source.DisplayNameA;
			return _displayName;
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NameEmpty"));
			}
			else if (_displayName != value)
			{
				_displayName = value;
				Source.DisplayNameA = value;
				Source.DescriptionA = value;
				OnPropertyChanged("DisplayNameA");
			}
		}
	}

	public override string DescriptionA
	{
		get
		{
			return Source?.DescriptionA ?? "";
		}
		set
		{
			if (Source != null && Source.Description != value)
			{
				Source.DescriptionA = value;
				OnPropertyChanged("DescriptionA");
			}
		}
	}

	public override string NameA
	{
		get
		{
			Name = Source.Name;
			return Name;
		}
		set
		{
			if (Name != value)
			{
				Name = value;
				Source.Name = value;
				OnPropertyChanged("NameA");
			}
		}
	}

	public DynamicCameraNameItemOfGroupCategory Group
	{
		get
		{
			return _Group;
		}
		set
		{
			if (_Group != value)
			{
				_Group = value;
				OnPropertyChanged("Group");
			}
		}
	}

	public bool? CheckEnabled
	{
		get
		{
			return checkEnabled;
		}
		set
		{
			if (checkEnabled != value)
			{
				checkEnabled = value;
				OnPropertyChanged("CheckEnabled");
				Group.OnPropertyChanged("CheckEnabled");
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

	public int Index { get; set; }

	public string TreeGroupName { get; set; }

	private void TextChangedExecute(string newName)
	{
		DisplayNameA = newName;
		try
		{
			Owner.Params["ModifyParamDisplayName"] = NameA + ";" + newName;
		}
		catch
		{
		}
	}

	public void DelGroupCommandExecute()
	{
		XmlDocument xmlDocument = new XmlDocument();
		if (Owner.UserData["MvsConfig"] != null)
		{
			string text = StringUtil.UTF8GetString(Owner.UserData["MvsConfig"]);
			if (!string.IsNullOrEmpty(text))
			{
				xmlDocument.LoadXml(text);
			}
		}
		try
		{
			Owner.Params["DeleteNodeName"] = NameA;
			XmlNode xmlNode = xmlDocument.SelectSingleNode("Favorites");
			if (xmlNode == null)
			{
				return;
			}
			XmlNodeList childNodes = xmlNode.ChildNodes;
			string empty = string.Empty;
			foreach (XmlNode item in childNodes)
			{
				if (item.SelectSingleNode("Name").InnerText == NameA)
				{
					xmlNode.RemoveChild(item);
					break;
				}
			}
			xmlDocument.Save("C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml");
			Owner.UserData["MvsConfig"] = StringUtil.UTF8GetBytes(xmlDocument.InnerXml);
		}
		catch
		{
		}
		Group.Collection.Remove(this);
		Group.OnPropertyChanged("CheckEnabled");
		Group = null;
		Parent.Collections.Remove(this);
	}

	public override int GetParamUnit()
	{
		return Source.GetParamUnit();
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
