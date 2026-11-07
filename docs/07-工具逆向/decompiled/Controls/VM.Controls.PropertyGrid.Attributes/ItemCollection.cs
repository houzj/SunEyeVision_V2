using System.Collections.Generic;

namespace VM.Controls.PropertyGrid.Attributes;

public class ItemCollection : List<Item>
{
	public void Add(object value)
	{
		Item item = new Item();
		item.DisplayName = value.ToString();
		item.Value = value;
		base.Add(item);
	}

	public void Add(object value, string displayName)
	{
		Item item = new Item();
		item.DisplayName = displayName;
		item.Value = value;
		base.Add(item);
	}
}
