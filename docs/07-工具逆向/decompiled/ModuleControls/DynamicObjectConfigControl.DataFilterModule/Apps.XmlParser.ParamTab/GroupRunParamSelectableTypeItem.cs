using System.Collections.Generic;
using VM.CommonData;

namespace Apps.XmlParser.ParamTab;

public class GroupRunParamSelectableTypeItem : NotifyPropertyChangedBase
{
	public List<string> TypeList;

	public string Name { get; set; }

	public override string ToString()
	{
		return Name;
	}
}
