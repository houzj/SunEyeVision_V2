using System.Collections.ObjectModel;

namespace Apps.XmlParser.ParamTab;

public class PixelModuRangeConfigItem : IntegerBettween
{
	public override string DisplayNameA
	{
		get
		{
			ObservableCollection<object> configList = GetParentNode<PixelModuRangeConfigList>().ConfigList;
			return $"    {configList.IndexOf(this)}";
		}
		set
		{
		}
	}
}
