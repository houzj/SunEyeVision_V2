using System.Windows;
using System.Windows.Markup;
using Apps.Data;
using Apps.Localization;
using HikPcUI.Controls;
using VM.Controls;

namespace GlobalCameraModuleControl;

public partial class PreviewWindow : BaseWindow, IComponentConnector
{
	private static bool needPopLostPacketWarning = true;

	public static readonly DependencyProperty LostPackgeNumProperty = DependencyProperty.Register("LostPackgeNum", typeof(int), typeof(PreviewWindow), new PropertyMetadata(0, LostPackgeNumChanged));

	private static object popLock = new object();

	public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register("ImageSource", typeof(ImageData), typeof(PreviewWindow), new PropertyMetadata(null, ImageSourceChanged));

	public int LostPackgeNum
	{
		get
		{
			return (int)GetValue(LostPackgeNumProperty);
		}
		set
		{
			SetValue(LostPackgeNumProperty, value);
		}
	}

	public ImageData ImageSource
	{
		get
		{
			return (ImageData)GetValue(ImageSourceProperty);
		}
		set
		{
			SetValue(ImageSourceProperty, value);
		}
	}

	private static void LostPackgeNumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as PreviewWindow).OnLostPackgeNumChanged();
	}

	private void OnLostPackgeNumChanged()
	{
		lock (popLock)
		{
			if (LostPackgeNum != 0 && needPopLostPacketWarning)
			{
				needPopLostPacketWarning = false;
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LostPackgeWarning"), MessageIcons.Warning);
			}
		}
	}

	private static void ImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as PreviewWindow).DoImageSourceChanged();
	}

	private void DoImageSourceChanged()
	{
		imageView.SetImageDataSource(ImageSource);
	}

	public PreviewWindow()
	{
		InitializeComponent();
	}

	private void BtnToolRegionScale_Click(object sender, RoutedEventArgs e)
	{
		imageView.Init();
	}

	private void m_btnFullScreen_Click(object sender, RoutedEventArgs e)
	{
		FullScreen instance = FullScreen.Instance;
		instance.DataContext = viewbox.DataContext;
		viewbox.Children.Remove(imageView);
		instance.SetChild(imageView);
		instance.ShowDialog();
		instance.RemoveChild();
		instance.DataContext = null;
		viewbox.Children.Add(imageView);
	}
}
