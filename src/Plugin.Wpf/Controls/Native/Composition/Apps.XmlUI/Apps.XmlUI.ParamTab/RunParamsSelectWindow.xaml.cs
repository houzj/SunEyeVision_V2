using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class RunParamsSelectWindow : BaseWindow, IComponentConnector, IStyleConnector
{
	private RunParamsSelectData runParamsSelectData = null;

	public RunParamsSelectWindow(List<RunParamsBaseNode> selectRunparams, RunParamsSelectData paramsData)
	{
		runParamsSelectData = paramsData;
		runParamsSelectData.SelectRunParams = selectRunparams;
		InitializeComponent();
		DataContext = runParamsSelectData;
	}

	public RunParamsSelectWindow(List<RunParamsBaseNode> selectRunparams, List<RunParamsBaseNode> otherRunParams, RunParamsSelectData paramsData, string varType)
	{
		runParamsSelectData = paramsData;
		runParamsSelectData.SelectRunParams = selectRunparams;
		runParamsSelectData.OtherSelectedRunParmas = otherRunParams;
		runParamsSelectData.SelectedVarType = varType;
		runParamsSelectData.IsFromVarBind = true;
		InitializeComponent();
		DataContext = runParamsSelectData;
	}

	public RunParamsSelectWindow(object dataSource)
	{
		InitializeComponent();
		runParamsSelectData.DecorateSource = dataSource;
		DataContext = runParamsSelectData;
	}

	public List<RunParamsBaseNode> GetSelectedParams()
	{
		return runParamsSelectData?.SelectRunParams;
	}

	public void SetSelectedParams(List<RunParamsBaseNode> selectRunparams)
	{
		if (runParamsSelectData != null)
		{
			runParamsSelectData.SelectRunParams = selectRunparams;
		}
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		if (runParamsSelectData.GetSelectedRunParams())
		{
			DialogResult = true;
			Close();
		}
	}

	private void Delete_OnHandler(object sender, RoutedEventArgs e)
	{
		if ((sender as Button).DataContext is RunParamsBaseNode runParamsBaseNode)
		{
			runParamsBaseNode.OutputEnabled = false;
		}
	}

	private void SearchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		runParamsSelectData.GetSelectRunParams(SearchComboBox.SelectedIndex);
	}

}
