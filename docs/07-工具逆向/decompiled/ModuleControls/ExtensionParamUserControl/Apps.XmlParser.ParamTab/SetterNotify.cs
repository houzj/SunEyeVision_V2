using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Apps.XmlParser.ParamTab;

public class SetterNotify : Setter, INotifyPropertyChanged
{
	public new string PropertyNameA
	{
		get
		{
			return PropertyName;
		}
		set
		{
			SetProperty(ref PropertyName, value, "PropertyNameA");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	protected void SetProperty<T>(ref T prop, T value, [CallerMemberName] string propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(prop, value))
		{
			prop = value;
			OnPropertyChanged(propertyName);
		}
	}
}
