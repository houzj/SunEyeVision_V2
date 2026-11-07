using System;
using System.Diagnostics;
using System.IO;
using Apps.Log;
using Microsoft.Win32;
using VM.Controls;

namespace VisionMaster.AppMainWindow;

public class BrowserHelper
{
	private const string GoogleBrowserKey = "\\shell\\open\\command\\";

	private const string DefaultBrowserKey = "http\\shell\\open\\command\\";

	private const string IEBrowserPath1 = "C:\\Program Files\\Internet Explorer\\iexplore.exe";

	private const string IEBrowserPath2 = "C:\\Program Files (x86)\\Internet Explorer\\iexplore.exe";

	public static void OpenBrowser(string url)
	{
		bool flag = false;
		try
		{
			RegistryKey classesRoot = Registry.ClassesRoot;
			if (classesRoot != null)
			{
				string text = "";
				string[] subKeyNames = classesRoot.GetSubKeyNames();
				foreach (string text2 in subKeyNames)
				{
					if (text2.ToUpper().Contains("CHROMEHTML"))
					{
						text = text2 + "\\shell\\open\\command\\";
						break;
					}
				}
				if (!string.IsNullOrEmpty(text))
				{
					RegistryKey registryKey = classesRoot.OpenSubKey(text);
					if (registryKey != null)
					{
						string text3 = registryKey.GetValue("")?.ToString();
						if (!string.IsNullOrEmpty(text3))
						{
							int num = text3.IndexOf(".exe", StringComparison.Ordinal);
							string fileName = text3.Substring(1, num + 3);
							Process process = Process.Start(fileName, url);
							if (process != null)
							{
								flag = true;
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error($"OpenBrowser error: {ex.Message}{Environment.NewLine}url: {url}");
		}
		if (!flag)
		{
			OpenDefaultBrowser(url);
		}
	}

	private static void OpenDefaultBrowser(string url)
	{
		try
		{
			bool flag = false;
			RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey("http\\shell\\open\\command\\");
			if (registryKey != null)
			{
				string text = registryKey.GetValue("")?.ToString();
				if (!string.IsNullOrEmpty(text))
				{
					int num = text.IndexOf(".exe", StringComparison.Ordinal);
					string fileName = text.Substring(1, num + 3);
					Process process = Process.Start(fileName, url);
					if (process != null)
					{
						flag = true;
					}
				}
			}
			if (!flag)
			{
				Process process2 = Process.Start("explorer.exe", url);
				if (process2 == null && Process.Start(url) == null)
				{
					HikMessageBox.Show("Lang_NoBrowserWarn", MessageIcons.Warning, isTranslate: true);
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error($"OpenDefaultBrowserUrl error: {ex.Message}{Environment.NewLine}url: {url}");
			OpenIeBrowser(url);
		}
	}

	private static void OpenIeBrowser(string url)
	{
		try
		{
			if (File.Exists("C:\\Program Files\\Internet Explorer\\iexplore.exe"))
			{
				StartProcess("C:\\Program Files\\Internet Explorer\\iexplore.exe", url);
			}
			else if (File.Exists("C:\\Program Files (x86)\\Internet Explorer\\iexplore.exe"))
			{
				StartProcess("C:\\Program Files (x86)\\Internet Explorer\\iexplore.exe", url);
			}
			else
			{
				Process.Start("iexplore.exe", url);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_NoBrowserWarn", MessageIcons.Warning, isTranslate: true);
			LogHelper.Error($"OpenIeBrowser error: {ex.Message}{Environment.NewLine}url: {url}");
		}
	}

	public static Process StartProcess(string path, string param)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.WindowStyle = ProcessWindowStyle.Maximized;
		processStartInfo.FileName = path;
		processStartInfo.Arguments = param;
		return Process.Start(processStartInfo);
	}
}
