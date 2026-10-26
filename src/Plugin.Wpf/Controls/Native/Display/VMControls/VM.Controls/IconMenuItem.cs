using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class IconMenuItem : MenuItem
{
	private object _currentItem;

	private string _itemnamekey = "";

	public static readonly DependencyProperty ShowMaskProperty;

	public static readonly DependencyProperty NormalIconProperty;

	public static readonly DependencyProperty ClickIconProperty;

	public static readonly DependencyProperty HoverIconProperty;

	public static readonly DependencyProperty DisableIconProperty;

	public static readonly DependencyProperty TopMaskHoverProperty;

	public static readonly DependencyProperty TopMaskClickProperty;

	public static readonly DependencyProperty TopMaskNormalProperty;

	public static readonly DependencyProperty SubMaskHoverProperty;

	public static readonly DependencyProperty SubMaskClickProperty;

	public static readonly DependencyProperty SubMaskNormalProperty;

	public static readonly DependencyProperty PopupStyleProperty;

	public static readonly DependencyProperty ScrollViewerStyleProperty;

	public static readonly DependencyProperty IconPaddingProperty;

	public string ItemNameKey
	{
		get
		{
			return _itemnamekey;
		}
		set
		{
			if (value != _itemnamekey)
			{
				_itemnamekey = value;
			}
		}
	}

	public bool ShowMask
	{
		get
		{
			return (bool)GetValue(ShowMaskProperty);
		}
		set
		{
			SetValue(ShowMaskProperty, value);
		}
	}

	public ImageSource NormalIcon
	{
		get
		{
			return (ImageSource)GetValue(NormalIconProperty);
		}
		set
		{
			SetValue(NormalIconProperty, value);
		}
	}

	public ImageSource ClickIcon
	{
		get
		{
			return (ImageSource)GetValue(ClickIconProperty);
		}
		set
		{
			SetValue(ClickIconProperty, value);
		}
	}

	public ImageSource HoverIcon
	{
		get
		{
			return (ImageSource)GetValue(HoverIconProperty);
		}
		set
		{
			SetValue(HoverIconProperty, value);
		}
	}

	public ImageSource DisableIcon
	{
		get
		{
			return (ImageSource)GetValue(DisableIconProperty);
		}
		set
		{
			SetValue(DisableIconProperty, value);
		}
	}

	public Style TopMaskHover
	{
		get
		{
			return (Style)GetValue(TopMaskHoverProperty);
		}
		set
		{
			SetValue(TopMaskHoverProperty, value);
		}
	}

	public Style TopMaskClick
	{
		get
		{
			return (Style)GetValue(TopMaskClickProperty);
		}
		set
		{
			SetValue(TopMaskClickProperty, value);
		}
	}

	public Style TopMaskNormal
	{
		get
		{
			return (Style)GetValue(TopMaskNormalProperty);
		}
		set
		{
			SetValue(TopMaskNormalProperty, value);
		}
	}

	public Style SubMaskHover
	{
		get
		{
			return (Style)GetValue(SubMaskHoverProperty);
		}
		set
		{
			SetValue(SubMaskHoverProperty, value);
		}
	}

	public Style SubMaskClick
	{
		get
		{
			return (Style)GetValue(SubMaskClickProperty);
		}
		set
		{
			SetValue(SubMaskClickProperty, value);
		}
	}

	public Style SubMaskNormal
	{
		get
		{
			return (Style)GetValue(SubMaskNormalProperty);
		}
		set
		{
			SetValue(SubMaskNormalProperty, value);
		}
	}

	public Style PopupStyle
	{
		get
		{
			return (Style)GetValue(PopupStyleProperty);
		}
		set
		{
			SetValue(PopupStyleProperty, value);
		}
	}

	public Style ScrollViewerStyle
	{
		get
		{
			return (Style)GetValue(ScrollViewerStyleProperty);
		}
		set
		{
			SetValue(ScrollViewerStyleProperty, value);
		}
	}

	public Thickness IconPadding
	{
		get
		{
			return (Thickness)GetValue(IconPaddingProperty);
		}
		set
		{
			SetValue(IconPaddingProperty, value);
		}
	}

	static IconMenuItem()
	{
		ShowMaskProperty = DependencyProperty.Register("ShowMask", typeof(bool), typeof(IconMenuItem), new PropertyMetadata(false));
		NormalIconProperty = DependencyProperty.Register("NormalIcon", typeof(ImageSource), typeof(IconMenuItem), new PropertyMetadata((object)null));
		ClickIconProperty = DependencyProperty.Register("ClickIcon", typeof(ImageSource), typeof(IconMenuItem), new PropertyMetadata((object)null));
		HoverIconProperty = DependencyProperty.Register("HoverIcon", typeof(ImageSource), typeof(IconMenuItem), new PropertyMetadata((object)null));
		DisableIconProperty = DependencyProperty.Register("DisableIcon", typeof(ImageSource), typeof(ImageSource), new PropertyMetadata((object)null));
		TopMaskHoverProperty = DependencyProperty.Register("TopMaskHover", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		TopMaskClickProperty = DependencyProperty.Register("TopMaskClick", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		TopMaskNormalProperty = DependencyProperty.Register("TopMaskNormal", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		SubMaskHoverProperty = DependencyProperty.Register("SubMaskHover", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		SubMaskClickProperty = DependencyProperty.Register("SubMaskClick", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		SubMaskNormalProperty = DependencyProperty.Register("SubMaskNormal", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		PopupStyleProperty = DependencyProperty.Register("PopupStyle", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		ScrollViewerStyleProperty = DependencyProperty.Register("ScrollViewerStyle", typeof(Style), typeof(IconMenuItem), new PropertyMetadata((object)null));
		IconPaddingProperty = DependencyProperty.Register("IconPadding", typeof(Thickness), typeof(IconMenuItem), new PropertyMetadata(default(Thickness)));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(IconMenuItem), new FrameworkPropertyMetadata(typeof(IconMenuItem)));
	}

	protected override bool IsItemItsOwnContainerOverride(object item)
	{
		int num;
		if (!(item is MenuItem))
		{
			num = ((item is Separator) ? 1 : 0);
			if (num == 0)
			{
				_currentItem = item;
			}
		}
		else
		{
			num = 1;
		}
		return (byte)num != 0;
	}

	protected override DependencyObject GetContainerForItemOverride()
	{
		object currentItem = _currentItem;
		_currentItem = null;
		if (UsesItemContainerTemplate)
		{
			DataTemplate dataTemplate = ItemContainerTemplateSelector.SelectTemplate(currentItem, this);
			if (dataTemplate != null)
			{
				object obj = dataTemplate.LoadContent();
				if (obj is MenuItem || obj is Separator)
				{
					return obj as DependencyObject;
				}
				throw new InvalidOperationException("InvalidItemContainer");
			}
		}
		return new IconMenuItem();
	}
}
