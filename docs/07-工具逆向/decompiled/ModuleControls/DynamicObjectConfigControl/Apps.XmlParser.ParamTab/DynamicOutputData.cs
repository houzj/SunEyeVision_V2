using VM.CommonData;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class DynamicOutputData : NotifyPropertyChangedBase
{
	private string _Name;

	private string _Value;

	public VmIO IoSource { get; set; }

	public string Name
	{
		get
		{
			return _Name;
		}
		set
		{
			if (_Name != value)
			{
				_Name = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
			}
		}
	}

	public string Value
	{
		get
		{
			return _Value;
		}
		set
		{
			if (_Value != value)
			{
				_Value = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("Value");
			}
		}
	}
}
