using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Apps.XmlUI;

public class TabControlFx : TabControl
{
	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == ItemsControl.ItemsSourceProperty && SelectedIndex == -1 && !(e.NewValue is IList { Count: >0 }))
		{
		}
	}
}
