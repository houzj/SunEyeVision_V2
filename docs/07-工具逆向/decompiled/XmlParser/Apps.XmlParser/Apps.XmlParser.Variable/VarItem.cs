using System.Collections.Generic;
using Apps.Localization;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.Variable;

public class VarItem : IVariable
{
	public object Owner { get; set; }

	public int Index { get; set; }

	public string Name { get; set; }

	public string Type { get; set; }

	public string RemarkInfo { get; set; }

	public string Value { get; set; }

	public object ValueArray { get; set; }

	public List<VarItem> SubIOInfos { get; set; }

	public string GroupInfo { get; set; }

	public bool IsCommInit { get; set; }

	public bool IsCombination { get; set; }

	public string DisplayName
	{
		get
		{
			if (Owner is VmModule vmModule)
			{
				return (vmModule.ID == 13000) ? (AppLocalizationService.GetLang("RunParam_Global") + "." + AppLocalizationService.GetLang(Name)) : (AppLocalizationService.GetLang("RunParam_Local") + "." + AppLocalizationService.GetLang(Name));
			}
			return "";
		}
	}

	public string Guid { get; set; }
}
