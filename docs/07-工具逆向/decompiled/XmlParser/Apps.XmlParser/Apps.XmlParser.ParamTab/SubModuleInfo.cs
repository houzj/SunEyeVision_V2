using System;
using System.Windows.Input;
using System.Xml;
using Apps.Localization;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class SubModuleInfo : NameValueItem
{
	[XmlMark(true)]
	public new string Name;

	public string TypeName;

	public string ItemName;

	public SubAlgorithmTabRoot AlgorithmRoot;

	public SubParamRoot ModuleParamRoot;

	private DelegateCommand<object> _ClickCommand = null;

	public new string NameA => Name;

	public string TypeNameA
	{
		get
		{
			if (IsHasIndex)
			{
				return "%" + AppLocalizationService.GetLang(TypeName) + IndexA + "%";
			}
			return TypeName;
		}
	}

	public string ItemNameA => AppLocalizationService.GetLang(ItemName);

	public SubAlgorithmTabRoot AlgorithmItems => AlgorithmRoot;

	public SubParamRoot ModuleParams => ModuleParamRoot;

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

	public bool IsHasIndex { get; set; } = false;

	public string IndexA { get; set; }

	public string HashCode { get; set; }

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		return this;
	}

	private void ClickCommandCmdExecute(object obj)
	{
		if (GetParentNode() is AddList addList && obj is string)
		{
			int num = 32;
			if (addList.EnumEntrysA.Count >= num)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + num, MessageButtons.Normal, MessageIcons.Warning);
			}
			else
			{
				addList.DoAdd((string)obj);
			}
		}
	}
}
