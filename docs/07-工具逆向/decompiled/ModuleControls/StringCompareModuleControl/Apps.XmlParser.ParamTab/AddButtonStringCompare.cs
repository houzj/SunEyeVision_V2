using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Xml;
using Apps.Localization;
using Hik.Xml;
using VM.Controls;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class AddButtonStringCompare : NameValueItem
{
	private DelegateCommand _clickCommand;

	private object _headerItem;

	public int Index = 1;

	public ICommand ClickCommand => _clickCommand ?? (_clickCommand = new DelegateCommand(ClickCommandCmdExecute));

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		return this;
	}

	private void ClickCommandCmdExecute()
	{
		if (!(GetParentNode() is Category { ItemsA: var itemsA }))
		{
			return;
		}
		List<StringCompare> source = itemsA.Where((object p) => p is StringCompare).Cast<StringCompare>().ToList();
		StringCompare stringCompare = source.FirstOrDefault((StringCompare p) => !p.CustomVisibleA);
		if (stringCompare == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + Enumerable.Count(source), MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		IEnumerable<StringCompare> enumerable = source.Where((StringCompare p) => p.CustomVisibleA);
		if (enumerable == null)
		{
			return;
		}
		if (enumerable.Count() > 0)
		{
			stringCompare.RealIndex = (Convert.ToInt32(enumerable.Max((StringCompare p) => Convert.ToInt32(p.RealIndex))) + 1).ToString();
		}
		else
		{
			stringCompare.RealIndex = "1";
		}
		stringCompare.CustomVisibleA = true;
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
