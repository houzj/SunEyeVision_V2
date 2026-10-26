using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class ImageStitchPanel : AddListControlBase, INotifyPropertyChanged, IComponentConnector
{
	public override bool IsCanDelete => false;

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public ImageStitchPanel()
	{
		InitializeComponent();
		DG = dg;
	}

	private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is Grid grid)
		{
			MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
			{
				RoutedEvent = UIElement.MouseWheelEvent,
				Source = sender
			};
			grid.RaiseEvent(e2);
		}
	}
}
