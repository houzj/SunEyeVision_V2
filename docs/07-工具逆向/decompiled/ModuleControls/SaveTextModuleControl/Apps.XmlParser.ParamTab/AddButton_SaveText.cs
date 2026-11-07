using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Xml;
using Apps.Localization;
using Hik.Xml;
using VM.Controls;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class AddButton_SaveText : NameValueItem
{
	private List<string> RelatedItemsName;

	private List<SaveTextItem> globalVarItemSource;

	public int MaxCount;

	public string RelatedItems;

	private DelegateCommand _ClickCommand = null;

	public override string DisplayType => "AddButton_SaveText";

	public List<SaveTextItem> GlobalVarItemSource
	{
		get
		{
			if (globalVarItemSource == null)
			{
				globalVarItemSource = new List<SaveTextItem>();
				if (GetParentNode(2u) is Tab tab)
				{
					foreach (string item in RelatedItemsName)
					{
						BaseNode baseNode = tab.FindBaseNodeByName(item);
						if (baseNode != null)
						{
							globalVarItemSource.Add(baseNode as SaveTextItem);
						}
					}
				}
			}
			return globalVarItemSource;
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
		if (!string.IsNullOrEmpty(RelatedItems))
		{
			RelatedItems = RelatedItems.Replace("\r", "");
			RelatedItems = RelatedItems.Replace("\n", "");
			RelatedItems = RelatedItems.Replace("\t", "");
			RelatedItems = RelatedItems.Replace(" ", "");
			RelatedItemsName = RelatedItems.Split(',').ToList();
		}
		return this;
	}

	private void ClickCommandCmdExecute()
	{
		int curCount = GetCurCount();
		if (curCount >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			AddGlobalVarItem();
		}
	}

	private void AddGlobalVarItem()
	{
		SaveTextItem saveTextItem = GlobalVarItemSource.FirstOrDefault((SaveTextItem p) => !p.CustomVisibleA && !p.IsAddedA);
		if (saveTextItem != null)
		{
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(saveTextItem);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), saveTextItem);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(saveTextItem);
				tab.Collection.Insert(tab.Collection.IndexOf(this), saveTextItem);
			}
			saveTextItem.IsAddedA = true;
			saveTextItem.CustomVisibleA = true;
		}
	}

	public int GetCurCount()
	{
		return GlobalVarItemSource.FindAll((SaveTextItem p) => p.IsAddedA).Count;
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
