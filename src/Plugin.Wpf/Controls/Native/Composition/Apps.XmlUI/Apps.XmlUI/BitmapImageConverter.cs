using System;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class BitmapImageConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return GetSource(AppDomain.CurrentDomain.BaseDirectory + (string)value);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}

	private ImageSource GetSource(string fileName)
	{
		try
		{
			Bitmap bitmap = new Bitmap(fileName);
			IntPtr hbitmap = bitmap.GetHbitmap();
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource.Freeze();
			WriteableBitmap result = new WriteableBitmap(bitmapSource);
			bitmap.Dispose();
			return result;
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString());
			return null;
		}
	}
}
