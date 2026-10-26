using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_AvailableColors", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_StandardColors", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_RecentColors", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_ColorPickerToggleButton", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_ColorPickerPalettePopup", Type = typeof(Popup))]
public class ColorPicker : Control
{
	private const string PART_AvailableColors = "PART_AvailableColors";

	private const string PART_StandardColors = "PART_StandardColors";

	private const string PART_RecentColors = "PART_RecentColors";

	private const string PART_ColorPickerToggleButton = "PART_ColorPickerToggleButton";

	private const string PART_ColorPickerPalettePopup = "PART_ColorPickerPalettePopup";

	private ListBox _availableColors;

	private ListBox _standardColors;

	private ListBox _recentColors;

	private ToggleButton _toggleButton;

	private Popup _popup;

	private Color? _initialColor;

	private bool _selectionChanged;

	public static readonly DependencyProperty ColorPickerToggleButtonVisibilityProperty;

	public static readonly DependencyProperty AdvancedButtonHeaderProperty;

	public static readonly DependencyProperty AvailableColorsProperty;

	public static readonly DependencyProperty AvailableColorsSortingModeProperty;

	public static readonly DependencyProperty AvailableColorsHeaderProperty;

	public static readonly DependencyProperty ButtonStyleProperty;

	public static readonly DependencyProperty DisplayColorAndNameProperty;

	public static readonly DependencyProperty ColorModeProperty;

	public static readonly DependencyProperty IsOpenProperty;

	public static readonly DependencyProperty MaxDropDownWidthProperty;

	public static readonly DependencyProperty RecentColorsProperty;

	public static readonly DependencyProperty RecentColorsHeaderProperty;

	public static readonly DependencyProperty SelectedColorProperty;

	public static readonly DependencyProperty SelectedColorTextProperty;

	public static readonly DependencyProperty ShowTabHeadersProperty;

	public static readonly DependencyProperty ShowAvailableColorsProperty;

	public static readonly DependencyProperty ShowRecentColorsProperty;

	public static readonly DependencyProperty ShowStandardColorsProperty;

	public static readonly DependencyProperty ShowDropDownButtonProperty;

	public static readonly DependencyProperty StandardButtonHeaderProperty;

	public static readonly DependencyProperty StandardColorsProperty;

	public static readonly DependencyProperty StandardColorsHeaderProperty;

	public static readonly DependencyProperty UsingAlphaChannelProperty;

	public static readonly RoutedEvent SelectedColorChangedEvent;

	public static readonly RoutedEvent OpenedEvent;

	public static readonly RoutedEvent ClosedEvent;

	public Visibility ColorPickerToggleButtonVisibility
	{
		get
		{
			return (Visibility)GetValue(ColorPickerToggleButtonVisibilityProperty);
		}
		set
		{
			SetValue(ColorPickerToggleButtonVisibilityProperty, value);
		}
	}

	public string AdvancedButtonHeader
	{
		get
		{
			return (string)GetValue(AdvancedButtonHeaderProperty);
		}
		set
		{
			SetValue(AdvancedButtonHeaderProperty, value);
		}
	}

	public ObservableCollection<ColorItem> AvailableColors
	{
		get
		{
			return (ObservableCollection<ColorItem>)GetValue(AvailableColorsProperty);
		}
		set
		{
			SetValue(AvailableColorsProperty, value);
		}
	}

	public ColorSortingMode AvailableColorsSortingMode
	{
		get
		{
			return (ColorSortingMode)GetValue(AvailableColorsSortingModeProperty);
		}
		set
		{
			SetValue(AvailableColorsSortingModeProperty, value);
		}
	}

	public string AvailableColorsHeader
	{
		get
		{
			return (string)GetValue(AvailableColorsHeaderProperty);
		}
		set
		{
			SetValue(AvailableColorsHeaderProperty, value);
		}
	}

	public Style ButtonStyle
	{
		get
		{
			return (Style)GetValue(ButtonStyleProperty);
		}
		set
		{
			SetValue(ButtonStyleProperty, value);
		}
	}

