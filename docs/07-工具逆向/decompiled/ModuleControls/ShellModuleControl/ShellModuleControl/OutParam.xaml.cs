using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;

namespace ShellModuleControl;

public partial class OutParam : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
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

	public OutParam()
	{
		InitializeComponent();
		Loaded += OutParam_Loaded;
		Unloaded += OutParam_Unloaded;
	}

	private void OutParam_Loaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO)
		{
			shellModuleDynamicIO.LoadChangedItemsOrder("ItemsAIndexA-Out");
			shellModuleDynamicIO.LoadDisplayValueType("OutputValueTypes");
		}
	}

	private void OutParam_Unloaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO)
		{
			shellModuleDynamicIO.SaveDisplayValueType("OutputValueTypes");
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
			e.Param = 2;
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

	private void TxtName_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space && sender != null && sender is TextBox)
		{
			e.Handled = true;
		}
	}

	private void ButtonUp_Click(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO && sender is Button button)
		{
			int index = shellModuleDynamicIO.ItemsA.IndexOf(button.Tag);
			shellModuleDynamicIO.ChangeItemsAOrder("ItemsAIndexA-Out", index, "Up");
		}
	}

	private void ButtonDown_Click(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellModuleDynamicIO shellModuleDynamicIO && sender is Button button)
		{
			int index = shellModuleDynamicIO.ItemsA.IndexOf(button.Tag);
			shellModuleDynamicIO.ChangeItemsAOrder("ItemsAIndexA-Out", index, "Down");
		}
	}

	private void listInputVars_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is ItemsControl current)
		{
			DependencyObject parent = LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(current));
			if (parent is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToEnd();
			}
		}
	}
}
