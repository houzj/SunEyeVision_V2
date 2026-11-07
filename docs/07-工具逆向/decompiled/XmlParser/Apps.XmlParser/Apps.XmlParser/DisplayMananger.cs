using System.Collections.Generic;
using VM.Core;

namespace Apps.XmlParser;

public class DisplayMananger
{
	public static List<object> GetDisplayShape(VmModule module, string path)
	{
		List<object> list = new List<object>();
		ParamRoot paramRoot = new ParamRoot(path)
		{
			Owner = module
		};
		IList<object> displayList = paramRoot.GetDisplayList();
		foreach (Object item in displayList)
		{
			object value = item.GetValue(uint.MaxValue);
			if (value is List<object> collection)
			{
				list.AddRange(collection);
			}
			else
			{
				list.Add(value);
			}
		}
		return list;
	}
}