	public bool DisplayColorAndName
	{
		get
		{
			return (bool)GetValue(DisplayColorAndNameProperty);
		}
		set
		{
			SetValue(DisplayColorAndNameProperty, value);
		}
	}

	public ColorMode ColorMode
	{
		get
		{
			return (ColorMode)GetValue(ColorModeProperty);
		}
		set
		{
			SetValue(ColorModeProperty, value);
		}
	}

	public bool IsOpen
	{
		get
		{
			return (bool)GetValue(IsOpenProperty);
		}
		set
		{
			SetValue(IsOpenProperty, value);
		}
	}

	public double MaxDropDownWidth
	{
		get
		{
			return (double)GetValue(MaxDropDownWidthProperty);
		}
		set
		{
			SetValue(MaxDropDownWidthProperty, value);
		}
	}

	public ObservableCollection<ColorItem> RecentColors
	{
		get
		{
			return (ObservableCollection<ColorItem>)GetValue(RecentColorsProperty);
		}
		set
		{
			SetValue(RecentColorsProperty, value);
		}
	}

	public string RecentColorsHeader
	{
		get
		{
			return (string)GetValue(RecentColorsHeaderProperty);
		}
		set
		{
			SetValue(RecentColorsHeaderProperty, value);
		}
	}

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

	public string SelectedColorText
	{
		get
		{
			return (string)GetValue(SelectedColorTextProperty);
		}
		protected set
		{
			SetValue(SelectedColorTextProperty, value);
		}
	}

	public bool ShowTabHeaders
	{
		get
		{
			return (bool)GetValue(ShowTabHeadersProperty);
		}
		set
		{
			SetValue(ShowTabHeadersProperty, value);
		}
	}

	public bool ShowAvailableColors
	{
		get
		{
			return (bool)GetValue(ShowAvailableColorsProperty);
		}
		set
		{
			SetValue(ShowAvailableColorsProperty, value);
		}
	}

	public bool ShowRecentColors
	{
		get
		{
			return (bool)GetValue(ShowRecentColorsProperty);
		}
		set
		{
			SetValue(ShowRecentColorsProperty, value);
		}
	}

	public bool ShowStandardColors
	{
		get
		{
			return (bool)GetValue(ShowStandardColorsProperty);
		}
		set
		{
			SetValue(ShowStandardColorsProperty, value);
		}
	}

	public bool ShowDropDownButton
	{
		get
		{
			return (bool)GetValue(ShowDropDownButtonProperty);
		}
		set
		{
			SetValue(ShowDropDownButtonProperty, value);
		}
	}

	public string StandardButtonHeader
	{
		get
		{
			return (string)GetValue(StandardButtonHeaderProperty);
		}
		set
		{
			SetValue(StandardButtonHeaderProperty, value);
		}
	}

	public ObservableCollection<ColorItem> StandardColors
	{
		get
		{
			return (ObservableCollection<ColorItem>)GetValue(StandardColorsProperty);
		}
		set
		{
			SetValue(StandardColorsProperty, value);
		}
	}

	public string StandardColorsHeader
	{
		get
		{
			return (string)GetValue(StandardColorsHeaderProperty);
		}
		set
		{
			SetValue(StandardColorsHeaderProperty, value);
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

	public event RoutedEventHandler Opened
	{
		add
		{
			AddHandler(OpenedEvent, value);
		}
		remove
		{
			RemoveHandler(OpenedEvent, value);
		}
	}

	public event RoutedEventHandler Closed
	{
		add
		{
			AddHandler(ClosedEvent, value);
		}
		remove
		{
			RemoveHandler(ClosedEvent, value);
		}
	}

	private static void OnAvailableColorsSortingModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((ColorPicker)d)?.OnAvailableColorsSortingModeChanged((ColorSortingMode)e.OldValue, (ColorSortingMode)e.NewValue);
	}

	private void OnAvailableColorsSortingModeChanged(ColorSortingMode oldValue, ColorSortingMode newValue)
	{
		ListCollectionView listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(AvailableColors);
		if (listCollectionView != null)
		{
			listCollectionView.CustomSort = ((AvailableColorsSortingMode == ColorSortingMode.HueSaturationBrightness) ? new ColorSorter() : null);
		}
	}

	private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((ColorPicker)d)?.OnIsOpenChanged((bool)e.OldValue, (bool)e.NewValue);
	}

