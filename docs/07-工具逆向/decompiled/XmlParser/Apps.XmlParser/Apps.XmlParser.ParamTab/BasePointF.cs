using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class BasePointF : PointF
{
	public string EditCommandName;

	public object EditCommand;

	public string EditCommandNameA => Name + ".ConfigCommandA";

	public object EditCommandA => Operation.GetPropertyValueByName(EditCommandNameA);

	public override int GetParamUnit()
	{
		OnPropertyChanged("EditCommandA");
		return base.GetParamUnit();
	}
}
