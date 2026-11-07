using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public interface ITrigger
{
	void ReOnTrigger();

	void OnTrigger(object obj);

	bool CheckProperty(string proName);

	List<object> GetSetters();
}
