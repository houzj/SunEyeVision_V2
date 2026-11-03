using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;

public partial class BooleanControl : ToggleButton, IComponentConnector
{
	public delegate void IsCheckedChange(object sender, RoutedEventArgs e);

	public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register("OffContent", typeof(object), typeof(BooleanControl), new PropertyMetadata(null));

	public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register("OnContent", typeof(object), typeof(BooleanControl), new PropertyMetadata(null));

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

	public event IsCheckedChange OnCheckedChange;

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == ToggleButton.IsCheckedProperty)
		{
			OnCheckedChange?.Invoke(this, null);
		}
	}

	public BooleanControl()
	{
		InitializeComponent();
	}
}
