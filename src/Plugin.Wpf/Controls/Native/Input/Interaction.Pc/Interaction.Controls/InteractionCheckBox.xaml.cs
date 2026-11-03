using SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;

public partial class InteractionCheckBox : UserControl, IComponentConnector
{
	private string strON = "pack://application:,,,/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Input/Interaction.Pc/resources/swith_on_dark.png";

	private string strOFF = "pack://application:,,,/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Input/Interaction.Pc/resources/swith_off_dark.png";

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(InteractionCheckBox), new FrameworkPropertyMetadata(OnIsCheckedChanged));

	public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent("IsCheckedChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<bool>), typeof(InteractionCheckBox));

	public static readonly DependencyProperty _context = DependencyProperty.Register("LblText", typeof(string), typeof(InteractionCheckBox), new PropertyMetadata("名称"));

	public bool IsChecked
	{
		get
		{
			return (bool)GetValue(ValueProperty);
		}
		set
		{
			SetValue(ValueProperty, value);
		}
	}

	public string LblText
	{
		get
		{
			return (string)GetValue(_context);
		}
		set
		{
			SetValue(_context, value);
		}
	}

	public event RoutedPropertyChangedEventHandler<bool> IsCheckedChanged
	{
		add
		{
			AddHandler(ValueChangedEvent, value);
		}
		remove
		{
			RemoveHandler(ValueChangedEvent, value);
		}
	}

	public InteractionCheckBox()
	{
		InitializeComponent();
	}

	private static void OnIsCheckedChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
	{
		InteractionCheckBox hikCheckBox = (InteractionCheckBox)obj;
		RoutedPropertyChangedEventArgs<bool> args2 = new RoutedPropertyChangedEventArgs<bool>((bool)args.OldValue, (bool)args.NewValue, ValueChangedEvent);
		hikCheckBox.OnValueChanged(args2);
	}

	protected virtual void OnValueChanged(RoutedPropertyChangedEventArgs<bool> args)
	{
		RaiseEvent(args);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage = (IsChecked ? UrlToImage(strON) : UrlToImage(strOFF));
		imgName.Source = bitmapImage;
	}

	private BitmapImage UrlToImage(string path)
	{
		BitmapImage bitmapImage = new BitmapImage();
		try
		{
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(path);
			bitmapImage.EndInit();
		}
		catch (Exception)
		{
			return null;
		}
		return bitmapImage;
	}

	private void MouseLeftButtonClick(object sender, MouseButtonEventArgs e)
	{
		if (!IsChecked)
		{
			IsChecked = true;
		}
		else
		{
			IsChecked = false;
		}
	}

	private new void IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		SolidColorBrush background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
		mainGrid.Background = background;
	}
}
