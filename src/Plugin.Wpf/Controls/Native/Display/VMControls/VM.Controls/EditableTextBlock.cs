using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "editableTextBoxHeader", Type = typeof(TextBox))]
[TemplatePart(Name = "textBlockHeader", Type = typeof(TextBlock))]
public class EditableTextBlock : Control, INotifyPropertyChanged
{
	private TextBox _editableTextBoxHeader;

	private TextBlock _textBlockHeader;

	public static readonly DependencyProperty IsSelectedProperty;

	public static readonly DependencyProperty IsListViewProperty;

	public static readonly DependencyProperty IsInEditModeProperty;

	private int _click;

	private string _oldText;

	public static DependencyProperty TextProperty;

	public bool IsSelected
	{
		get
		{
			return (bool)GetValue(IsSelectedProperty);
		}
		set
		{
			SetValue(IsSelectedProperty, value);
		}
	}

	public bool IsListView
	{
		get
		{
			return (bool)GetValue(IsListViewProperty);
		}
		set
		{
			SetValue(IsListViewProperty, value);
		}
	}

	public bool IsInEditMode
	{
		get
		{
			return (bool)GetValue(IsInEditModeProperty);
		}
		set
		{
			SetValue(IsInEditModeProperty, value);
		}
	}

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	static EditableTextBlock()
	{
		IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false, CallBack), null);
		IsListViewProperty = DependencyProperty.Register("IsListView", typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false, CallBack), null);
		IsInEditModeProperty = DependencyProperty.Register("IsInEditMode", typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false, CallBack), null);
		TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(EditableTextBlock), new FrameworkPropertyMetadata(null, CallBack), null);
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(EditableTextBlock), new FrameworkPropertyMetadata(typeof(EditableTextBlock)));
	}

	public override void OnApplyTemplate()
	{
		if (_editableTextBoxHeader != null)
		{
			_editableTextBoxHeader.IsVisibleChanged -= editableTextBoxHeader_IsVisibleChanged;
			_editableTextBoxHeader.KeyDown -= editableTextBoxHeader_KeyDown;
			_editableTextBoxHeader.LostFocus -= editableTextBoxHeader_LostFocus;
		}
		if (_textBlockHeader != null) _textBlockHeader.MouseLeftButtonDown -= textBlockHeader_MouseLeftButtonDown;
		base.OnApplyTemplate();
		_editableTextBoxHeader = Template.FindName("editableTextBoxHeader", this) as TextBox;
		if (_editableTextBoxHeader != null)
		{
			_editableTextBoxHeader.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("Text") { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
			_editableTextBoxHeader.IsVisibleChanged += editableTextBoxHeader_IsVisibleChanged;
			_editableTextBoxHeader.KeyDown += editableTextBoxHeader_KeyDown;
			_editableTextBoxHeader.LostFocus += editableTextBoxHeader_LostFocus;
		}
		_textBlockHeader = Template.FindName("textBlockHeader", this) as TextBlock;
		if (_textBlockHeader != null)
		{
			_textBlockHeader.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
			_textBlockHeader.MouseLeftButtonDown += textBlockHeader_MouseLeftButtonDown;
		}
	}

	private void FirePropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private static void CallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is EditableTextBlock editableTextBlock))
		{
			return;
		}
		switch (e.Property.Name)
		{
		case "IsSelected":
			if (!(bool)e.NewValue)
			{
				editableTextBlock.IsInEditMode = false;
			}
			break;
		case "IsListView":
			if (!editableTextBlock.IsSelected)
			{
				editableTextBlock.IsInEditMode = false;
			}
			break;
		case "IsInEditMode":
			if (editableTextBlock._editableTextBoxHeader == null)
			{
				return;
			}
			editableTextBlock._editableTextBoxHeader.Visibility = ((!(bool)e.NewValue) ? Visibility.Hidden : Visibility.Visible);
			editableTextBlock._click = 0;
			break;
		}
		editableTextBlock.FirePropertyChanged(e.Property.Name);
	}

	private void editableTextBoxHeader_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is TextBox { IsVisible: not false } textBox)
		{
			textBox.Focus();
			textBox.SelectAll();
			_oldText = textBox.Text;
		}
	}

	private void textBlockHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_click++;
		bool handled = (IsInEditMode = _click == 2);
		e.Handled = handled;
		if (_click > 2)
		{
			_click = 0;
		}
	}

	private void editableTextBoxHeader_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			IsInEditMode = false;
		}
		if (e.Key == Key.Escape)
		{
			if (sender is TextBox textBox)
			{
				textBox.Text = _oldText;
			}
			IsInEditMode = false;
		}
	}

	private void editableTextBoxHeader_LostFocus(object sender, RoutedEventArgs e)
	{
		if (IsListView)
		{
			IsInEditMode = false;
		}
	}
}
