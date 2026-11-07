using System.Windows.Controls;
using System.Windows.Input;

namespace Apps.XmlParser.ParamTab;

public class ComboNoWheel : ComboBox
{
	protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
	{
		e.Handled = true;
	}
}
