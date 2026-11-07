using System.Windows.Markup;
using Apps.PcUI.Common;

namespace DynamicObjectConfigControl.UI;

public partial class ShapePropertyConfigWin : ConfirmBaseWindow, IComponentConnector
{
	public object TargetObj { get; internal set; }

	public ShapePropertyConfigWin()
	{
		InitializeComponent();
	}
}
