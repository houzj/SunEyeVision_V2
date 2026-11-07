using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace FormatModuleControl;

public class Bool2IconImageConverter : IValueConverter
{
	private BitmapImage okImg = new BitmapImage(new Uri("/VM.Controls;component/Resources/ok.png", UriKind.Relative));

	private BitmapImage ngImg = new BitmapImage(new Uri("/VM.Controls;component/Resources/error.png", UriKind.Relative));

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (value is bool flag)
			{
				if (!flag)
				{
					return ngImg;
				}
				return okImg;
			}
			return null;
		}
		catch
		{
			throw new NotImplementedException();
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
