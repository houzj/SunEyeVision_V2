using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using VM.ModuleControl;

namespace IMVSMachineLearningClassifierModuControl;

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
		MLClassifyMgr.Instance.OnDeleteImageEvent(DataContext);
	}

	public void DeleteTag_Click()
	{
		object dataContext = DataContext;
		ImageEntity image = (ImageEntity)((dataContext is ImageEntity) ? dataContext : null);
		if (image == null)
		{
			return;
		}
		if (image.IsOKCalibrated)
		{
			image.IsCalibrated = false;
			image.IsOKCalibrated = false;
		}
		else
		{
			CategoryData categoryData = MLClassifyMgr.Instance.Owner.pageRegisterImage.CategoryList.FirstOrDefault((CategoryData p) => p.CategoryName == image.ImageLabel);
			if (categoryData != null)
			{
				categoryData.CalibratedNum--;
			}
		}
		image.ImageLabel = "";
		MLClassifyMgr.Instance.Owner.pageRegisterImage.MarkNum--;
	}
}
