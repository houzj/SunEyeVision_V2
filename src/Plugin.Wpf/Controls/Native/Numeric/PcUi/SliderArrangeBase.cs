using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

public class SliderArrangeBase : UserControl, INotifyPropertyChanged
{
	public delegate void StartValueChange(object sender, RoutedPropertyChangedEventArgs<double> e);

	public delegate void EndValueChange(object sender, RoutedPropertyChangedEventArgs<double> e);

	public delegate void LeftTypeChange(object sender, RoutedPropertyChangedEventArgs<double> e);

	public delegate void RightTypeChange(object sender, RoutedPropertyChangedEventArgs<double> e);

	protected bool isBeside = true;

	protected bool isBetween = false;

	protected bool IsThrough = false;

	protected bool LoadAlready = false;

	protected int d = 5;

	protected NumericControl T1 = null;

	protected NumericControl T2 = null;

	protected static int _width = 150;

	protected static int _height = 40;

	private static double _min = 0.0;

	private static double _max = 100.0;

	protected static double _freq = 1.0;

	protected static SliderArrangeType _SliderType = SliderArrangeType.min2value;

	protected static Rect _rect = new Rect(0.0, 0.0, _width, _height);

	public static readonly DependencyProperty DragingChangedCommandProperty = DependencyProperty.Register("DragingChangedCommand", typeof(ICommand), typeof(SliderArrangeBase), new PropertyMetadata(null));

