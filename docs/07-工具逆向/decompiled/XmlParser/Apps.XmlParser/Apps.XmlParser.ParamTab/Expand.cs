using System.Collections.Generic;
using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class Expand : NameValueItem
{
	public bool CurValue;

	public bool CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			List<NameValueItem> list = new List<NameValueItem>();
			GetSubscribeItem(this, list);
			foreach (NameValueItem item in list)
			{
				item.IsDeleteSubscribeA = value;
			}
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			CurValueA = bool.Parse(value);
		}
	}

	private void GetSubscribeItem(NameValueItem item, List<NameValueItem> paramList)
	{
		if (item == null || paramList == null || item.TriggersA == null || item.TriggersA.Count == 0)
		{
			return;
		}
		foreach (object item3 in item.TriggersA)
		{
			if (!(item3 is ITrigger trigger))
			{
				continue;
			}
			List<object> setters = trigger.GetSetters();
			if (setters == null || setters.Count <= 0)
			{
				continue;
			}
			foreach (object item4 in setters)
			{
				if (!(item4 is Setter { TargetObj: not null } setter))
				{
					continue;
				}
				if (setter.TargetObj is ISubInfo)
				{
					paramList.Add(setter.TargetObj);
				}
				else if (setter.TargetObj is ISubChild subChild)
				{
					foreach (object item5 in subChild.SubChildren.ToList())
					{
						if (item5 is NameValueItem item2)
						{
							paramList.Add(item2);
						}
					}
				}
				GetSubscribeItem(setter.TargetObj, paramList);
			}
		}
	}
}