	private void OnIsOpenChanged(bool oldValue, bool newValue)
	{
		if (newValue)
		{
			_initialColor = SelectedColor;
		}
		RoutedEventArgs e = new RoutedEventArgs(newValue ? OpenedEvent : ClosedEvent, this);
		RaiseEvent(e);
	}

	private static void OnMaxDropDownWidthChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ColorPicker colorPicker)
		{
			colorPicker.OnMaxDropDownWidthChanged((double)e.OldValue, (double)e.NewValue);
		}
	}

	protected virtual void OnMaxDropDownWidthChanged(double oldValue, double newValue)
	{
	}

	private static void OnSelectedColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((ColorPicker)d)?.OnSelectedColorChanged((Color?)e.OldValue, (Color?)e.NewValue);
	}

	private void OnSelectedColorChanged(Color? oldValue, Color? newValue)
	{
		SelectedColorText = GetFormatedColorString(newValue);
		RoutedPropertyChangedEventArgs<Color?> e = new RoutedPropertyChangedEventArgs<Color?>(oldValue, newValue);
		e.RoutedEvent = SelectedColorChangedEvent;
		RaiseEvent(e);
	}

	private static void OnUsingAlphaChannelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((ColorPicker)d)?.OnUsingAlphaChannelChanged();
	}

	private void OnUsingAlphaChannelChanged()
	{
		SelectedColorText = GetFormatedColorString(SelectedColor);
	}

	static ColorPicker()
	{
		ColorPickerToggleButtonVisibilityProperty = DependencyProperty.Register("ColorPickerToggleButtonVisibility", typeof(Visibility), typeof(ColorPicker), new PropertyMetadata(Visibility.Visible));
		AdvancedButtonHeaderProperty = DependencyProperty.Register("AdvancedButtonHeader", typeof(string), typeof(ColorPicker), new UIPropertyMetadata("Advanced"));
		AvailableColorsProperty = DependencyProperty.Register("AvailableColors", typeof(ObservableCollection<ColorItem>), typeof(ColorPicker), new UIPropertyMetadata(CreateAvailableColors()));
		AvailableColorsSortingModeProperty = DependencyProperty.Register("AvailableColorsSortingMode", typeof(ColorSortingMode), typeof(ColorPicker), new UIPropertyMetadata(ColorSortingMode.Alphabetical, OnAvailableColorsSortingModeChanged));
		AvailableColorsHeaderProperty = DependencyProperty.Register("AvailableColorsHeader", typeof(string), typeof(ColorPicker), new UIPropertyMetadata("Available Colors"));
		ButtonStyleProperty = DependencyProperty.Register("ButtonStyle", typeof(Style), typeof(ColorPicker));
		DisplayColorAndNameProperty = DependencyProperty.Register("DisplayColorAndName", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(false));
		ColorModeProperty = DependencyProperty.Register("ColorMode", typeof(ColorMode), typeof(ColorPicker), new UIPropertyMetadata(ColorMode.ColorPalette));
		IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(false, OnIsOpenChanged));
		MaxDropDownWidthProperty = DependencyProperty.Register("MaxDropDownWidth", typeof(double), typeof(ColorPicker), new UIPropertyMetadata(214.0));
		RecentColorsProperty = DependencyProperty.Register("RecentColors", typeof(ObservableCollection<ColorItem>), typeof(ColorPicker), new UIPropertyMetadata(null));
		RecentColorsHeaderProperty = DependencyProperty.Register("RecentColorsHeader", typeof(string), typeof(ColorPicker), new UIPropertyMetadata("Recent Colors"));
		SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color?), typeof(ColorPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorPropertyChanged));
		SelectedColorTextProperty = DependencyProperty.Register("SelectedColorText", typeof(string), typeof(ColorPicker), new UIPropertyMetadata(""));
		ShowTabHeadersProperty = DependencyProperty.Register("ShowTabHeaders", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(true));
		ShowAvailableColorsProperty = DependencyProperty.Register("ShowAvailableColors", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(true));
		ShowRecentColorsProperty = DependencyProperty.Register("ShowRecentColors", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(false));
		ShowStandardColorsProperty = DependencyProperty.Register("ShowStandardColors", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(true));
		ShowDropDownButtonProperty = DependencyProperty.Register("ShowDropDownButton", typeof(bool), typeof(ColorPicker), new UIPropertyMetadata(true));
		StandardButtonHeaderProperty = DependencyProperty.Register("StandardButtonHeader", typeof(string), typeof(ColorPicker), new UIPropertyMetadata("Standard"));
		StandardColorsProperty = DependencyProperty.Register("StandardColors", typeof(ObservableCollection<ColorItem>), typeof(ColorPicker), new UIPropertyMetadata(CreateStandardColors()));
		StandardColorsHeaderProperty = DependencyProperty.Register("StandardColorsHeader", typeof(string), typeof(ColorPicker), new UIPropertyMetadata("Standard Colors"));
		UsingAlphaChannelProperty = DependencyProperty.Register("UsingAlphaChannel", typeof(bool), typeof(ColorPicker), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnUsingAlphaChannelPropertyChanged));
		SelectedColorChangedEvent = EventManager.RegisterRoutedEvent("SelectedColorChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<Color?>), typeof(ColorPicker));
		OpenedEvent = EventManager.RegisterRoutedEvent("OpenedEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorPicker));
		ClosedEvent = EventManager.RegisterRoutedEvent("ClosedEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorPicker));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker)));
	}

	public ColorPicker()
	{
		SetCurrentValue(RecentColorsProperty, new ObservableCollection<ColorItem>());
		Keyboard.AddKeyDownHandler(this, OnKeyDown);
		Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(this, OnMouseDownOutsideCapturedElement);
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_availableColors != null)
		{
			_availableColors.SelectionChanged -= Color_SelectionChanged;
		}
		_availableColors = GetTemplateChild("PART_AvailableColors") as ListBox;
		if (_availableColors != null)
		{
			_availableColors.SelectionChanged += Color_SelectionChanged;
		}
		if (_standardColors != null)
		{
			_standardColors.SelectionChanged -= Color_SelectionChanged;
		}
		_standardColors = GetTemplateChild("PART_StandardColors") as ListBox;
		if (_standardColors != null)
		{
			_standardColors.SelectionChanged += Color_SelectionChanged;
		}
		if (_recentColors != null)
		{
			_recentColors.SelectionChanged -= Color_SelectionChanged;
		}
		_recentColors = GetTemplateChild("PART_RecentColors") as ListBox;
		if (_recentColors != null)
		{
			_recentColors.SelectionChanged += Color_SelectionChanged;
		}
		if (_popup != null)
		{
			_popup.Opened -= Popup_Opened;
		}
		_popup = GetTemplateChild("PART_ColorPickerPalettePopup") as Popup;
		if (_popup != null)
		{
			_popup.Opened += Popup_Opened;
		}
		_toggleButton = Template.FindName("PART_ColorPickerToggleButton", this) as ToggleButton;
	}

	protected override void OnMouseUp(MouseButtonEventArgs e)
	{
		base.OnMouseUp(e);
		if (_selectionChanged)
		{
			CloseColorPicker(isFocusOnColorPicker: true);
			_selectionChanged = false;
		}
	}

	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		if (!IsOpen)
		{
			if (KeyboardUtilities.IsKeyModifyingPopupState(e))
			{
				IsOpen = true;
				e.Handled = true;
			}
		}
		else if (KeyboardUtilities.IsKeyModifyingPopupState(e))
		{
			CloseColorPicker(isFocusOnColorPicker: true);
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			SelectedColor = _initialColor;
			CloseColorPicker(isFocusOnColorPicker: true);
			e.Handled = true;
		}
	}

	private void OnMouseDownOutsideCapturedElement(object sender, MouseButtonEventArgs e)
	{
		CloseColorPicker(isFocusOnColorPicker: true);
	}

	private void Color_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		ListBox listBox = (ListBox)sender;
		if (e.AddedItems.Count > 0)
		{
			ColorItem colorItem = (ColorItem)e.AddedItems[0];
			SelectedColor = colorItem.Color;
			if (!string.IsNullOrEmpty(colorItem.Name))
			{
				SelectedColorText = colorItem.Name;
			}
			UpdateRecentColors(colorItem);
			_selectionChanged = true;
			listBox.SelectedIndex = -1;
		}
	}

	private void Popup_Opened(object sender, EventArgs e)
	{
		if (_availableColors != null && ShowAvailableColors)
		{
			FocusOnListBoxItem(_availableColors);
		}
		else if (_standardColors != null && ShowStandardColors)
		{
			FocusOnListBoxItem(_standardColors);
		}
		else if (_recentColors != null && ShowRecentColors)
		{
			FocusOnListBoxItem(_recentColors);
		}
	}

	private void FocusOnListBoxItem(ListBox listBox)
	{
		ListBoxItem listBoxItem = (ListBoxItem)listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem);
		if (listBoxItem == null && listBox.Items.Count > 0)
		{
			listBoxItem = (ListBoxItem)listBox.ItemContainerGenerator.ContainerFromItem(listBox.Items[0]);
		}
		listBoxItem?.Focus();
	}

	private void CloseColorPicker(bool isFocusOnColorPicker)
	{
		if (IsOpen)
		{
			IsOpen = false;
		}
		ReleaseMouseCapture();
		if (isFocusOnColorPicker && _toggleButton != null)
		{
			_toggleButton.Focus();
		}
		UpdateRecentColors(new ColorItem(SelectedColor, SelectedColorText));
	}

	private void UpdateRecentColors(ColorItem colorItem)
	{
		if (!RecentColors.Contains(colorItem))
		{
			RecentColors.Add(colorItem);
		}
		if (RecentColors.Count > 10)
		{
			RecentColors.RemoveAt(0);
		}
	}

	private string GetFormatedColorString(Color? colorToFormat)
	{
		if (!colorToFormat.HasValue || !colorToFormat.HasValue)
		{
			return string.Empty;
		}
		return ColorUtilities.FormatColorString(colorToFormat.Value.GetColorName(), UsingAlphaChannel);
	}

	private static ObservableCollection<ColorItem> CreateStandardColors()
	{
		return new ObservableCollection<ColorItem>
		{
			new ColorItem(Colors.Transparent, "Transparent"),
			new ColorItem(Colors.White, "White"),
			new ColorItem(Colors.Gray, "Gray"),
			new ColorItem(Colors.Black, "Black"),
			new ColorItem(Colors.Red, "Red"),
			new ColorItem(Colors.Green, "Green"),
			new ColorItem(Colors.Blue, "Blue"),
			new ColorItem(Colors.Yellow, "Yellow"),
			new ColorItem(Colors.Orange, "Orange"),
			new ColorItem(Colors.Purple, "Purple")
		};
	}

	private static ObservableCollection<ColorItem> CreateAvailableColors()
	{
		ObservableCollection<ColorItem> observableCollection = new ObservableCollection<ColorItem>();
		foreach (KeyValuePair<string, Color> knownColor in ColorUtilities.KnownColors)
		{
			if (!string.Equals(knownColor.Key, "Transparent"))
			{
				ColorItem item = new ColorItem(knownColor.Value, knownColor.Key);
				if (!observableCollection.Contains(item))
				{
					observableCollection.Add(item);
				}
			}
		}
		return observableCollection;
	}
}
