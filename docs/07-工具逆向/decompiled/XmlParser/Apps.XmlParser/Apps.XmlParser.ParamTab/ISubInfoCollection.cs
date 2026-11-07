using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public interface ISubInfoCollection
{
	IList<object> SubChildren { get; }
}
