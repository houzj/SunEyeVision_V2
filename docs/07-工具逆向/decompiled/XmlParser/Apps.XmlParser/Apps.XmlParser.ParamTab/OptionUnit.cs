using System.ComponentModel;

namespace Apps.XmlParser.ParamTab;

public class OptionUnit : INotifyPropertyChanged
{
	private string optionName = string.Empty;

	private bool isVisible = false;

	public string strOptionName => optionName;

	public bool bIsVisible
	{
		get
		{
			return isVisible;
		}
		set
		{
			if (isVisible != value)
			{
				isVisible = value;
				RaisePropertyChangedEvent("bIsVisible");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void RaisePropertyChangedEvent(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public OptionUnit(string optionName, bool isVisible)
	{
		this.optionName = optionName;
		this.isVisible = isVisible;
	}
}
