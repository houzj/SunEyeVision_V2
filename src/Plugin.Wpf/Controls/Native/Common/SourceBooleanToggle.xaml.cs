using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Common;

public partial class SourceBooleanToggle : ToggleButton
{
	public delegate void IsCheckedChange(object sender, RoutedEventArgs e);

	public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register("OffContent", typeof(object), typeof(SourceBooleanToggle), new PropertyMetadata(null));

	public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register("OnContent", typeof(object), typeof(SourceBooleanToggle), new PropertyMetadata(null));

	public object OffContent
	{
		get
		{
			return GetValue(OffContentProperty);
		}
		set
		{
			SetValue(OffContentProperty, value);
		}
	}

	public object OnContent
	{
		get
		{
			return GetValue(OnContentProperty);
		}
		set
		{
			SetValue(OnContentProperty, value);
		}
	}

	public event IsCheckedChange? OnCheckedChange;

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == ToggleButton.IsCheckedProperty)
		{
			OnCheckedChange?.Invoke(this,new RoutedEventArgs());
		}
	}

	public SourceBooleanToggle()
	{
		InitializeComponent();
        OffContent=new System.Windows.Controls.Image{Stretch=System.Windows.Media.Stretch.None,Source=new System.Windows.Media.Imaging.BitmapImage(new System.Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/swith_off.png",System.UriKind.Relative))};
        OnContent=new System.Windows.Controls.Image{Stretch=System.Windows.Media.Stretch.None,Source=new System.Windows.Media.Imaging.BitmapImage(new System.Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/swith_on.png",System.UriKind.Relative))};
	}
}
