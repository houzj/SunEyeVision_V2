using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using Apps.Localization;
using Apps.XmlUI.ParamTab;
using DynamicObjectConfigControl.UI.Data;
using VM.Controls;

namespace DynamicObjectConfigControl;

public partial class Picker : BaseWindow, IComponentConnector
{
	internal CheckBoxConfigTreeView outputConfigTreeView;

	public IList CheckedLeafNodeList { get; set; } = new List<object>();

	public bool IsNeedIncludeModuleName { get; set; }

	public Picker()
	{
		InitializeComponent();
		Closing += IOPick_Closing;
	}

	private void IOPick_Closing(object sender, CancelEventArgs e)
	{
		if ((!DialogResult) ?? true)
		{
			IList checkedLeafNode = (DataContext as PickDataBase).GetCheckedLeafNode();
			if (checkedLeafNode != null && checkedLeafNode.Count > 0 && HikMessageBox.Show(AppLocalizationService.GetLang("是否放弃已勾选信息直接退出？"), "", MessageButtons.YesOrNo, MessageIcons.Question) != VM.Controls.DialogResult.Yes)
			{
				e.Cancel = true;
			}
		}
	}

	private void Confirm_OnClick(object sender, RoutedEventArgs e)
	{
		CheckedLeafNodeList = (DataContext as PickDataBase).GetCheckedLeafNode();
		IsNeedIncludeModuleName = (DataContext as PickDataBase).IsNeedIncludeModuleName;
		DialogResult = true;
	}

	private void Canncel_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}
}
