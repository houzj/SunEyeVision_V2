using System;
using System.Windows.Media;

namespace HikPcUI.ListView;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
