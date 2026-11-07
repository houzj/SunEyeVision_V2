using Apps.Localization;

namespace Apps.XmlParser.ParamTab;

public class EnumerationGEx : EnumerationG
{
	public override string DisplayNameA
	{
		get
		{
			return $"%{AppLocalizationService.GetLang(DisplayName)}{IndexA}%";
		}
		set
		{
			if (DisplayName != value)
			{
				DisplayName = value;
				OnPropertyChanged("DisplayNameA");
			}
		}
	}

	public string IndexA { get; set; }
}
