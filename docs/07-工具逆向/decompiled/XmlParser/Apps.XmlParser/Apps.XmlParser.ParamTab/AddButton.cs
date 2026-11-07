using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Xml;
using Apps.Localization;
using Hik.Xml;
using VM.Controls;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class AddButton : NameValueItem
{
	public int MaxCount;

	public string RelateItems;

	public string RelateValues;

	private object CurValue;

	public ObservableCollection<object> EnumEntrys;

	private List<string> RelateItemList;

	private string[] RelateValueList;

	private DelegateCommand _ClickCommand = null;

	private object HeaderItem;

	private List<BaseNode> RelateNode;

	public object CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public ObservableCollection<object> EnumEntrysA
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
			OnPropertyChanged("EnumEntrysA");
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand(ClickCommandCmdExecute);
			}
			return _ClickCommand;
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (!string.IsNullOrEmpty(RelateItems))
		{
			RelateItems = RelateItems.Replace("\r", "");
			RelateItems = RelateItems.Replace("\n", "");
			RelateItems = RelateItems.Replace("\t", "");
			RelateItems = RelateItems.Replace(" ", "");
			RelateItemList = RelateItems.Split(',').ToList();
		}
		if (!string.IsNullOrEmpty(RelateValues))
		{
			RelateValues = RelateValues.Replace("\r", "");
			RelateValues = RelateValues.Replace("\n", "");
			RelateValues = RelateValues.Replace("\t", "");
			RelateValues = RelateValues.Replace(" ", "");
			RelateValueList = RelateValues.Split(',');
		}
		return this;
	}

	private void ClickCommandCmdExecute()
	{
		if (RelateNode == null)
		{
			RelateNode = new List<BaseNode>();
			if (GetParentNode(2u) is Tab tab)
			{
				foreach (string relateItem in RelateItemList)
				{
					BaseNode baseNode = tab.FindBaseNodeByName(relateItem);
					if (baseNode != null)
					{
						RelateNode.Add(baseNode);
					}
				}
			}
		}
		int curCount = GetCurCount();
		if (curCount == 0)
		{
			HeaderItem = null;
		}
		if (curCount >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			if (!(CurValue is EnumEntry) || RelateItemList == null)
			{
				return;
			}
			foreach (BaseNode item in RelateNode)
			{
				if (item is NameValueItem && !(item as NameValueItem).CustomVisibleA && RelateValueList[RelateItemList.IndexOf((item as NameValueItem).Name)] == (CurValue as EnumEntry).StrValue)
				{
					Category category = GetParentNode() as Category;
					category.ItemsA.Remove(item);
					category.ItemsA.Insert(category.ItemsA.IndexOf(this), item);
					if (GetParentNode(2u) is Tab tab2)
					{
						tab2.Collection.Remove(item);
						tab2.Collection.Insert(tab2.Collection.IndexOf(this), item);
					}
					(item as NameValueItem).CustomVisibleA = true;
					break;
				}
			}
		}
	}

	private int GetCurCount()
	{
		int num = 0;
		foreach (BaseNode item in RelateNode)
		{
			if (item is NameValueItem && (item as NameValueItem).CustomVisibleA)
			{
				num++;
			}
		}
		return num;
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
