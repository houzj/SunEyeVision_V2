using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace FewShotCommonControl;

public partial class ImageView1 : UserControl, IComponentConnector
{
	public ImageView1()
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
		EdgeLearningData.Instance.OnDeleteImageEvent(DataContext);
	}

	public void DeleteTag_Click()
	{
		EdgeLearningData.Instance.OnDeleteImageTagEvent(DataContext);
	}
}
