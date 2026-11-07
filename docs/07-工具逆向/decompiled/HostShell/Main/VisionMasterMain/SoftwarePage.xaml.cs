using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
using Apps.Localization;
using Hik.Util;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.User;
using VM.Utility;

namespace VisionMasterMain;

public partial class SoftwarePage : Page, INotifyPropertyChanged, IComponentConnector
{
	public static Dictionary<string, string> accessMap = new Dictionary<string, string>
	{
		{ "管理员", "ef84e765e027ce1984c2be0aabaea66afb9e66eb" },
		{ "操作员", "0c7c09a07e129b8bb76dc7547589c875ced22804" },
		{ "技术员", "1c6e56e9e2c157e9650a92509aaf3ba7c4cf750b" }
	};

	private bool _ifShowSolCode;

	private string AUTOSTARTTASK = "VisionMasterAutoStart";

	public bool IfShowSolCode
	{
		get
		{
			return _ifShowSolCode;
		}
		set
		{
			if (_ifShowSolCode != value)
			{
				if (value)
				{
					solCodeText.Text = solCodeBox.Password;
					solCodeBox.Visibility = Visibility.Collapsed;
					solCodeText.Visibility = Visibility.Visible;
				}
				else
				{
					solCodeBox.Password = solCodeText.Text;
					solCodeText.Visibility = Visibility.Collapsed;
					solCodeBox.Visibility = Visibility.Visible;
				}
				_ifShowSolCode = value;
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public SoftwarePage()
	{
		InitializeComponent();
	}

	private void basepage_Loaded(object sender, RoutedEventArgs e)
	{
	}

	private void InitAccess()
	{
		access.Items.Clear();
		switch (CodeManager.GetInstance().GetLoginUser())
		{
		case AccessUser.Admin:
		{
			access.Items.Add(AppLocalizationService.GetLang("Lang_Role_Admin"));
			bool bEnable2 = false;
			string techCode = "";
			CodeManager.GetInstance().GetTechCode(out bEnable2, out techCode);
			if (bEnable2)
			{
				access.Items.Add(AppLocalizationService.GetLang("Lang_Role_Tech"));
			}
			bEnable2 = false;
			CodeManager.GetInstance().GetOperatorCode(out bEnable2, out techCode);
			if (bEnable2)
			{
				access.Items.Add(AppLocalizationService.GetLang("Lang_Role_Operator"));
			}
			FrontendFirstSP.Visibility = Visibility.Visible;
			break;
		}
		case AccessUser.Tech:
		{
			access.Items.Add(AppLocalizationService.GetLang("Lang_Role_Tech"));
			bool bEnable = false;
			string operatorCode = "";
			CodeManager.GetInstance().GetOperatorCode(out bEnable, out operatorCode);
			if (bEnable)
			{
				access.Items.Add(AppLocalizationService.GetLang("Lang_Role_Operator"));
			}
			break;
		}
		}
	}

	public void LoadSetting()
	{
		try
		{
			XmlDocument configXMLDoc = GetConfigXMLDoc();
			XmlNode node = SearchNodeContent(configXMLDoc, "IsAutoLaunch");
			if (node != null)
			{
				autoStartEnable.IsChecked = Convert.ToBoolean(node.InnerText);
			}
			node = SearchNodeContent(configXMLDoc, "AutoLaunchDelayTime");
			if (node != null)
			{
				delayTime.StartValue = int.Parse(node.InnerText);
			}
			if (FrontendFirstSP.Visibility == Visibility.Visible)
			{
				FrontendFirst.IsChecked = IsFrontendFirst();
			}
			node = SearchNodeContent(configXMLDoc, "AutoLaunchAccess");
			if (node != null)
			{
				access.Text = AppLocalizationService.GetLang(accessMap.FirstOrDefault((KeyValuePair<string, string> q) => q.Value == node.InnerText).Key);
			}
		}
		catch (Exception)
		{
			HikMessageBox.Show("Lang_CfgErr", MessageIcons.Warning, isTranslate: true);
		}
	}

	private XmlDocument GetConfigXMLDoc()
	{
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(ConfigurationHelper.GetConfig("ApplicationName") + ".cfg");
		return xmlDocument;
	}

	public void SoftwareApply()
	{
		try
		{
			XmlDocument configXMLDoc = GetConfigXMLDoc();
			XmlNode xmlNode = SearchNodeContent(configXMLDoc, "IsAutoLaunch");
			if (xmlNode != null && autoStartEnable.IsChecked.ToString() != xmlNode.InnerText)
			{
				xmlNode.InnerText = autoStartEnable.IsChecked.ToString();
			}
			xmlNode = SearchNodeContent(configXMLDoc, "AutoLaunchDelayTime");
			if (xmlNode != null)
			{
				xmlNode.InnerText = delayTime.StartValue.ToString();
			}
			xmlNode = SearchNodeContent(configXMLDoc, "AutoLoadPath");
			if (xmlNode != null)
			{
				xmlNode.InnerText = loadPath.Text;
			}
			xmlNode = SearchNodeContent(configXMLDoc, "IsAutoLoadPath");
			if (xmlNode != null && autoLoadEnable.IsChecked.ToString() != xmlNode.InnerText)
			{
				xmlNode.InnerText = autoLoadEnable.IsChecked.ToString();
			}
			xmlNode = SearchNodeContent(configXMLDoc, "AutoLoadSolPassword");
			if (xmlNode != null)
			{
				xmlNode.InnerText = solCodeBox.Password.String2Base64();
			}
			xmlNode = SearchNodeContent(configXMLDoc, "AutoLaunchAccess");
			if (xmlNode != null)
			{
				xmlNode.InnerText = accessMap.FirstOrDefault((KeyValuePair<string, string> q) => AppLocalizationService.GetLang(q.Key) == access.Text).Value;
			}
			xmlNode = SearchNodeContent(configXMLDoc, "AutoLaunchRunState");
			if (xmlNode != null)
			{
				string innerText = runState.Text;
				if (runState.Text == "非连续执行" || runState.Text == "Discontinuouse Run")
				{
					innerText = "非连续执行";
				}
				if (runState.Text == "连续执行" || runState.Text == "Run Continuously")
				{
					innerText = "连续执行";
				}
				xmlNode.InnerText = innerText;
			}
			ConfigurationHelper.SaveConfig(configXMLDoc);
			SetAutoLaunch(autoStartEnable.IsChecked, (int)delayTime.StartValue);
			SetAutoLoadSol();
		}
		catch (Exception)
		{
			HikMessageBox.Show("Lang_CfgErr", MessageIcons.Warning, isTranslate: true);
		}
	}

	private int GetAutoLoadSolState()
	{
		ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO iMVS_AUTO_LOAD_SOLUTION_INFO = default;
		iMVS_AUTO_LOAD_SOLUTION_INFO = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetAutoLoadSolution().Value;
		if (iMVS_AUTO_LOAD_SOLUTION_INFO.nIfAutoLoad == 1)
		{
			autoLoadEnable.IsChecked = true;
			RefreshAccessVisible();
		}
		else
		{
			autoLoadEnable.IsChecked = false;
			RefreshAccessVisible();
		}
		if (iMVS_AUTO_LOAD_SOLUTION_INFO.nExecuteStatus == 2)
		{
			runState.Text = AppLocalizationService.GetLang("Lang_MutiRun");
		}
		else
		{
			runState.Text = AppLocalizationService.GetLang("静默执行");
		}
		solCodeBox.Password = Hik.Util.StringUtil.UTF8GetString(iMVS_AUTO_LOAD_SOLUTION_INFO.strPassWord);
		loadPath.Text = Hik.Util.StringUtil.UTF8GetString(iMVS_AUTO_LOAD_SOLUTION_INFO.strSolutionPath);
		return 0;
	}

	public XmlNode SearchNodeContent(XmlDocument doc, string attribute)
	{
		if (doc == null)
		{
			return null;
		}
		XmlNode xmlNode = doc.SelectSingleNode($"//Setting[@Name='{attribute}']");
		if (xmlNode == null)
		{
			return null;
		}
		XmlNode xmlNode2 = xmlNode.SelectSingleNode("./CurValue");
		if (xmlNode2 == null)
		{
			return null;
		}
		return xmlNode2;
	}

	public static bool IsFrontendFirst()
	{
		return true;
	}

	private void SetAutoLoadSol()
	{
		ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO autoLoadSolution = new ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO
		{
			nIfAutoLoad = ((autoLoadEnable.IsChecked == true) ? 1u : 0u),
			nExecuteStatus = ((runState.Text == AppLocalizationService.GetLang("Lang_MutiRun")) ? 2u : 0u),
			strPassWord = Hik.Util.StringUtil.UTF8GetFixLenBytes(solCodeBox.Password, 32),
			strSolutionPath = Hik.Util.StringUtil.UTF8GetFixLenBytes(loadPath.Text, 780)
		};
		ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetAutoLoadSolution(autoLoadSolution);
	}

	private void SetAutoLaunch(bool? bIsAutoLaunch, int nDelayTime)
	{
	}

	private void FrontendFirst_OnChecked(object sender, RoutedEventArgs e)
	{
	}

	private void FrontendFirst_OnUnchecked(object sender, RoutedEventArgs e)
	{
	}

	private void autoLoadEnable_Unchecked(object sender, RoutedEventArgs e)
	{
		ClearData();
		RefreshAccessVisible();
	}

	private void autoLoadEnable_Checked(object sender, RoutedEventArgs e)
	{
		RefreshAccessVisible();
	}

	private void ClearData()
	{
		loadPath.Text = "";
		solCodeBox.Password = "";
		solCodeText.Text = "";
		runState.SelectedIndex = 0;
	}

	private void RefreshAccessVisible()
	{
		if (CodeManager.GetInstance().IsAccessEnable && autoLoadEnable.IsChecked.Value)
		{
			accessPanel.Visibility = Visibility.Visible;
			accessText.Visibility = Visibility.Visible;
		}
		else
		{
			accessPanel.Visibility = Visibility.Collapsed;
			accessText.Visibility = Visibility.Collapsed;
		}
	}

	private void selPath_Click(object sender, RoutedEventArgs e)
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
