using System.ComponentModel;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class HeadItem : BaseNode, INotifyPropertyChanged
{
	public double Width;

	public string DisplayName;

	public double WidthA
	{
		get
		{
			return Width;
		}
		set
		{
			if (Width != value)
			{
				Width = value;
				OnPropertyChanged("WidthA");
			}
		}
	}

	public string DisplayNameA
	{
		get
		{
			return DisplayName;
		}
		set
		{
			if (DisplayName != value)
			{
				DisplayName = value;
				OnPropertyChanged("DisplayNameA");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public override string ToString()
	{
		return DisplayName;
	}
}
