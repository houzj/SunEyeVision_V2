using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.VisionMaster.ParamTab;

public partial class ParamsTabControlPanal : UserControl, IComponentConnector
{
	public ParamsTabControlPanal()
	{
		InitializeComponent();
	}

	private void Tab_Loaded(object sender, RoutedEventArgs e)
	{
		for (int i = 0; i < tab.Items.Count; i++)
		{
			if (tab.ItemContainerGenerator.ContainerFromIndex(i) is TabItem { Visibility: Visibility.Visible })
			{
				tab.SelectedIndex = i;
				break;
			}
		}
	}

}
