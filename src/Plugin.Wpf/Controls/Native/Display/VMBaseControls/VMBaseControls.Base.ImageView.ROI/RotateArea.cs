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

public class RotateArea : UserControl, INotifyPropertyChanged
{
	private Cursor _RotateCursor;

	private static Bitmap image;

	public static readonly DependencyProperty RectAngleProperty = DependencyProperty.Register("RectAngle", typeof(double), typeof(RotateArea), new PropertyMetadata(0.0));

	public Cursor RotateCursor
	{
		get
		{
			_RotateCursor?.Dispose();
			Bitmap rotateImage = ROIControlFrame.GetRotateImage(GetBiArrowImage(), (int)RectAngle + AngleOffset);
			_RotateCursor = BitmapCursor.CreateBmpCursor(rotateImage);
			return _RotateCursor;
		}
	}

	public double RectAngle
	{
		get
		{
			return (double)GetValue(RectAngleProperty);
		}
		set
		{
			SetValue(RectAngleProperty, value);
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

	public RotateArea()
	{
		SetBinding(FrameworkElement.CursorProperty, new Binding("RotateCursor")
		{
			Source = this
		});
	}

	~RotateArea()
	{
		_RotateCursor?.Dispose();
	}

	public static Bitmap GetBiArrowImage()
	{
		if (image == null)
		{
			Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Resources.旋转光标.png");
			image = (Bitmap)System.Drawing.Image.FromStream(manifestResourceStream);
			manifestResourceStream.Dispose();
		}
		return image;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		System.Windows.Point position = e.GetPosition(this);
		UIElement uIElement = (UIElement)InputHitTest(position);
		if (IsMouseOver)
		{
			OnPropertyChanged("RotateCursor");
		}
	}
}
