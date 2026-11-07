using System.Collections.Generic;
using System.Linq;
using Apps.Data;
using Apps.Interface;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ExternModule : VmModule, IItemsRoot
{
	public List<IFilter> ConstomFilters { get; set; } = new List<IFilter>();

	object IItemsRoot.GetItemByName(string name)
	{
		return ConstomFilters.FirstOrDefault((IFilter p) => p.Owner.GetPropertyValueByName("Guid")?.ToString() + "_" + p.Name == name);
	}
}
