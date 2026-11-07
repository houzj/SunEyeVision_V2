using System.ComponentModel;
using System.Windows.Controls;

namespace FeatureMatchCommonControl;

public class FeaturepatMatchDialogUIBase : Control, INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
