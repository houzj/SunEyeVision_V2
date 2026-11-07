using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HikPcUI.ImageView;

public class HikCoverCircleStyleConverter : IValueConverter
{
	private static ResourceDictionary _ResourceDictionary;

	public ResourceDictionary ResourceDictionary
	{
		get
		{
			if (_ResourceDictionary == null)
			{
				Uri resourceLocator = new Uri("/VM.PcUI;component/ImageView/ROIDictionary.xaml", UriKind.Relative);
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
			ROITypeUIEnum.Level => (object)(ResourceDictionary["LevelPath"] as Style), 
			ROITypeUIEnum.Cover => ResourceDictionary["CoverPath"] as Style, 
			_ => ResourceDictionary["TransparentPath"] as Style, 
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
