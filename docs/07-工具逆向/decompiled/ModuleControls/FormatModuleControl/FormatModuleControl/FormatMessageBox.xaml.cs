using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace FormatModuleControl;

public partial class FormatMessageBox : UserControl, INotifyPropertyChanged, IComponentConnector
{
	public static readonly DependencyProperty ResultProperty = DependencyProperty.Register("Result", typeof(bool), typeof(FormatMessageBox), new PropertyMetadata(false));

	public static readonly DependencyProperty MessageProperty = DependencyProperty.Register("Message", typeof(string), typeof(FormatMessageBox), new PropertyMetadata((object)null));

	public bool Result
	{
		get
		{
			return (bool)GetValue(ResultProperty);
		}
		set
		{
			SetValue(ResultProperty, value);
			OnPropertyChanged("Result");
		}
	}

	public string Message
	{
		get
		{
			return (string)GetValue(MessageProperty);
		}
		set
		{
			SetValue(MessageProperty, value);
			OnPropertyChanged("Message");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public FormatMessageBox()
	{
		InitializeComponent();
	}
}
