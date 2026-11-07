using System.Collections.Generic;
using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class ImageProcessCategory : Category
{
	public int TypeIndex;

	private readonly Dictionary<Category, List<NameValueItem>> _dictionary = new Dictionary<Category, List<NameValueItem>>();

	public List<NameValueItem> GetNameValueItems()
	{
		if (!_dictionary.TryGetValue(this, out var value))
		{
			List<NameValueItem> list = new List<NameValueItem>();
			foreach (object item in Items)
			{
				if (!(item is NameValueItem nameValueItem))
				{
					continue;
				}
				if (string.IsNullOrEmpty(nameValueItem.AlgorithmIndex))
				{
					List<NameValueItem> list2 = null;
					if (nameValueItem is IntegerBettween)
					{
						list2 = (from p in (nameValueItem as IntegerBettween).Integers.OfType<NameValueItem>()
							where !string.IsNullOrEmpty(p.AlgorithmIndex)
							select p).ToList();
					}
					else if (nameValueItem is FloatBettween)
					{
						list2 = (from p in (nameValueItem as FloatBettween).Floats.OfType<NameValueItem>()
							where !string.IsNullOrEmpty(p.AlgorithmIndex)
							select p).ToList();
					}
					if (list2 != null)
					{
						list.AddRange(list2);
					}
				}
				else
				{
					list.Add(nameValueItem);
				}
			}
			_dictionary.Add(this, list);
			return list;
		}
		return value;
	}
}
