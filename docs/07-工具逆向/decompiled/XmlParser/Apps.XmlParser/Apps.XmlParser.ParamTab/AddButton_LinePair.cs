using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class AddButton_LinePair : NameValueItem
{
	private List<LinePair> linePairSource;

	public int MaxCount;

	private DelegateCommand _ClickCommand = null;

	public List<LinePair> LinePairSource
	{
		get
		{
			if (linePairSource == null)
			{
				if (!(GetParentNode() is Category category))
				{
					return null;
				}
				linePairSource = category.ItemsA.OfType<LinePair>().ToList();
			}
			return linePairSource;
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
			AddLinePair();
		}
	}

	private void AddLinePair()
	{
		if (LinePairSource == null)
		{
			return;
		}
		LinePair linePair = LinePairSource.FirstOrDefault((LinePair p) => !p.CustomVisibleA);
		if (linePair != null)
		{
			Category category = GetParentNode() as Category;
			category.ItemsA.Remove(linePair);
			category.ItemsA.Insert(category.ItemsA.IndexOf(this), linePair);
			if (GetParentNode(2u) is Tab tab)
			{
				tab.Collection.Remove(linePair);
				tab.Collection.Insert(tab.Collection.IndexOf(this), linePair);
			}
			linePair.CustomVisibleA = true;
		}
	}

	private int GetCurCount()
	{
		if (LinePairSource == null)
		{
			return 0;
		}
		return LinePairSource.FindAll((LinePair p) => p.CustomVisibleA).Count;
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
