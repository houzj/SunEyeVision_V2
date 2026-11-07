using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace CalculatorModuleControl;

public partial class ExpressionEditor : TextBox, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty ExpressionProperty = DependencyProperty.Register("Expression", typeof(string), typeof(ExpressionEditor), new PropertyMetadata((object)null));

	public string Expression
	{
		get
		{
			return (string)GetValue(ExpressionProperty);
		}
		set
		{
			SetValue(ExpressionProperty, value);
		}
	}

	public ExpressionEditor()
	{
		InitializeComponent();
	}

	private void OpenPanel_OnClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is CalculatorItem calculatorItem)
		{
			CalculatorWindow calculatorWindow = new CalculatorWindow(calculatorItem);
			if (calculatorWindow.ShowDialog() == true)
			{
				calculatorItem.Expression = calculatorWindow.CalculatorItem.Expression;
				calculatorItem.Initial = calculatorWindow.CalculatorItem.Initial;
				calculatorItem.Summation = calculatorWindow.CalculatorItem.Summation;
				calculatorItem.RemarkInfo = calculatorWindow.CalculatorItem.RemarkInfo;
			}
		}
	}
}
