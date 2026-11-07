using System.Windows.Controls;
using System.Windows.Input;

namespace Apps.XmlUI.ParamTab;

public class ComboBoxNoWheel : ComboBox
{
	protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
	{
		e.Handled = true;
	}
}
