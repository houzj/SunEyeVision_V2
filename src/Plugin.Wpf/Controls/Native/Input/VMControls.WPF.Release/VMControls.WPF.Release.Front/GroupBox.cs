using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Release.Front;

public class GroupBox : System.Windows.Controls.GroupBox
{
	public static readonly DependencyProperty FitSizeProperty;

	/// <summary>
	/// 默认尺寸
	/// </summary>
	private System.Windows.Size _size = System.Windows.Size.Empty;

	private Font _font;

	public static readonly DependencyProperty TitleProperty;

	private bool _IsNeedLoadOrUnLoad = true;

	private bool _AutoStrechA;

	public static readonly DependencyProperty HeaderBackgroundProperty;

	private string cellAlign;

	/// <summary>
	/// 控件自适应
	/// </summary>
	public bool FitSize
	{
		get
		{
			return (bool)GetValue(FitSizeProperty);
		}
		set
		{
			SetValue(FitSizeProperty, value);
		}
	}

	/// <summary>
	/// 尺寸
	/// </summary>
	public System.Windows.Size Size
	{
		get
		{
			if (_size == System.Windows.Size.Empty)
			{
				_size = new System.Windows.Size((int)Width, (int)Height);
			}
			return _size;
		}
		set
		{
			Height = value.Height;
			Width = value.Width;
			_size = new System.Windows.Size((int)Width, (int)Height);
		}
	}

	public Font Font
	{
		get
		{
			return _font;
		}
		set
		{
			if (_font != null)
			{
				_font.Dispose();
			}
			_font = value;
			FontSize = _font.Size;
			FontWeight = (_font.Bold ? FontWeights.Bold : FontWeights.Normal);
			FontStyle = (_font.Italic ? FontStyles.Italic : FontStyles.Normal);
			FontFamily = new System.Windows.Media.FontFamily(_font.FontFamily.Name);
		}
	}

	public string Title
	{
		get
		{
			return (string)GetValue(TitleProperty);
		}
		set
		{
			SetValue(TitleProperty, value);
		}
	}

	/// <summary>
	/// 用于判断是否是切换tab页触发的Unload事件
	/// </summary>
	public bool IsNeedLoadOrUnLoad
	{
		get
		{
			return _IsNeedLoadOrUnLoad;
		}
		set
		{
			_IsNeedLoadOrUnLoad = value;
		}
	}

	public bool AutoStrechA
	{
		get
		{
			return _AutoStrechA;
		}
		set
		{
			_AutoStrechA = value;
		}
	}

	public System.Windows.Media.Brush HeaderBackground
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(HeaderBackgroundProperty);
		}
		set
		{
			SetValue(HeaderBackgroundProperty, value);
		}
	}

	public string CellAlign
	{
		get
		{
			return cellAlign;
		}
		set
		{
			if (!(cellAlign == value))
			{
				cellAlign = value;
				DoCellAlignChanged(cellAlign);
			}
		}
	}

	static GroupBox()
	{
		FitSizeProperty = DependencyProperty.Register("FitSize", typeof(bool), typeof(GroupBox), new PropertyMetadata(true));
		TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(GroupBox), new PropertyMetadata("GroupBox", OnTitleChanged));
		HeaderBackgroundProperty = DependencyProperty.Register("HeaderBackground", typeof(System.Windows.Media.Brush), typeof(GroupBox), new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromArgb(byte.MaxValue, 96, 96, 102))));
		FrameworkElement.StyleProperty.OverrideMetadata(typeof(GroupBox));
	}

	private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as GroupBox).Header = e.NewValue.ToString();
	}

	private void DoCellAlignChanged(string inputCellAlign)
	{
		if (inputCellAlign == null)
		{
			VerticalAlignment = VerticalAlignment.Top;
			HorizontalAlignment = HorizontalAlignment.Left;
		}
		else
		{
			IEnumerable<string> source = inputCellAlign.Split(';').Take(2);
			if (Enum.TryParse<VerticalAlignment>(source.First(), out var result))
			{
				VerticalAlignment = result;
			}
			if (Enum.TryParse<HorizontalAlignment>(source.Last(), out var result2))
			{
				HorizontalAlignment = result2;
			}
		}
		Thickness margin = Margin;
		if (HorizontalAlignment == HorizontalAlignment.Center)
		{
			double left = (margin.Right = 0.0);
			margin.Left = left;
		}
		if (VerticalAlignment == VerticalAlignment.Center)
		{
			double left = (margin.Bottom = 0.0);
			margin.Top = left;
		}
	}
}