	public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(SliderArrangeBase), new PropertyMetadata(false));

	public static readonly DependencyProperty TBtnBgNormalProperty = DependencyProperty.Register("TBtnBgNormal", typeof(string), typeof(SliderArrangeBase), new PropertyMetadata("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Numeric/VM.PcUI/combo_ic_top_normal.png"));

	public static readonly DependencyProperty TBtnBgHoverProperty = DependencyProperty.Register("TBtnBgHover", typeof(string), typeof(SliderArrangeBase), new PropertyMetadata("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Numeric/VM.PcUI/combo_ic_top_click.png"));

	public static readonly DependencyProperty TBorderBrushProperty = DependencyProperty.Register("TBorderBrush", typeof(Brush), typeof(SliderArrangeBase), new PropertyMetadata((Brush)new BrushConverter().ConvertFromString("#CCCCCC")));

	public static readonly DependencyProperty TBackgroundProperty = DependencyProperty.Register("TBackground", typeof(Brush), typeof(SliderArrangeBase), new PropertyMetadata((Brush)new BrushConverter().ConvertFromString("#FCFCFC")));

	public static readonly DependencyProperty StartRectProperty = DependencyProperty.Register("StartRect", typeof(Rect), typeof(SliderArrangeBase), new PropertyMetadata(_rect));

	public static readonly DependencyProperty EndRectProperty = DependencyProperty.Register("EndRect", typeof(Rect), typeof(SliderArrangeBase), new PropertyMetadata(_rect));

	public static readonly DependencyProperty SliderTickFrequencyProperty = DependencyProperty.Register("SliderTickFrequency", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_freq));

	public static readonly DependencyProperty SilderHeightProperty = DependencyProperty.Register("SilderHeight", typeof(int), typeof(SliderArrangeBase), new PropertyMetadata(_height));

	public static readonly DependencyProperty SilderHeightEProperty = DependencyProperty.Register("SilderHeightE", typeof(int), typeof(SliderArrangeBase), new PropertyMetadata(_height));

	public static readonly DependencyProperty SilderWidthProperty = DependencyProperty.Register("SilderWidth", typeof(int), typeof(SliderArrangeBase), new PropertyMetadata(_width));

	public static readonly DependencyProperty SliderVisibilityProperty = DependencyProperty.Register("SliderVisibility", typeof(Visibility), typeof(SliderArrangeBase), new PropertyMetadata(Visibility.Visible));

	public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_min));

	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_max, MaxChanged));

	public static readonly DependencyProperty MinimumLeftProperty = DependencyProperty.Register("MinimumLeft", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_min));

	public static readonly DependencyProperty MaximumLeftProperty = DependencyProperty.Register("MaximumLeft", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_max));

	public static readonly DependencyProperty MinimumRightProperty = DependencyProperty.Register("MinimumRight", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_min));

	public static readonly DependencyProperty MaximumRightProperty = DependencyProperty.Register("MaximumRight", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_max));

	public static readonly DependencyProperty IsNeedCheckExtremumByXMLProperty = DependencyProperty.Register("IsNeedCheckExtremumByXML", typeof(bool), typeof(SliderArrangeBase), new PropertyMetadata(false));

	public static readonly DependencyProperty IsSubscribeLeftProperty = DependencyProperty.Register("IsSubscribeLeft", typeof(bool), typeof(SliderArrangeBase), new PropertyMetadata(false, SubscribeLeftCallBack));

	public static readonly DependencyProperty IsSubscribeRightProperty = DependencyProperty.Register("IsSubscribeRight", typeof(bool), typeof(SliderArrangeBase), new PropertyMetadata(false, SubscribeRightCallBack));

	public static readonly DependencyProperty SubscribeTemplateProperty = DependencyProperty.Register("SubscribeTemplate", typeof(DataTemplate), typeof(SliderArrangeBase), new PropertyMetadata(null));

	public static readonly DependencyProperty SubscribeDataLeftProperty = DependencyProperty.Register("SubscribeDataLeft", typeof(object), typeof(SliderArrangeBase), new PropertyMetadata(null));

	public static readonly DependencyProperty SubscribeDataRightProperty = DependencyProperty.Register("SubscribeDataRight", typeof(object), typeof(SliderArrangeBase), new PropertyMetadata(null));

	public static readonly DependencyProperty IsSubscribeVisibleProperty = DependencyProperty.Register("IsSubscribeVisible", typeof(Visibility), typeof(SliderArrangeBase), new PropertyMetadata(Visibility.Collapsed));

	public static readonly DependencyProperty IsDynamicValueProperty = DependencyProperty.Register("IsDynamicValue", typeof(bool), typeof(SliderArrangeBase), new PropertyMetadata(false));

	public static readonly DependencyProperty StartValueProperty = DependencyProperty.Register("StartValue", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(0.0, StartValueChangeCallBack));

	public static readonly DependencyProperty EndValueProperty = DependencyProperty.Register("EndValue", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_max, EndValueChangeCallBack));

	private static readonly DependencyProperty StartValueEProperty = DependencyProperty.Register("StartValueE", typeof(double), typeof(SliderArrangeBase));

	private static readonly DependencyProperty EndValueEProperty = DependencyProperty.Register("EndValueE", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_max));

	private static readonly DependencyProperty StartValueFProperty = DependencyProperty.Register("StartValueF", typeof(double), typeof(SliderArrangeBase));

	private static readonly DependencyProperty EndValueFProperty = DependencyProperty.Register("EndValueF", typeof(double), typeof(SliderArrangeBase), new PropertyMetadata(_max));

	public static readonly DependencyProperty SilderArrangeTypeProperty = DependencyProperty.Register("SilderArrangeTypeProperty", typeof(SliderArrangeType), typeof(SliderArrangeBase), new PropertyMetadata(_SliderType, OnTypeChange));

	private SymbolType _LeftSymbol = SymbolType.None;

	private SymbolType _RightSymbol = SymbolType.None;

	private Visibility _isLeftSliderVisible = Visibility.Visible;

	private Visibility _isRightSliderVisible = Visibility.Visible;

	private Visibility _isVisibleOfChild = Visibility.Visible;

	private Visibility _isVisibleOfLable = Visibility.Visible;

	private GridLength _Gr0 = new GridLength(0.0, GridUnitType.Star);

	private GridLength _Gr1 = new GridLength(100.0, GridUnitType.Star);

	private GridLength _Gr2 = new GridLength(0.0, GridUnitType.Star);

	private Geometry _StartRectClip = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	private Geometry _EndRectClip = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	public ICommand DragingChangedCommand
	{
		get
		{
			return (ICommand)GetValue(DragingChangedCommandProperty);
		}
		set
		{
			SetValue(DragingChangedCommandProperty, value);
		}
	}

	public bool IsReadOnly
	{
		get
		{
			return (bool)GetValue(IsReadOnlyProperty);
		}
		set
		{
			SetValue(IsReadOnlyProperty, value);
		}
	}

	public string TBtnBgNormal
	{
		get
		{
			return (string)GetValue(TBtnBgNormalProperty);
		}
		set
		{
			SetValue(TBtnBgNormalProperty, value);
		}
	}

	public string TBtnBgHover
	{
		get
		{
			return (string)GetValue(TBtnBgHoverProperty);
		}
		set
		{
			SetValue(TBtnBgHoverProperty, value);
		}
	}

	public Brush TBorderBrush
	{
		get
		{
			return (Brush)GetValue(TBorderBrushProperty);
		}
		set
		{
			SetValue(TBorderBrushProperty, value);
		}
	}

	public Brush TBackground
	{
		get
		{
			return (Brush)GetValue(TBackgroundProperty);
		}
		set
		{
			SetValue(TBackgroundProperty, value);
		}
	}

	public Rect StartRect
	{
		get
		{
			return (Rect)GetValue(StartRectProperty);
		}
		set
		{
			SetValue(StartRectProperty, value);
		}
	}

	public Rect EndRect
	{
		get
		{
			return (Rect)GetValue(EndRectProperty);
		}
		set
		{
			SetValue(EndRectProperty, value);
		}
	}

	public double SliderTickFrequency
	{
		get
		{
			return (double)GetValue(SliderTickFrequencyProperty);
		}
		set
		{
			SetValue(SliderTickFrequencyProperty, value);
		}
	}

	public int SilderHeight
	{
		get
		{
			return SilderHeightE = (int)GetValue(SilderHeightProperty);
		}
		set
		{
			SetValue(SilderHeightProperty, value);
		}
	}

	public int SilderHeightE
	{
		get
		{
			return (int)GetValue(SilderHeightEProperty);
		}
		set
		{
			SetValue(SilderHeightEProperty, value * 2);
		}
	}

	public int SilderWidth
	{
		get
		{
			return (int)GetValue(SilderWidthProperty);
		}
		set
		{
			SetValue(SilderWidthProperty, value);
		}
	}

	public Visibility SliderVisibility
	{
		get
		{
			return (Visibility)GetValue(SliderVisibilityProperty);
		}
		set
		{
			SetValue(SliderVisibilityProperty, value);
		}
	}

	public double Minimum
	{
		get
		{
			return (double)GetValue(MinimumProperty);
		}
		set
		{
			SetValue(MinimumProperty, value);
		}
	}

	public double Maximum
	{
		get
		{
			return (double)GetValue(MaximumProperty);
		}
		set
		{
			SetValue(MaximumProperty, value);
		}
	}

	public double MinimumLeft
	{
		get
		{
			return (double)GetValue(MinimumLeftProperty);
		}
		set
		{
			SetValue(MinimumLeftProperty, value);
		}
	}

	public double MaximumLeft
	{
		get
		{
			return (double)GetValue(MaximumLeftProperty);
		}
		set
		{
			SetValue(MaximumLeftProperty, value);
		}
	}

	public double MinimumRight
	{
		get
		{
			return (double)GetValue(MinimumRightProperty);
		}
		set
		{
			SetValue(MinimumRightProperty, value);
		}
	}

	public double MaximumRight
	{
		get
		{
			return (double)GetValue(MaximumRightProperty);
		}
		set
		{
			SetValue(MaximumRightProperty, value);
		}
	}

	public bool IsNeedCheckExtremumByXML
	{
		get
		{
			return (bool)GetValue(IsNeedCheckExtremumByXMLProperty);
		}
		set
		{
			SetValue(IsNeedCheckExtremumByXMLProperty, value);
		}
	}

	public bool IsSubscribeLeft
	{
		get
		{
			return (bool)GetValue(IsSubscribeLeftProperty);
		}
		set
		{
			SetValue(IsSubscribeLeftProperty, value);
		}
	}

	public bool IsSubscribeRight
	{
		get
		{
			return (bool)GetValue(IsSubscribeRightProperty);
		}
		set
		{
			SetValue(IsSubscribeRightProperty, value);
		}
	}

	public DataTemplate SubscribeTemplate
	{
		get
		{
			return (DataTemplate)GetValue(SubscribeTemplateProperty);
		}
		set
		{
			SetValue(SubscribeTemplateProperty, value);
		}
	}

	public object SubscribeDataLeft
	{
		get
		{
			return GetValue(SubscribeDataLeftProperty);
		}
		set
		{
			SetValue(SubscribeDataLeftProperty, value);
		}
	}

	public object SubscribeDataRight
	{
		get
		{
			return GetValue(SubscribeDataRightProperty);
		}
		set
		{
			SetValue(SubscribeDataRightProperty, value);
		}
	}

	public Visibility IsSubscribeVisible
	{
		get
		{
			return (Visibility)GetValue(IsSubscribeVisibleProperty);
		}
		set
		{
			SetValue(IsSubscribeVisibleProperty, value);
		}
	}

	public bool IsDynamicValue
	{
		get
		{
			return (bool)GetValue(IsDynamicValueProperty);
		}
		set
		{
			SetValue(IsDynamicValueProperty, value);
		}
	}

	public double StartValue
	{
		get
		{
			return StartValueF = (StartValueE = (double)GetValue(StartValueProperty));
		}
		set
		{
			SetValue(StartValueProperty, value);
		}
	}

	public double EndValue
	{
		get
		{
			return EndValueE = (EndValueF = (double)GetValue(EndValueProperty));
		}
		set
		{
			SetValue(EndValueProperty, value);
		}
	}

	private double StartValueE
	{
		get
		{
			return (double)GetValue(StartValueEProperty);
		}
		set
		{
			if (isBetween)
			{
				SetValue(StartValueEProperty, value);
			}
			else
			{
				SetValue(StartValueEProperty, Minimum);
			}
		}
	}

	private double EndValueE
	{
		get
		{
			return (double)GetValue(EndValueEProperty);
		}
		set
		{
			if (isBetween)
			{
				SetValue(EndValueEProperty, value);
			}
			else
			{
				SetValue(EndValueEProperty, (double)GetValue(StartValueProperty));
			}
		}
	}

	private double StartValueF
	{
		get
		{
			return (double)GetValue(StartValueFProperty);
		}
		set
		{
			if (isBetween)
			{
				SetValue(StartValueFProperty, value);
			}
			else
			{
				SetValue(StartValueFProperty, (double)GetValue(EndValueProperty));
			}
		}
	}

	private double EndValueF
	{
		get
		{
			return (double)GetValue(EndValueFProperty);
		}
		set
		{
			if (isBetween)
			{
				SetValue(EndValueFProperty, value);
			}
			else
			{
				SetValue(EndValueFProperty, Maximum);
			}
		}
	}

	public SliderArrangeType SliderType
	{
		get
		{
			return (SliderArrangeType)GetValue(SilderArrangeTypeProperty);
		}
		set
		{
			SetValue(SilderArrangeTypeProperty, value);
		}
	}

	public SymbolType LeftSymbol
	{
		get
		{
			return _LeftSymbol;
		}
		set
		{
			if (_LeftSymbol != value)
			{
				_LeftSymbol = value;
				OnPropertyChanged("LeftSymbol");
			}
		}
	}

	public SymbolType RightSymbol
	{
		get
		{
			return _RightSymbol;
		}
		set
		{
			if (_RightSymbol != value)
			{
				_RightSymbol = value;
				OnPropertyChanged("RightSymbol");
			}
		}
	}

	public Visibility IsLeftSliderVisible
	{
		get
		{
			return _isLeftSliderVisible;
		}
		set
		{
			if (_isLeftSliderVisible != value)
			{
				_isLeftSliderVisible = value;
				OnPropertyChanged("IsLeftSliderVisible");
			}
		}
	}

	public Visibility IsRightSliderVisible
	{
		get
		{
			return _isRightSliderVisible;
		}
		set
		{
			if (_isRightSliderVisible != value)
			{
				_isRightSliderVisible = value;
				OnPropertyChanged("IsRightSliderVisible");
			}
		}
	}

	public Visibility IsVisibleOfChild
	{
		get
		{
			return _isVisibleOfChild;
		}
		set
		{
			if (_isVisibleOfChild != value)
			{
				_isVisibleOfChild = value;
				OnPropertyChanged("IsVisibleOfChild");
			}
		}
	}

	public Visibility IsVisibleOfLable
	{
		get
		{
			return _isVisibleOfLable;
		}
		set
		{
			if (_isVisibleOfLable != value)
			{
				_isVisibleOfLable = value;
				OnPropertyChanged("IsVisibleOfLable");
			}
		}
	}

	public GridLength Gr0
	{
		get
		{
			return _Gr0;
		}
		set
		{
			if (_Gr0 != value)
			{
				_Gr0 = value;
				OnPropertyChanged("Gr0");
			}
		}
	}

	public GridLength Gr1
	{
		get
		{
			return _Gr1;
		}
		set
		{
			if (_Gr1 != value)
			{
				_Gr1 = value;
				OnPropertyChanged("Gr1");
			}
		}
	}

	public GridLength Gr2
	{
		get
		{
			return _Gr2;
		}
		set
		{
			if (_Gr2 != value)
			{
				_Gr2 = value;
				OnPropertyChanged("Gr2");
			}
		}
	}

	public Geometry StartRectClip
	{
		get
		{
			return _StartRectClip;
		}
		set
		{
			_StartRectClip = value;
			OnPropertyChanged("StartRectClip");
		}
	}

	public Geometry EndRectClip
	{
		get
		{
			return _EndRectClip;
		}
		set
		{
			_EndRectClip = value;
			OnPropertyChanged("EndRectClip");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event StartValueChange OnStartValueChange;

	public event EndValueChange OnEndValueChange;

	public event LeftTypeChange OnLeftTypeChange;

	public event RightTypeChange OnRightTypeChange;

	protected virtual void OnPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public SliderArrangeBase()
	{
		Loaded += SliderArrangeBase_Loaded;
	}

	private void SliderArrangeBase_Loaded(object sender, RoutedEventArgs e)
	{
		LoadAlready = true;
		InitUI();
		if (!isBeside && IsSubscribeRight != IsSubscribeLeft)
		{
			isBeside = true;
		}
		ChangedSliderVisibility();
		ClipSilder();
	}

	protected virtual void ChangedSliderVisibility()
	{
	}

	private static void MaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SliderArrangeBase).ClipSilder();
	}

	private static void SubscribeLeftCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is bool value)
		{
			(d as SliderArrangeBase).DoSubscribeLeftCallBack(value);
		}
	}

	private void DoSubscribeLeftCallBack(bool value)
	{
		if (value)
		{
			IsLeftSliderVisible = Visibility.Collapsed;
			SliderVisibility = Visibility.Collapsed;
			if (isBetween)
			{
				if (IsSubscribeRight)
				{
					isBeside = false;
				}
				else
				{
					isBeside = true;
				}
			}
			return;
		}
		IsLeftSliderVisible = Visibility.Visible;
		if (IsSubscribeRight)
		{
			SliderVisibility = Visibility.Collapsed;
		}
		else
		{
			SliderVisibility = Visibility.Visible;
		}
		if (isBetween)
		{
			if (IsSubscribeRight)
			{
				isBeside = true;
			}
			else
			{
				isBeside = false;
			}
		}
	}

	private static void SubscribeRightCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is bool value)
		{
			(d as SliderArrangeBase).DoSubscribeRightCallBack(value);
		}
	}

	private void DoSubscribeRightCallBack(bool value)
	{
		if (!isBetween)
		{
			return;
		}
		if (value)
		{
			IsRightSliderVisible = Visibility.Collapsed;
			SliderVisibility = Visibility.Collapsed;
			if (IsSubscribeLeft)
			{
				isBeside = false;
			}
			else
			{
				isBeside = true;
			}
			return;
		}
		IsRightSliderVisible = Visibility.Visible;
		if (IsSubscribeLeft)
		{
			isBeside = true;
			SliderVisibility = Visibility.Collapsed;
		}
		else
		{
			isBeside = false;
			SliderVisibility = Visibility.Visible;
		}
	}

	private static void StartValueChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SliderArrangeBase).DoStartValueChangeCallBack();
	}

	private void DoStartValueChangeCallBack()
	{
		if (this is SliderArrange)
		{
			StartValue = Math.Round(StartValue);
		}
		StartValueE = StartValue;
		StartValueF = StartValue;
		if (IsDynamicValue)
		{
			DoStartValueChange();
		}
	}

	private static void EndValueChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SliderArrangeBase).DoEndValueChangeCallBack();
	}

	private void DoEndValueChangeCallBack()
	{
		if (this is SliderArrange)
		{
			EndValue = Math.Round(EndValue);
		}
		EndValueF = EndValue;
		EndValueE = EndValue;
	}

	protected void ClipSilder()
	{
		if (!LoadAlready)
		{
			return;
		}
		double num = EndValue / 2.0 + StartValue / 2.0;
		double num2 = Maximum - Minimum;
		double num3 = Math.Abs((double)(SilderWidth - 2 * d) * (num - Minimum) / num2 + (double)d);
		if (!IsThrough)
		{
			if (!isBeside)
			{
				StartRectClip = new RectangleGeometry(new Rect(0.0, 0.0, num3, SilderHeight));
			}
			else
			{
				StartRectClip = new RectangleGeometry(new Rect(0.0, 0.0, SilderWidth, SilderHeight));
			}
			if (isBeside)
			{
				EndRectClip = new RectangleGeometry(new Rect(0.0, 0.0, SilderWidth, SilderHeight));
			}
			else
			{
				EndRectClip = new RectangleGeometry(new Rect(num3, 0.0, SilderWidth, SilderHeight));
			}
		}
		else
		{
			if (!isBeside)
			{
				StartRectClip = new RectangleGeometry(new Rect(num3, 0.0, SilderWidth, SilderHeight));
			}
			else
			{
				StartRectClip = new RectangleGeometry(new Rect(0.0, 0.0, SilderWidth, SilderHeight));
			}
			EndRectClip = new RectangleGeometry(new Rect(0.0, 0.0, num3, SilderHeight));
		}
	}

	private static void OnTypeChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SliderArrangeBase sliderArrangeBase)
		{
			switch ((SliderArrangeType)e.NewValue)
			{
			case SliderArrangeType.min2value:
				sliderArrangeBase.LeftSymbol = SymbolType.None;
				sliderArrangeBase.isBeside = true;
				sliderArrangeBase.isBetween = false;
				break;
			case SliderArrangeType.value2max:
				sliderArrangeBase.LeftSymbol = SymbolType.None;
				sliderArrangeBase.isBeside = true;
				sliderArrangeBase.isBetween = true;
				break;
			case SliderArrangeType.between:
				sliderArrangeBase.LeftSymbol = SymbolType.None;
				sliderArrangeBase.RightSymbol = SymbolType.None;
				sliderArrangeBase.isBeside = false;
				sliderArrangeBase.isBetween = true;
				break;
			case SliderArrangeType.outside:
				sliderArrangeBase.LeftSymbol = SymbolType.Less;
				sliderArrangeBase.RightSymbol = SymbolType.Greater;
				sliderArrangeBase.isBeside = false;
				sliderArrangeBase.isBetween = false;
				break;
			case SliderArrangeType.bothWay:
				sliderArrangeBase.LeftSymbol = SymbolType.Less;
				sliderArrangeBase.RightSymbol = SymbolType.Greater;
				sliderArrangeBase.isBeside = false;
				sliderArrangeBase.isBetween = false;
				break;
			}
			sliderArrangeBase.InitUI();
		}
	}

	public void InitUI()
	{
		if (!isBetween)
		{
			IsVisibleOfLable = Visibility.Collapsed;
			Gr0 = new GridLength(45.0, GridUnitType.Star);
			Gr1 = new GridLength(10.0, GridUnitType.Star);
			Gr2 = new GridLength(45.0, GridUnitType.Star);
		}
		else
		{
			IsVisibleOfLable = Visibility.Visible;
			Gr0 = new GridLength(40.0, GridUnitType.Star);
			Gr1 = new GridLength(8.0, GridUnitType.Star);
			Gr2 = new GridLength(40.0, GridUnitType.Star);
		}
		if (isBeside)
		{
			IsVisibleOfLable = Visibility.Collapsed;
			IsVisibleOfChild = Visibility.Collapsed;
			IsRightSliderVisible = Visibility.Collapsed;
			Gr0 = new GridLength(100.0, GridUnitType.Star);
			Gr1 = new GridLength(0.0, GridUnitType.Star);
			Gr2 = new GridLength(0.0, GridUnitType.Star);
		}
		else
		{
			IsVisibleOfChild = Visibility.Visible;
			if (IsSubscribeRight)
			{
				IsRightSliderVisible = Visibility.Collapsed;
			}
		}
		if (IsSubscribeLeft)
		{
			IsLeftSliderVisible = Visibility.Collapsed;
		}
	}

	protected void SL_Bat1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (SliderType == SliderArrangeType.bothWay)
			{
				if (e.NewValue > EndValue)
				{
					IsThrough = true;
				}
				else
				{
					IsThrough = false;
				}
			}
			else if (SliderType != SliderArrangeType.min2value && SliderType != SliderArrangeType.value2max && e.NewValue > EndValue)
			{
				StartValue = EndValue;
			}
			if (SliderType != SliderArrangeType.min2value && SliderType != SliderArrangeType.value2max)
			{
				if (e.NewValue < MinimumLeft)
				{
					StartValue = MinimumLeft;
				}
				if (e.NewValue > MaximumLeft)
				{
					StartValue = MaximumLeft;
				}
			}
			ClipSilder();
		}));
	}

	protected void SL_Bat2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (SliderType == SliderArrangeType.bothWay)
			{
				if (e.NewValue < StartValue)
				{
					IsThrough = true;
				}
				else
				{
					IsThrough = false;
				}
			}
			else if (SliderType != SliderArrangeType.min2value && SliderType != SliderArrangeType.value2max && EndValue < StartValue)
			{
				EndValue = StartValue;
			}
			if (SliderType != SliderArrangeType.min2value && SliderType != SliderArrangeType.value2max)
			{
				if (e.NewValue < MinimumRight)
				{
					EndValue = MinimumRight;
				}
				if (e.NewValue > MaximumRight)
				{
					EndValue = MaximumRight;
				}
			}
			ClipSilder();
		}));
	}

	protected void DoStartValueChange()
	{
		if (OnStartValueChange != null)
		{
			OnStartValueChange(this, null);
		}
	}

	protected void DoEndValueChange()
	{
		if (OnEndValueChange != null)
		{
			OnEndValueChange(this, null);
		}
	}

	protected void DoLeftTypeChange()
	{
		if (OnLeftTypeChange != null)
		{
			OnLeftTypeChange(this, null);
		}
	}

	protected void DoRightTypeChange()
	{
		if (OnRightTypeChange != null)
		{
			OnRightTypeChange(this, null);
		}
	}

	protected void Thumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragingChangedCommand?.Execute(true);
	}

	protected void Thumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		DoValueChange(sender);
	}

	protected void RepeatButton_Click(object sender, RoutedEventArgs e)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (!isBetween || !isBeside)
			{
				DoValueChange(sender);
			}
		}));
	}

	private void DoValueChange(object sender)
	{
		if ((sender as FrameworkElement).Tag is FrameworkElement frameworkElement)
		{
			if (frameworkElement.Name == "SL_Bat1")
			{
				DoStartValueChange();
			}
			else
			{
				DoEndValueChange();
			}
		}
	}

	protected virtual void T1_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is NumericControl t)
		{
			T1 = t;
		}
	}

	protected virtual void T2_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is NumericControl t)
		{
			T2 = t;
		}
	}

	protected void T1_LostFocus(object sender, RoutedEventArgs e)
	{
		if (isBetween && StartValue > EndValue)
		{
			StartValue = EndValue;
		}
		DoStartValueChange();
	}

	protected void T2_LostFocus(object sender, RoutedEventArgs e)
	{
		if (isBetween && StartValue > EndValue)
		{
			EndValue = StartValue;
		}
		DoEndValueChange();
	}

	private void T1_OnNumericClick(object sender, EventArgs e)
	{
		if (isBetween && StartValue > EndValue)
		{
			StartValue = EndValue;
		}
		DoStartValueChange();
	}

	private void T2_OnNumericClick(object sender, EventArgs e)
	{
		if (isBetween && StartValue > EndValue)
		{
			EndValue = StartValue;
		}
		DoEndValueChange();
	}

	protected void TypeChangeLeft_Click(object sender, RoutedEventArgs e)
	{
		DoLeftTypeChange();
	}

	protected void TypeChangeRight_Click(object sender, RoutedEventArgs e)
	{
		DoRightTypeChange();
	}
}
