using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml.Linq;
using VM.Controls;

namespace VisionMasterMain;

public partial class SolutionManagerDialog : BaseWindow, IComponentConnector, IStyleConnector
{
	public SolutionManagerDialog()
	{
		InitializeComponent();
	}

	private void Apply_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}

	private static XElement GetElement(XDocument xDoc, string nodename)
	{
		if (xDoc.Root == null)
		{
			return null;
		}
		return xDoc.Root.Elements().SingleOrDefault((XElement p) => p.Name == nodename);
	}

	private void tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("^[0-9a-zA-Z]*$");
		e.Handled = !regex.IsMatch(e.Text);
	}

	private void text_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
	}

	private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		e.Handled = true;
	}
}
