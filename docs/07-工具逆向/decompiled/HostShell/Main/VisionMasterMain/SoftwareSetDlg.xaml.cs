using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Log;
using VM.Controls;
using VM.Controls.Utility;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;

namespace VisionMasterMain;

public partial class SoftwareSetDlg : BaseWindow, IComponentConnector
{
	public AccessPage accessPage = new AccessPage();

	public SoftwarePage softwarepage = new SoftwarePage();

	public SolPage solpage = new SolPage();

	public ProcedureRunPage procedureRunPage = new ProcedureRunPage();

	public CollectionPage collectionPage = new CollectionPage();

	private DelegateCommand _openHelpCommand;

	public DelegateCommand OpenHelpCommand
	{
		get
		{
			if (_openHelpCommand == null)
			{
				_openHelpCommand = new DelegateCommand(ExecuteOpenHelp);
			}
			return _openHelpCommand;
		}
	}

	public event Func<bool> ApplyEventHander;

	private void ExecuteOpenHelp()
	{
		string toolItemInfoPath = $"{CurrentPathHelper.Instance.GetCurrentPath()}\\\\Resources\\Settings\\ToolItemInfo.xml";
		HelpDocumentUtil.OpenHelpDocument(toolItemInfoPath);
	}

	public SoftwareSetDlg()
	{
		InitializeComponent();
	}

	private bool Apply()
	{
		bool flag = true;
		try
		{
			ApplyEventHander?.Invoke();
		}
		catch (Exception ex)
		{
			VmException vmException = VmSolution.GetVmException(ex);
			if (vmException != null)
			{
				HikMessageBox.Show(vmException.errorMessage, MessageIcons.Error);
				LogHelper.Error(vmException.errorCode + vmException.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
			}
			LogHelper.Error(ex);
			flag = false;
		}
		return accessPage.AccessApply() & flag;
	}

	private void SetType_Click(object sender, RoutedEventArgs e)
	{
		switch ((sender as RadioButton).Name)
		{
		case "rbt_accessset":
			ChangePage(accessPage);
			break;
		case "rbt_vmset":
			ChangePage(softwarepage);
			break;
		case "rbt_solset":
			ChangePage(solpage);
			break;
		case "rbt_runstragety":
			ChangePage(procedureRunPage);
			break;
		case "rbt_collectionstragety":
			ChangePage(collectionPage);
			break;
		}
	}

	public void ChangePage(Page page)
	{
		if (page != null)
		{
			setpagechage.Content = new Frame
			{
				Content = page
			};
		}
	}

	public void ChangePageIsChecked(string strName)
	{
		switch (strName)
		{
		case "itemauth":
			rbt_accessset.IsChecked = true;
			break;
		case "itemvmset":
			rbt_vmset.IsChecked = true;
			break;
		case "itemsolset":
			rbt_solset.IsChecked = true;
			break;
		case "itemprocrunset":
			rbt_runstragety.IsChecked = true;
			break;
		case "itemcollectset":
			rbt_collectionstragety.IsChecked = true;
			break;
		}
	}

	public void Apply_OnClick(object sender, RoutedEventArgs e)
	{
		if (Apply())
		{
			Close();
		}
	}
}
