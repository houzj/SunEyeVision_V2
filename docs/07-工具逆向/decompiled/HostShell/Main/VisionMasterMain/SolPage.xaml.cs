using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml.Linq;
using VM.Controls;

namespace VisionMasterMain;

public partial class SolPage : Page, IComponentConnector, IStyleConnector
{
	public SolPage()
	{
		InitializeComponent();
	}

	public static void LoadSolInfo(object itemsData)
	{
		try
		{
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_CfgErr", ex.Message, MessageIcons.Warning, isTranslate: true);
		}
	}

	public static void SaveSolInfo(object itemsData)
	{
		try
		{
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_CfgSaveErr", ex.Message, MessageIcons.Warning, isTranslate: true);
		}
	}

	private static XElement GetElement(XDocument xDoc, string nodename)
	{
		if (xDoc.Root == null)
		{
			return null;
		}
		return xDoc.Root.Elements().SingleOrDefault((XElement p) => p.Name == nodename);
	}

	public void solApply()
	{
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
