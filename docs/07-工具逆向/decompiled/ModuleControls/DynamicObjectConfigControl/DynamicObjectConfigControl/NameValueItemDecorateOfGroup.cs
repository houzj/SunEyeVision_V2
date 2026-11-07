using Apps.XmlParser.ParamTab;
using VM.CommonData;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

internal class NameValueItemDecorateOfGroup : NotifyPropertyChangedBase, IDecorate
{
	private string tempName;

	private DynamicNameValueItemOfGroup _DecorateSource;

	public object Owner { get; set; }

	public string Name
	{
		get
		{
			if (tempName == null)
			{
				tempName = DecorateSource.DisplayNameA;
			}
			return tempName;
		}
		set
		{
			if (tempName != value)
			{
				if (CheckName(value))
				{
					tempName = value;
				}
				((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
			}
		}
	}

	public DynamicNameValueItemOfGroup DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
			}
		}
	}

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value as DynamicNameValueItemOfGroup;
		}
	}

	private bool CheckName(string value)
	{
		return true;
	}
}
