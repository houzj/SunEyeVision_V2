using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace CalculatorModuleControl;

public partial class CalculatorProPanel : UserControl, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty ButtonEnableProperty = DependencyProperty.Register("ButtonEnable", typeof(bool), typeof(CalculatorProPanel), new PropertyMetadata(true));

	public bool ButtonEnable
	{
		get
		{
			return (bool)GetValue(ButtonEnableProperty);
		}
		set
		{
			SetValue(ButtonEnableProperty, value);
		}
	}

	public int MaxIOCount => int.MaxValue;

	public CalculatorProPanel()
	{
		InitializeComponent();
	}

	private void Add_Out(object sender, RoutedEventArgs e)
	{
		ScrollViewer.ScrollToEnd();
	}

	private void Delete_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: IDisposable dataContext })
		{
			dataContext.Dispose();
		}
	}
}
