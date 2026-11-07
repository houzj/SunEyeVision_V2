using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Markup;

namespace VisionMasterMain;

public partial class WelcomeDialog : Window, IComponentConnector
{
	public WelcomeDialog()
	{
		InitializeComponent();
		pictureBoxLoading.SizeMode = PictureBoxSizeMode.StretchImage;
		pictureBoxLoading.Image = Image.FromFile("WelcomePage.GIF");
	}
}
