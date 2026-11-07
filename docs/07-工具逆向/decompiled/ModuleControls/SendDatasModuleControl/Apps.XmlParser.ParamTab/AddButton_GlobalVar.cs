using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Xml;
using Apps.Localization;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class AddButton_GlobalVar : NameValueItem
{
	private List<string> RelatedItemsName;

	private List<GlobalVarItemBase> globalVarItemSource;

	public int MaxCount;

	public string RelatedItems;

	private DelegateCommand _ClickCommand = null;

	public override string DisplayType => "AddButton_QueueItem";

	public List<GlobalVarItemBase> GlobalVarItemSource
	{
		get
		{
			if (globalVarItemSource == null)
			{
				globalVarItemSource = new List<GlobalVarItemBase>();
				if (GetParentNode(2u) is Tab tab)
				{
					foreach (string item in RelatedItemsName)
					{
						BaseNode baseNode = tab.FindBaseNodeByName(item);
						if (baseNode != null)
						{
							globalVarItemSource.Add(baseNode as GlobalVarItemBase);
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
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
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
			AddGlobalVarItem(curCount);
		}
	}

	private void AddGlobalVarItem(int curcount)
	{
		GlobalVarItemBase globalVarItemBase = GlobalVarItemSource.FirstOrDefault((GlobalVarItemBase p) => !p.CustomVisibleA && !p.IsAddedA);
		if (globalVarItemBase != null)
		{
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(globalVarItemBase);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), globalVarItemBase);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(globalVarItemBase);
				tab.Collection.Insert(tab.Collection.IndexOf(this), globalVarItemBase);
			}
			globalVarItemBase.IsAddedA = true;
			globalVarItemBase.CustomVisibleA = true;
			(globalVarItemBase as GlobalVarItemIn).DisplayIndexA = curcount + 1;
			UpdateModuleStatus();
		}
	}

	private void UpdateModuleStatus()
	{
		GetVmModule()?.OnPropertyChanged("IsReady");
	}

	private int GetCurCount()
	{
		return GlobalVarItemSource.FindAll((GlobalVarItemBase p) => p.IsAddedA).Count;
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
