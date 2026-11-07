namespace Apps.XmlParser.ParamTab;

public class SubAlgorithmTabRoot : AlgorithmTabRoot
{
	public AlgorithmTabRoot Root { get; set; }

	public string HashCode { get; set; }

	public override bool IsSubscribeInWindow
	{
		get
		{
			return Root?.IsSubscribeInWindow ?? false;
		}
		set
		{
			if (Root != null)
			{
				Root.IsSubscribeInWindow = value;
			}
		}
	}

	public override NameValueItem SelectedSubscribeParam
	{
		get
		{
			return Root?.SelectedSubscribeParam;
		}
		set
		{
			if (Root != null)
			{
				Root.SelectedSubscribeParam = value;
			}
		}
	}

	public override bool IsSubscribeIndex
	{
		get
		{
			return Root?.IsSubscribeIndex ?? false;
		}
		set
		{
			if (Root != null)
			{
				Root.IsSubscribeIndex = value;
			}
		}
	}

	public void ChangeItemNames(string hashCode)
	{
		foreach (object tab2 in Tabs)
		{
			if (!(tab2 is Tab tab))
			{
				continue;
			}
			foreach (object item in tab.AllNodes())
			{
				if (item is NameValueItem nameValueItem)
				{
					nameValueItem.ComTailName = "_" + hashCode;
				}
			}
		}
	}
}
