using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_ColorShadingCanvas", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_ColorShadeSelector", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_SpectrumSlider", Type = typeof(ColorSpectrumSlider))]
[TemplatePart(Name = "PART_HexadecimalTextBox", Type = typeof(TextBox))]
public class ColorCanvas : Control
{
	private const string PART_ColorShadingCanvas = "PART_ColorShadingCanvas";

	private const string PART_ColorShadeSelector = "PART_ColorShadeSelector";

	private const string PART_SpectrumSlider = "PART_SpectrumSlider";

	private const string PART_HexadecimalTextBox = "PART_HexadecimalTextBox";

	private TranslateTransform _colorShadeSelectorTransform = new TranslateTransform();

	private Canvas _colorShadingCanvas;

	private Canvas _colorShadeSelector;

	private ColorSpectrumSlider _spectrumSlider;

	private TextBox _hexadecimalTextBox;

	private Point? _currentColorPosition;

	private bool _surpressPropertyChanged;

	private bool _updateSpectrumSliderValue = true;

	public static readonly DependencyProperty SelectedColorProperty;

	public static readonly DependencyProperty AProperty;

	public static readonly DependencyProperty RProperty;

	public static readonly DependencyProperty GProperty;

	public static readonly DependencyProperty BProperty;

	public static readonly DependencyProperty HexadecimalStringProperty;

	public static readonly DependencyProperty UsingAlphaChannelProperty;

	public static readonly RoutedEvent SelectedColorChangedEvent;

	public Color? SelectedColor
	{
		get
		{
			return (Color?)GetValue(SelectedColorProperty);
		}
		set
		{
			SetValue(SelectedColorProperty, value);
		}
	}

	public byte A
	{
		get
		{
			return (byte)GetValue(AProperty);
		}
		set
		{
			SetValue(AProperty, value);
		}
	}

	public byte R
	{
		get
		{
			return (byte)GetValue(RProperty);
		}
		set
		{
			SetValue(RProperty, value);
		}
	}

	public byte G
	{
		get
		{
			return (byte)GetValue(GProperty);
		}
		set
		{
			SetValue(GProperty, value);
		}
	}

	public byte B
	{
		get
		{
			return (byte)GetValue(BProperty);
		}
		set
		{
			SetValue(BProperty, value);
		}
	}

	public string HexadecimalString
	{
		get
		{
			return (string)GetValue(HexadecimalStringProperty);
		}
		set
		{
			SetValue(HexadecimalStringProperty, value);
		}
	}

	public bool UsingAlphaChannel
	{
		get
		{
			return (bool)GetValue(UsingAlphaChannelProperty);
		}
		set
		{
			SetValue(UsingAlphaChannelProperty, value);
		}
	}

	public event RoutedPropertyChangedEventHandler<Color?> SelectedColorChanged
	{
		add
		{
			AddHandler(SelectedColorChangedEvent, value);
		}
		remove
		{
			RemoveHandler(SelectedColorChangedEvent, value);
		}
	}

