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

public class AddButton_PointSet : NameValueItem
{
	private List<string> RelatedItemsName;

	private List<PointSet> pointSetSource;

	public int MaxCount;

	public string RelatedItems;

	private DelegateCommand<object> _ClickCommand = null;

	public List<PointSet> PointSetSource
	{
		get
		{
			if (pointSetSource == null)
			{
				pointSetSource = new List<PointSet>();
				if (GetParentNode(2u) is Tab tab)
				{
					foreach (string item in RelatedItemsName)
					{
						BaseNode baseNode = tab.FindBaseNodeByName(item);
						if (baseNode != null)
						{
							pointSetSource.Add(baseNode as PointSet);
						}
					}
				}
			}
			return pointSetSource;
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand<object>((Action<object>)ClickCommandCmdExecute);
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

	private void ClickCommandCmdExecute(object e)
	{
		int curCount = GetCurCount();
		if (curCount >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		int addType = Convert.ToInt32(e);
		AddPoint(addType);
	}

	private void AddPoint(int addType)
	{
		PointSet pointSet = PointSetSource.FirstOrDefault((PointSet p) => !p.CustomVisibleA);
		if (pointSet != null)
		{
			pointSet.CurValueA = null;
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(pointSet);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), pointSet);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(pointSet);
				tab.Collection.Insert(tab.Collection.IndexOf(this), pointSet);
			}
			switch (addType)
			{
			case 0:
				pointSet.IsPointA = true;
				pointSet.IsPointCoordinateA = false;
				break;
			case 1:
				pointSet.IsPointA = false;
				pointSet.IsPointCoordinateA = true;
				break;
			}
			pointSet.CurValueA = null;
			if (pointSet.XCoordButtonSelecter == null)
			{
				pointSet.XCoordButtonSelecterA = new ButtonSelecter(this, "float", "PointX" + (GetCurCount() + 1));
				pointSet.XCoordButtonSelecterA.AccessModeA = "O";
				pointSet.XCoordButtonSelecterA.ReadOnlyA = false;
				pointSet.XCoordButtonSelecterA.SetParentNode(pointSet);
			}
			if (pointSet.YCoordButtonSelecterA == null)
			{
				pointSet.YCoordButtonSelecterA = new ButtonSelecter(this, "float", "PointY" + (GetCurCount() + 1));
				pointSet.YCoordButtonSelecterA.AccessModeA = "O";
				pointSet.YCoordButtonSelecterA.ReadOnlyA = false;
				pointSet.YCoordButtonSelecterA.SetParentNode(pointSet);
			}
			pointSet.XCoordButtonSelecterA.CurValueA = null;
			pointSet.YCoordButtonSelecterA.CurValueA = null;
			pointSet.CustomVisibleA = true;
		}
	}

	private int GetCurCount()
	{
		return PointSetSource.FindAll((PointSet p) => p.CustomVisibleA).Count;
	}

	public override int GetParamUnit()
	{
		foreach (PointSet item in PointSetSource)
		{
			if (!item.CustomVisible)
			{
			}
		}
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
