using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace IMVSMarkInspModuVAControl;

public partial class ImageView : UserControl, IComponentConnector
{
	public ImageView()
	{
		InitializeComponent();
		LstItemPanel.Visibility = Visibility.Hidden;
	}

	private void Image_MouseLeave(object sender, MouseEventArgs e)
	{
		LstItemPanel.Visibility = Visibility.Hidden;
	}

	private void Image_MouseEnter(object sender, MouseEventArgs e)
	{
		LstItemPanel.Visibility = Visibility.Visible;
	}

	private void DelImage_Click(object sender, RoutedEventArgs e)
	{
		OCRTrainDataMgr.Instance.OnDeleteImageEvent(DataContext);
	}
}
