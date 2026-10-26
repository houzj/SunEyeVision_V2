using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ImageView;

public class HikCoverStyleConverter : IValueConverter
{
	private static ResourceDictionary _ResourceDictionary;

	public ResourceDictionary ResourceDictionary
	{
		get
		{
			if (_ResourceDictionary == null)
			{
				Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Input/VM.PcUI/imageview/roidictionary.xaml", UriKind.Relative);
				_ResourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
			}
			return _ResourceDictionary;
		}
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (ResourceDictionary == null)
		{
			return null;
		}
		return (ROITypeUIEnum)value switch
		{
			ROITypeUIEnum.Level => (object)(ResourceDictionary["LevelGrid"] as Style), 
			ROITypeUIEnum.Cover => ResourceDictionary["CoverGrid"] as Style, 
			_ => ResourceDictionary["TransparentGrid"] as Style, 
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
