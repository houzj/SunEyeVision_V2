using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Apps.Data;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;

namespace FormatModuleControl;

public partial class FormatTextBox : UserControl, IComponentConnector
{
	public delegate void TBFoucsChanged(object sender, TextBoxFocusEventArgs e);

	public delegate void TBDoubleClick(object sender, TextBoxFocusEventArgs e);

	public static readonly DependencyProperty ActWidthValueProperty = DependencyProperty.Register("ActWidth", typeof(double), typeof(FormatTextBox), new PropertyMetadata(20.0));

	public static readonly DependencyProperty TargetValueProperty = DependencyProperty.Register("TargetValue", typeof(string), typeof(FormatTextBox), new PropertyMetadata(""));

	public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(FormatTextBox), new PropertyMetadata(true));

	public double ActWidth
	{
		get
		{
			return (double)GetValue(ActWidthValueProperty);
		}
		set
		{
			SetValue(ActWidthValueProperty, value);
		}
	}

	public string TargetValue
	{
		get
		{
			return (string)GetValue(TargetValueProperty);
		}
		set
		{
			SetValue(TargetValueProperty, value);
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

	public event TBFoucsChanged OnTBFoucsChanged;

	public event TBDoubleClick OnTBDoubleClick;

	public FormatTextBox()
	{
		InitializeComponent();
		tb.TextChanged += (object s, TextChangedEventArgs e) =>
		{
			DoFocusChange();
		};
	}

	private void DoFocusChange()
	{
		if (OnTBFoucsChanged != null)
		{
			TextBoxFocusEventArgs e = new TextBoxFocusEventArgs
			{
				Text = tb.Text,
				IsFocus = tb.IsFocused,
				SelectionStart = tb.CaretIndex
			};
			OnTBFoucsChanged(null, e);
		}
	}

	private void tb_SelectionChanged(object sender, RoutedEventArgs e)
	{
		DoFocusChange();
	}

	private void tb_TextChanged(object sender, TextChangedEventArgs e)
	{
		FormattedText formattedText = new FormattedText(tb.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), myControl.FontSize, Brushes.Black);
		double actWidth = 12.0 + formattedText.Width;
		ActWidth = actWidth;
	}

	private void tb_GotFocus(object sender, RoutedEventArgs e)
	{
		tb.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEDEDE"));
		if (DataContext is FormatTextInfo formatTextInfo)
		{
			formatTextInfo.MyItemState = ItemState.Normal;
			Format parentNode = formatTextInfo.GetParentNode<Format>();
			if (parentNode != null)
			{
				parentNode.CurrentRow = null;
				parentNode.CurrentInfo = formatTextInfo;
			}
		}
	}

	private void tb_LostFocus(object sender, RoutedEventArgs e)
	{
		tb.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECEC"));
	}

	private void myControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue != null || !(e.NewValue is FormatTextInfo))
		{
			return;
		}
		FormatTextInfo formatTextInfo = e.NewValue as FormatTextInfo;
		if (formatTextInfo.TextInfoType == TextType.Text && formatTextInfo.TempValue == string.Empty && formatTextInfo.IsAdd)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
			{
				Keyboard.Focus(tb);
			}));
			formatTextInfo.IsAdd = false;
		}
		else if (formatTextInfo.TextInfoType == TextType.Pair)
		{
			tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C48B4"));
		}
	}
}
