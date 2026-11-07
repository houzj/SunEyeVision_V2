using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;

namespace ShellModuleControl;

public partial class InputParam : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public delegate void AddVariableCompletedHandler(object sender, ObjectEventArgs e);

	public string _varname = "";

	public string _vartype = "";

	public string _varinitvalue = "";

	public string VarName
	{
		get
		{
			return _varname;
		}
		set
		{
			if (_varname != value)
			{
				_varname = value;
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs("VarName"));
				}
			}
		}
	}

	public string VarType
	{
		get
		{
			return _vartype;
		}
		set
		{
			if (_vartype != value)
			{
				_vartype = value;
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs("VarType"));
				}
			}
		}
	}

	public string VarInitValue
	{
		get
		{
			return _varinitvalue;
		}
		set
		{
			if (_varinitvalue != value)
			{
				_varinitvalue = value;
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs("VarInitValue"));
				}
			}
		}
	}

	public event AddVariableCompletedHandler AddVariableCompleted;

	public event PropertyChangedEventHandler PropertyChanged;

	public InputParam()
	{
		InitializeComponent();
		Loaded += InputParam_Loaded;
		Unloaded += InputParam_Unloaded;
	}

	private void InputParam_Loaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO)
		{
			shellModuleDynamicIO.LoadChangedItemsOrder("ItemsAIndexA-In");
			shellModuleDynamicIO.LoadDisplayValueType("InputValueTypes");
		}
	}

	private void InputParam_Unloaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO)
		{
			shellModuleDynamicIO.SaveDisplayValueType("InputValueTypes");
		}
	}

	private void RaisePropertyChangedEvent(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private void ParamSwitch_Click(object sender, RoutedEventArgs e)
	{
		if (ButEdit.IsChecked.Value)
		{
			ButEdit.Content = AppLocalizationService.GetLang("Lang_OK");
			editConCtrl.Visibility = Visibility.Visible;
			displayConCtrl.Visibility = Visibility.Collapsed;
		}
		else
		{
			SwitchToDisplay();
		}
	}

	public void SwitchToDisplay()
	{
		if (ButEdit.IsChecked == true)
		{
			ButEdit.IsChecked = false;
		}
		editConCtrl.Visibility = Visibility.Collapsed;
		displayConCtrl.Visibility = Visibility.Visible;
		if (AddVariableCompleted != null)
		{
			ObjectEventArgs e = new ObjectEventArgs();
			e.Param = 1;
			AddVariableCompleted(ButEdit, e);
		}
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				e.Handled = true;
			}
		}
	}

	private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
		{
			return;
		}
		try
		{
			DynamicIO dynamicIO = (sender as ContentControl).DataContext as DynamicIO;
			TextBox textBox = e.OriginalSource as TextBox;
			if (dynamicIO == null || textBox == null)
			{
				e.Handled = true;
				return;
			}
			string valueTypeA = dynamicIO.ValueTypeA;
			string text = textBox.Text;
			switch (valueTypeA)
			{
			case "int":
				if (string.IsNullOrWhiteSpace(text))
				{
					if (!IsPlusOrMinus(e.Key) && !IsNumber(e.Key))
					{
						e.Handled = true;
					}
				}
				else if (!IsNumber(e.Key))
				{
					if (textBox.CaretIndex == 0 && text[0] != '+' && text[0] != '-' && IsPlusOrMinus(e.Key))
					{
						e.Handled = false;
					}
					else
					{
						e.Handled = true;
					}
				}
				break;
			case "float":
				if (string.IsNullOrWhiteSpace(text))
				{
					if (!IsPlusOrMinus(e.Key) && !IsNumber(e.Key))
					{
						e.Handled = true;
					}
				}
				else if (!IsNumber(e.Key))
				{
					if (textBox.CaretIndex == 0 && text[0] != '+' && text[0] != '-' && IsPlusOrMinus(e.Key))
					{
						e.Handled = false;
					}
					else if (textBox.CaretIndex > 0 && textBox.CaretIndex <= textBox.Text.Length && !text.Contains(".") && IsDecimal(e.Key))
					{
						e.Handled = false;
					}
					else
					{
						e.Handled = true;
					}
				}
				break;
			case "string":
				if (!IsUnderline(e.Key) && !IsNumberOrLetter(e.Key))
				{
					e.Handled = true;
				}
				break;
			}
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	private bool IsNumber(Key key)
	{
		bool flag = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
		if ((key >= Key.NumPad0 && key <= Key.NumPad9) || (key >= Key.D0 && key <= Key.D9 && !flag))
		{
			return true;
		}
		return false;
	}

	private bool IsNumberOrLetter(Key key)
	{
		if (IsNumber(key) || (key >= Key.A && key <= Key.Z))
		{
			return true;
		}
		return false;
	}

	private bool IsPlusOrMinus(Key key)
	{
		bool flag = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
		if (key == Key.Add || key == Key.Subtract || ((key == Key.OemPlus) & flag) || (key == Key.OemMinus && !flag))
		{
			return true;
		}
		return false;
	}

	private bool IsDecimal(Key key)
	{
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift && (key == Key.Decimal || key == Key.OemPeriod))
		{
			return true;
		}
		return false;
	}

	private bool IsQuotes(Key key)
	{
		if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && key == Key.Oem7)
		{
			return true;
		}
		return false;
	}

	private bool IsUnderline(Key key)
	{
		if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && key == Key.OemMinus)
		{
			return true;
		}
		return false;
	}

	private void mouseRightBtnDown(object sender, MouseButtonEventArgs e)
	{
		TextBox parentObject = GetParentObject<TextBox>((DependencyObject)e.OriginalSource, 1);
		if (parentObject != null)
		{
			parentObject.ContextMenu = null;
		}
	}

	public T GetParentObject<T>(DependencyObject obj, int level) where T : FrameworkElement
	{
		DependencyObject parent = VisualTreeHelper.GetParent(obj);
		int num = 0;
		while (parent != null)
		{
			if (parent is T)
			{
				num++;
				if (num == level)
				{
					return (T)parent;
				}
			}
			parent = VisualTreeHelper.GetParent(parent);
		}
		return null;
	}

	private void ButtonUp_Click(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO && sender is Button button)
		{
			int index = shellModuleDynamicIO.ItemsA.IndexOf(button.Tag);
			shellModuleDynamicIO.ChangeItemsAOrder("ItemsAIndexA-In", index, "Up");
		}
	}

	private void ButtonDown_Click(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO && sender is Button button)
		{
			int index = shellModuleDynamicIO.ItemsA.IndexOf(button.Tag);
			shellModuleDynamicIO.ChangeItemsAOrder("ItemsAIndexA-In", index, "Down");
		}
	}

	private void listInputVars_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (!(sender is ItemsControl current))
		{
			return;
		}
		DependencyObject parent = LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(current));
		if (parent is ScrollViewer scrollViewer)
		{
			scrollViewer.ScrollToEnd();
			if (e.NewSize.Height < e.PreviousSize.Height)
			{
				scrollViewer.ScrollToRightEnd();
			}
		}
	}
}
