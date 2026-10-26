using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public class Area : UserControl, INotifyPropertyChanged
{
	private Cursor _CurrtentCursor;

	private static Bitmap image;

	public static readonly DependencyProperty AreaAngleProperty = DependencyProperty.Register("AreaAngle", typeof(double), typeof(Area), new PropertyMetadata(0.0));

	public Cursor CurrtentCursor
	{
		get
		{
			_CurrtentCursor?.Dispose();
			Bitmap rotateImage = ROIControlFrame.GetRotateImage(GetBiArrowImage(), (int)AreaAngle + AngleOffset);
			_CurrtentCursor = BitmapCursor.CreateBmpCursor(rotateImage);
			return _CurrtentCursor;
		}
	}

	public double AreaAngle
	{
		get
		{
			return (double)GetValue(AreaAngleProperty);
		}
		set
		{
			SetValue(AreaAngleProperty, value);
		}
	}

	protected virtual int AngleOffset => 0;

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string strPropertyInfo)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(strPropertyInfo));
		}
	}

	public Area()
	{
		SetBinding(FrameworkElement.CursorProperty, new Binding("CurrtentCursor")
		{
			Source = this
		});
	}

	~Area()
	{
		_CurrtentCursor?.Dispose();
	}

	public static Bitmap GetBiArrowImage()
	{
		if (image == null)
		{
			Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Resources.鼠标双向箭头.png");
			image = (Bitmap)System.Drawing.Image.FromStream(manifestResourceStream);
			manifestResourceStream.Dispose();
		}
		return image;
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property.Name == "IsMouseOver" && IsMouseOver)
		{
			OnPropertyChanged("CurrtentCursor");
		}
	}
}
