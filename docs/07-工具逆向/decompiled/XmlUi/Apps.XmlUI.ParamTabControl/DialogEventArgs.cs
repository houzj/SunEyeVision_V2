using System;
using System.Windows.Media;

namespace Apps.XmlUI.ParamTabControl;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
