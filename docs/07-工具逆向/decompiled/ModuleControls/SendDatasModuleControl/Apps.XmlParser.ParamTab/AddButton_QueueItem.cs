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

public class AddButton_QueueItem : NameValueItem
{
	private List<string> RelatedItemsName;

	private List<QueueItemBase> queueItemSource;

	public int MaxCount;

	public string RelatedItems;

	private DelegateCommand _ClickCommand = null;

	public List<QueueItemBase> QueueItemSource
	{
		get
		{
			if (queueItemSource == null)
			{
				queueItemSource = new List<QueueItemBase>();
				if (GetParentNode(2u) is Tab tab)
				{
					foreach (string item in RelatedItemsName)
					{
						BaseNode baseNode = tab.FindBaseNodeByName(item);
						if (baseNode != null)
						{
							queueItemSource.Add(baseNode as QueueItemBase);
						}
					}
				}
			}
			return queueItemSource;
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
			return;
		}
		QueueItemBase queueItemBase = QueueItemSource.FirstOrDefault((QueueItemBase p) => !p.CustomVisibleA && !p.IsAddedA);
		if (queueItemBase != null)
		{
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(queueItemBase);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), queueItemBase);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(queueItemBase);
				tab.Collection.Insert(tab.Collection.IndexOf(this), queueItemBase);
			}
			queueItemBase.IsAddedA = true;
			queueItemBase.CustomVisibleA = true;
			queueItemBase.DisplayIndexA = curCount + 1;
			GetVmModule()?.OnPropertyChanged("IsReady");
		}
	}

	private void AddQueueItem()
	{
		QueueItemBase queueItemBase = QueueItemSource.FirstOrDefault((QueueItemBase p) => !p.CustomVisibleA && !p.IsAddedA);
		if (queueItemBase != null)
		{
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(queueItemBase);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), queueItemBase);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(queueItemBase);
				tab.Collection.Insert(tab.Collection.IndexOf(this), queueItemBase);
			}
			queueItemBase.IsAddedA = true;
			queueItemBase.CustomVisibleA = true;
		}
	}

	private int GetCurCount()
	{
		return QueueItemSource.FindAll((QueueItemBase p) => p.IsAddedA).Count;
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
