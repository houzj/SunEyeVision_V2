using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VisionMaster.AppMainWindow;

public class GuideButton : Button
{
	public static readonly DependencyProperty HelpContentProperty = DependencyProperty.Register("HelpContent", typeof(string), typeof(GuideButton), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BackImageProperty = DependencyProperty.Register("BackImage", typeof(ImageSource), typeof(GuideButton), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BackImageClickProperty = DependencyProperty.Register("BackImageClick", typeof(ImageSource), typeof(GuideButton), new PropertyMetadata((object)null));

	public string HelpContent
	{
		get
		{
			return (string)GetValue(HelpContentProperty);
		}
		set
		{
			SetValue(HelpContentProperty, value);
		}
	}

	public ImageSource BackImage
	{
		get
		{
			return (ImageSource)GetValue(BackImageProperty);
		}
		set
		{
			SetValue(BackImageProperty, value);
		}
	}

	public ImageSource BackImageClick
	{
		get
		{
			return (ImageSource)GetValue(BackImageClickProperty);
		}
		set
		{
			SetValue(BackImageClickProperty, value);
		}
	}

	public ToolBoxType ToolBoxType { get; set; }
}