	private static void OnSelectedColorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnSelectedColorChanged((Color?)e.OldValue, (Color?)e.NewValue);
		}
	}

	protected virtual void OnSelectedColorChanged(Color? oldValue, Color? newValue)
	{
		SetHexadecimalStringProperty(GetFormatedColorString(newValue), modifyFromUI: false);
		UpdateRGBValues(newValue);
		UpdateColorShadeSelectorPosition(newValue);
		RoutedPropertyChangedEventArgs<Color?> e = new RoutedPropertyChangedEventArgs<Color?>(oldValue, newValue);
		e.RoutedEvent = SelectedColorChangedEvent;
		RaiseEvent(e);
	}

	private static void OnAChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnAChanged((byte)e.OldValue, (byte)e.NewValue);
		}
	}

	protected virtual void OnAChanged(byte oldValue, byte newValue)
	{
		if (!_surpressPropertyChanged)
		{
			UpdateSelectedColor();
		}
	}

	private static void OnRChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnRChanged((byte)e.OldValue, (byte)e.NewValue);
		}
	}

	protected virtual void OnRChanged(byte oldValue, byte newValue)
	{
		if (!_surpressPropertyChanged)
		{
			UpdateSelectedColor();
		}
	}

	private static void OnGChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnGChanged((byte)e.OldValue, (byte)e.NewValue);
		}
	}

	protected virtual void OnGChanged(byte oldValue, byte newValue)
	{
		if (!_surpressPropertyChanged)
		{
			UpdateSelectedColor();
		}
	}

	private static void OnBChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnBChanged((byte)e.OldValue, (byte)e.NewValue);
		}
	}

	protected virtual void OnBChanged(byte oldValue, byte newValue)
	{
		if (!_surpressPropertyChanged)
		{
			UpdateSelectedColor();
		}
	}

	private static void OnHexadecimalStringChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnHexadecimalStringChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnHexadecimalStringChanged(string oldValue, string newValue)
	{
		string formatedColorString = GetFormatedColorString(newValue);
		if (!GetFormatedColorString(SelectedColor).Equals(formatedColorString))
		{
			Color? color = null;
			if (!string.IsNullOrEmpty(formatedColorString))
			{
				color = (Color)ColorConverter.ConvertFromString(formatedColorString);
			}
			UpdateSelectedColor(color);
		}
		SetHexadecimalTextBoxTextProperty(newValue);
	}

	private static object OnCoerceHexadecimalString(DependencyObject d, object basevalue)
	{
		ColorCanvas colorCanvas = (ColorCanvas)d;
		if (colorCanvas == null)
		{
			return basevalue;
		}
		return colorCanvas.OnCoerceHexadecimalString(basevalue);
	}

	private object OnCoerceHexadecimalString(object newValue)
	{
		string text = newValue as string;
		string text2 = text;
		try
		{
			if (!string.IsNullOrEmpty(text2))
			{
				ColorConverter.ConvertFromString(text);
			}
		}
		catch
		{
			throw new InvalidDataException("Color provided is not in the correct format.");
		}
		return text2;
	}

	private static void OnUsingAlphaChannelPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorCanvas colorCanvas)
		{
			colorCanvas.OnUsingAlphaChannelChanged();
		}
	}

	protected virtual void OnUsingAlphaChannelChanged()
	{
		SetHexadecimalStringProperty(GetFormatedColorString(SelectedColor), modifyFromUI: false);
	}

	static ColorCanvas()
	{
		SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color?), typeof(ColorCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));
		AProperty = DependencyProperty.Register("A", typeof(byte), typeof(ColorCanvas), new UIPropertyMetadata(byte.MaxValue, OnAChanged));
		RProperty = DependencyProperty.Register("R", typeof(byte), typeof(ColorCanvas), new UIPropertyMetadata((byte)0, OnRChanged));
		GProperty = DependencyProperty.Register("G", typeof(byte), typeof(ColorCanvas), new UIPropertyMetadata((byte)0, OnGChanged));
		BProperty = DependencyProperty.Register("B", typeof(byte), typeof(ColorCanvas), new UIPropertyMetadata((byte)0, OnBChanged));
		HexadecimalStringProperty = DependencyProperty.Register("HexadecimalString", typeof(string), typeof(ColorCanvas), new UIPropertyMetadata("", OnHexadecimalStringChanged, OnCoerceHexadecimalString));
		UsingAlphaChannelProperty = DependencyProperty.Register("UsingAlphaChannel", typeof(bool), typeof(ColorCanvas), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnUsingAlphaChannelPropertyChanged));
		SelectedColorChangedEvent = EventManager.RegisterRoutedEvent("SelectedColorChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<Color?>), typeof(ColorCanvas));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorCanvas), new FrameworkPropertyMetadata(typeof(ColorCanvas)));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_colorShadingCanvas != null)
		{
			_colorShadingCanvas.MouseLeftButtonDown -= ColorShadingCanvas_MouseLeftButtonDown;
			_colorShadingCanvas.MouseLeftButtonUp -= ColorShadingCanvas_MouseLeftButtonUp;
			_colorShadingCanvas.MouseMove -= ColorShadingCanvas_MouseMove;
			_colorShadingCanvas.SizeChanged -= ColorShadingCanvas_SizeChanged;
		}
		_colorShadingCanvas = GetTemplateChild("PART_ColorShadingCanvas") as Canvas;
		if (_colorShadingCanvas != null)
		{
			_colorShadingCanvas.MouseLeftButtonDown += ColorShadingCanvas_MouseLeftButtonDown;
			_colorShadingCanvas.MouseLeftButtonUp += ColorShadingCanvas_MouseLeftButtonUp;
			_colorShadingCanvas.MouseMove += ColorShadingCanvas_MouseMove;
			_colorShadingCanvas.SizeChanged += ColorShadingCanvas_SizeChanged;
		}
		_colorShadeSelector = GetTemplateChild("PART_ColorShadeSelector") as Canvas;
		if (_colorShadeSelector != null)
		{
			_colorShadeSelector.RenderTransform = _colorShadeSelectorTransform;
		}
		if (_spectrumSlider != null)
		{
			_spectrumSlider.ValueChanged -= SpectrumSlider_ValueChanged;
		}
		_spectrumSlider = GetTemplateChild("PART_SpectrumSlider") as ColorSpectrumSlider;
		if (_spectrumSlider != null)
		{
			_spectrumSlider.ValueChanged += SpectrumSlider_ValueChanged;
		}
		if (_hexadecimalTextBox != null)
		{
			_hexadecimalTextBox.LostFocus -= HexadecimalTextBox_LostFocus;
		}
		_hexadecimalTextBox = GetTemplateChild("PART_HexadecimalTextBox") as TextBox;
		if (_hexadecimalTextBox != null)
		{
			_hexadecimalTextBox.LostFocus += HexadecimalTextBox_LostFocus;
		}
		UpdateRGBValues(SelectedColor);
		UpdateColorShadeSelectorPosition(SelectedColor);
		SetHexadecimalTextBoxTextProperty(GetFormatedColorString(SelectedColor));
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Key == Key.Return && e.OriginalSource is TextBox)
		{
			TextBox textBox = (TextBox)e.OriginalSource;
			if (textBox.Name == "PART_HexadecimalTextBox")
			{
				SetHexadecimalStringProperty(textBox.Text, modifyFromUI: true);
			}
		}
	}

	private void ColorShadingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (_colorShadingCanvas != null)
		{
			Point position = e.GetPosition(_colorShadingCanvas);
			UpdateColorShadeSelectorPositionAndCalculateColor(position, calculateColor: true);
			_colorShadingCanvas.CaptureMouse();
			e.Handled = true;
		}
	}

	private void ColorShadingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_colorShadingCanvas != null)
		{
			_colorShadingCanvas.ReleaseMouseCapture();
		}
	}

	private void ColorShadingCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (_colorShadingCanvas != null && e.LeftButton == MouseButtonState.Pressed)
		{
			Point position = e.GetPosition(_colorShadingCanvas);
			UpdateColorShadeSelectorPositionAndCalculateColor(position, calculateColor: true);
			Mouse.Synchronize();
		}
	}

	private void ColorShadingCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (_currentColorPosition.HasValue)
		{
			Point p = new Point
			{
				X = _currentColorPosition.Value.X * e.NewSize.Width,
				Y = _currentColorPosition.Value.Y * e.NewSize.Height
			};
			UpdateColorShadeSelectorPositionAndCalculateColor(p, calculateColor: false);
		}
	}

	private void SpectrumSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_currentColorPosition.HasValue && SelectedColor.HasValue)
		{
			CalculateColor(_currentColorPosition.Value);
		}
	}

	private void HexadecimalTextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		TextBox textBox = sender as TextBox;
		SetHexadecimalStringProperty(textBox.Text, modifyFromUI: true);
	}

	private void UpdateSelectedColor()
	{
		SelectedColor = Color.FromArgb(A, R, G, B);
	}

	private void UpdateSelectedColor(Color? color)
	{
		SelectedColor = ((color.HasValue && color.HasValue) ? new Color?(Color.FromArgb(color.Value.A, color.Value.R, color.Value.G, color.Value.B)) : ((Color?)null));
	}

	private void UpdateRGBValues(Color? color)
	{
		if (color.HasValue && color.HasValue)
		{
			_surpressPropertyChanged = true;
			A = color.Value.A;
			R = color.Value.R;
			G = color.Value.G;
			B = color.Value.B;
			_surpressPropertyChanged = false;
		}
	}

	private void UpdateColorShadeSelectorPositionAndCalculateColor(Point p, bool calculateColor)
	{
		if (_colorShadingCanvas != null && _colorShadeSelector != null)
		{
			if (p.Y < 0.0)
			{
				p.Y = 0.0;
			}
			if (p.X < 0.0)
			{
				p.X = 0.0;
			}
			if (p.X > _colorShadingCanvas.ActualWidth)
			{
				p.X = _colorShadingCanvas.ActualWidth;
			}
			if (p.Y > _colorShadingCanvas.ActualHeight)
			{
				p.Y = _colorShadingCanvas.ActualHeight;
			}
			_colorShadeSelectorTransform.X = p.X - _colorShadeSelector.Width / 2.0;
			_colorShadeSelectorTransform.Y = p.Y - _colorShadeSelector.Height / 2.0;
			p.X /= _colorShadingCanvas.ActualWidth;
			p.Y /= _colorShadingCanvas.ActualHeight;
			_currentColorPosition = p;
			if (calculateColor)
			{
				CalculateColor(p);
			}
		}
	}

	private void UpdateColorShadeSelectorPosition(Color? color)
	{
		if (_spectrumSlider != null && _colorShadingCanvas != null && color.HasValue && color.HasValue)
		{
			_currentColorPosition = null;
			HsvColor hsvColor = ColorUtilities.ConvertRgbToHsv(color.Value.R, color.Value.B, color.Value.G);
			if (_updateSpectrumSliderValue)
			{
				_spectrumSlider.Value = hsvColor.H;
			}
			Point value = new Point(hsvColor.S, 1.0 - hsvColor.V);
			_currentColorPosition = value;
			_colorShadeSelectorTransform.X = value.X * _colorShadingCanvas.Width - 5.0;
			_colorShadeSelectorTransform.Y = value.Y * _colorShadingCanvas.Height - 5.0;
		}
	}

	private void CalculateColor(Point p)
	{
		if (_spectrumSlider != null)
		{
			HsvColor hsvColor = new HsvColor(360.0 - _spectrumSlider.Value, 1.0, 1.0);
			hsvColor.S = p.X;
			hsvColor.V = 1.0 - p.Y;
			HsvColor hsvColor2 = hsvColor;
			Color value = ColorUtilities.ConvertHsvToRgb(hsvColor2.H, hsvColor2.S, hsvColor2.V);
			value.A = A;
			_updateSpectrumSliderValue = false;
			SelectedColor = value;
			_updateSpectrumSliderValue = true;
			SetHexadecimalStringProperty(GetFormatedColorString(SelectedColor), modifyFromUI: false);
		}
	}

	private string GetFormatedColorString(Color? colorToFormat)
	{
		if (!colorToFormat.HasValue || !colorToFormat.HasValue)
		{
			return string.Empty;
		}
		return ColorUtilities.FormatColorString(colorToFormat.ToString(), UsingAlphaChannel);
	}

	private string GetFormatedColorString(string stringToFormat)
	{
		return ColorUtilities.FormatColorString(stringToFormat, UsingAlphaChannel);
	}

	private void SetHexadecimalStringProperty(string newValue, bool modifyFromUI)
	{
		if (modifyFromUI)
		{
			try
			{
				if (!string.IsNullOrEmpty(newValue))
				{
					ColorConverter.ConvertFromString(newValue);
				}
				HexadecimalString = newValue;
				return;
			}
			catch
			{
				SetHexadecimalTextBoxTextProperty(HexadecimalString);
				return;
			}
		}
		HexadecimalString = newValue;
	}

	private void SetHexadecimalTextBoxTextProperty(string newValue)
	{
		if (_hexadecimalTextBox != null)
		{
			_hexadecimalTextBox.Text = newValue;
		}
	}
}
