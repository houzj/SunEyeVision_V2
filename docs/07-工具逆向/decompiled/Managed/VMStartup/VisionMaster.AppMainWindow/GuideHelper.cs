using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using VM.AppData;
using VM.Controls;
using VM.Utility;

namespace VisionMaster.AppMainWindow;

public static class GuideHelper
{
	private static string Configfle = AppDataFactory.Instance().AppCfgName;

	private const string GuideSettingNode = "GuideSetting";

	internal const string ShowEnableNode = "ShowEnable";

	private const string LocalHelpNode = "LocalHelp";

	private const string OnlineHelpNode = "OnlineHelp";

	private const string HelpDocNode = "HelpDoc";

	private const string HelpDocStd = "HelpDocStd";

	private const string HelpDocNeu = "HelpDocNeu";

	public static bool IsGuideWindowShow()
	{
		string config = ConfigurationHelper.GetConfig("ShowEnable");
		return bool.Parse(config);
	}

	private static XElement GetElement(XDocument xDoc, string nodename)
	{
		if (xDoc.Root == null)
		{
			return null;
		}
		return xDoc.Root.Elements().SingleOrDefault((XElement p) => p.Name == "GuideSetting")?.Elements().SingleOrDefault((XElement p) => p.Name == nodename);
	}

	private static XElement GetHelpElement(XDocument xDoc, string nodename)
	{
		if (xDoc.Root == null)
		{
			return null;
		}
		return xDoc.Root.Elements().SingleOrDefault((XElement p) => p.Name == "HelpDoc")?.Elements().SingleOrDefault((XElement p) => p.Name == nodename);
	}

	public static void SetGuideWindowOn()
	{
		ConfigurationHelper.SaveConfig("ShowEnable", "true");
	}

	public static void SetGuideWindowOff()
	{
		ConfigurationHelper.SaveConfig("ShowEnable", "false");
	}

	public static bool OpenSolution(bool opendemo, out string filename)
	{
		bool flag = false;
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = string.Format($"{0}|*.{AppFileExtension.Solution}", opendemo ? AppLocalizationService.GetLang("示例方案") : AppLocalizationService.GetLang("方案"))
		};
		try
		{
			if (opendemo)
			{
				string text = ConfigurationHelper.INIGetStringValue("OpenSample");
				string path = ((text == null) ? (AppDomain.CurrentDomain.SetupInformation.ApplicationBase + ConfigurationHelper.GetConfig("Demo")) : text);
				DirectoryInfo directoryInfo = new DirectoryInfo(path);
				if (directoryInfo.Exists)
				{
					openFileDialog.InitialDirectory = directoryInfo.FullName;
					flag = true;
				}
				else
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Not_Installed_Samples"));
					flag = false;
				}
			}
			if (flag)
			{
				openFileDialog.ShowDialog();
				filename = openFileDialog.FileName;
				ConfigurationHelper.INISetStringValue("OpenSample", Path.GetDirectoryName(filename));
				openFileDialog.Dispose();
				if (string.IsNullOrEmpty(filename))
				{
					return false;
				}
				return true;
			}
			filename = "";
			return false;
		}
		finally
		{
			openFileDialog.Dispose();
		}
	}

	public static void GetHelp(bool local)
	{
		if (local)
		{
			string text = "";
			try
			{
				string lang = AppLocalizationService.GetLang(GetHelpPathFile(AppDataFactory.Instance().IsNeutral));
				text = lang;
			}
			catch (Exception)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LangLack"), MessageIcons.Warning);
				LogHelper.Error("语言资源缺失");
			}
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo();
				processStartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, text);
				processStartInfo.WindowStyle = ProcessWindowStyle.Maximized;
				processStartInfo.WorkingDirectory = text;
				Process.Start(processStartInfo);
				return;
			}
			catch (Exception)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_HelpFileLack"), MessageIcons.Warning);
				LogHelper.Error("帮助文档缺失");
				return;
			}
		}
		string key = (local ? "LocalHelp" : "OnlineHelp");
		string text2 = ConfigurationHelper.GetConfig(key);
		if (AppLocalizationService.GetLang("Language") == "English")
		{
			text2 += "en";
		}
		try
		{
			Process.Start(text2);
		}
		catch (Exception ex3)
		{
			HikMessageBox.Show(ex3.Message, MessageIcons.Error);
		}
	}

	public static void GetRemoteDongleHelp()
	{
		string text = "";
		try
		{
			text = AppLocalizationService.GetLang(GetHelpPathFile(AppDataFactory.Instance().IsNeutral));
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LangLack"), MessageIcons.Warning);
			LogHelper.Error("语言资源缺失: " + ex.Message);
			return;
		}
		try
		{
			string text2 = Environment.CurrentDirectory + text;
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				BrowserHelper.StartProcess("hh.exe", string.Format("{0}::{1}", text2, AppLocalizationService.GetLang("Lang_RemoteDongleHelpPage")));
				return;
			}
			UriBuilder uriBuilder = new UriBuilder(text2);
			uriBuilder.Query = string.Format("{0}", AppLocalizationService.GetLang("Lang_RemoteDongleHelpPage"));
			string url = uriBuilder.Uri.ToString();
			BrowserUtil.OpenUrl(url);
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_HelpFileLack"), MessageIcons.Warning);
			LogHelper.Error("帮助文档缺失: " + ex2.Message);
		}
	}

	public static string GetHelpPathFile(bool ifMeu = false)
	{
		return ConfigurationHelper.GetConfig(ifMeu ? "HelpDocNeu" : "HelpDocStd");
	}
}
