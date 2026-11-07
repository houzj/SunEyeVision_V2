using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class AddButton_PointPair : NameValueItem
{
	private List<PointPair> pointPairSource;

	public int MaxCount;

	private DelegateCommand _ClickCommand = null;

	public List<PointPair> PointPairSource
	{
		get
		{
			if (pointPairSource == null)
			{
				if (!(GetParentNode() is Category category))
				{
					return null;
				}
				pointPairSource = category.ItemsA.OfType<PointPair>().ToList();
			}
			return pointPairSource;
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

	private void ClickCommandCmdExecute()
	{
		int curCount = GetCurCount();
		if (curCount >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			AddPointPair();
		}
	}

	private void AddPointPair()
	{
		if (PointPairSource == null)
		{
			return;
		}
		PointPair pointPair = PointPairSource.FirstOrDefault((PointPair p) => !p.CustomVisibleA);
		if (pointPair != null)
		{
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(pointPair);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), pointPair);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(pointPair);
				tab.Collection.Insert(tab.Collection.IndexOf(this), pointPair);
			}
			pointPair.CustomVisibleA = true;
		}
	}

	private int GetCurCount()
	{
		if (PointPairSource == null)
		{
			return 0;
		}
		return PointPairSource.FindAll((PointPair p) => p.CustomVisibleA).Count;
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
