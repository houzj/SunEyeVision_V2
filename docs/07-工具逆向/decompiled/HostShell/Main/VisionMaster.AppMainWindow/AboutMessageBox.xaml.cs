using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Apps.Localization;
using VM.AppData;
using VM.Controls;
using VM.Utility;
using VMControls.Interface;

namespace VisionMaster.AppMainWindow;

public partial class AboutMessageBox : BaseWindow, IComponentConnector
{
	public AboutMessageBox()
	{
		InitializeComponent();
		AppData appData = AppDataFactory.Instance();
		ShowInTaskbar = false;
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "Resources\\about_logo.png", UriKind.RelativeOrAbsolute);
		bitmapImage.DecodePixelWidth = 374;
		bitmapImage.DecodePixelHeight = 80;
		bitmapImage.EndInit();
		aboutLogoImg.Source = bitmapImage;
		using (Process process = new Process())
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ToolsOfInternal\\VersionInfoTool.exe");
			if (File.Exists(text))
			{
				string arguments = ConfigurationHelper.GetConfig("ApplicationName") ?? "VisionMaster";
				process.StartInfo.FileName = text;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.UseShellExecute = false;
				process.Start();
				process.WaitForExit(5000);
			}
		}
		List<VersionInfo> versionInfoList = new List<VersionInfo>();
		AddVersionInfo(ref versionInfoList, "版本信息", "VM");
		if (CheckAdditionExist.IsExistAddition)
		{
			VersionInfo versionInfo = new VersionInfo
			{
				Title = AppLocalizationService.GetLang("3D版本信息"),
				Value = AppLocalizationService.GetLang("Lang_version3D") + " Build" + appData.LastWriteTime
			};
			if (!string.IsNullOrEmpty(versionInfo.Value))
			{
				versionInfoList.Add(versionInfo);
			}
		}
		AddVersionInfo(ref versionInfoList, "补丁版本信息", "VM_Patch");
		AddAlgVersionInfo(ref versionInfoList, "2D_OperatorVersion", "MVDAlgorithmSDK_2D", "ReferencedAssemblies\\Algorithms\\MVDVersionObtain.Net.dll", "Lang_2dSDKLoadFail");
		AddVersionInfo(ref versionInfoList, "2D算子补丁版本号", "MVDAlgorithmSDK_Patch");
		if (CheckAdditionExist.IsExistAddition)
		{
			AddAlgVersionInfo(ref versionInfoList, "3D_OperatorVersion", "MVDAlgorithmSDK_3D", "ReferencedAssemblies\\Algorithms\\MVDVersionObtain.Net.dll", "Get 3DAlogVersion Failed");
		}
		AddAlgVersionInfo(ref versionInfoList, "DL_OperatorVersion", "MVDAlgorithmSDK_DeepLearning", "ReferencedAssemblies\\Algorithms\\MVD_DLAlgorithm.Net.dll", "Lang_DLSDKLoadFail");
		AddVersionInfo(ref versionInfoList, "Lang_MVS_Driver_Version", "MVS_Drive");
		AddVersionInfo(ref versionInfoList, "MVS_Support_Version", "MVS_STD");
		versionInfoListBox.ItemsSource = versionInfoList;
		if (appData.IsUseCustomCopyright)
		{
			txtCopyright.Text = "©" + appData.CustomCopyright;
		}
		if (appData.IsHideCopyright)
		{
			txtStatement.Visibility = Visibility.Hidden;
			txtCopyright.Visibility = Visibility.Hidden;
		}
		if (appData.IsHideLogo)
		{
			aboutLogoImg.Visibility = Visibility.Collapsed;
		}
		if (appData.IsNeutral)
		{
			txtStatement.Visibility = Visibility.Hidden;
			txtCopyright.Visibility = Visibility.Hidden;
		}
		else
		{
			txtStatement.Visibility = Visibility.Visible;
			txtCopyright.Visibility = Visibility.Visible;
		}
	}

	private void AddVersionInfo(ref List<VersionInfo> versionInfoList, string title, string verInfoKey)
	{
		VersionInfo versionInfo = new VersionInfo();
		versionInfo.Title = AppLocalizationService.GetLang(title);
		string text = ((!(verInfoKey == "VM")) ? AppDataFactory.Instance().GetVersionInfo(verInfoKey) : AppDataFactory.Instance().VersionInfo);
		if (!string.IsNullOrEmpty(text))
		{
			versionInfo.Value = GetBuildDate(text);
			versionInfoList.Add(versionInfo);
		}
	}

	private void AddAlgVersionInfo(ref List<VersionInfo> versionInfoList, string title, string verInfoKey, string dllPath, string errorKey)
	{
		VersionInfo versionInfo = new VersionInfo();
		versionInfo.Title = AppLocalizationService.GetLang(title);
		string text = AppDataFactory.Instance().GetVersionInfo(verInfoKey);
		if (string.IsNullOrEmpty(text))
		{
			try
			{
				text = GetAlgorithmVersionInfo(dllPath);
			}
			catch
			{
				HikMessageBox.Show(AppLocalizationService.GetLang(errorKey));
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			versionInfo.Value = GetBuildDate(text);
			versionInfoList.Add(versionInfo);
		}
	}

	private string GetBuildDate(string versionValue)
	{
		if (versionValue.IndexOf("@") > -1)
		{
			versionValue = versionValue.Substring(0, versionValue.IndexOf("@"));
		}
		return versionValue;
	}

	private string GetAlgorithmVersionInfo(string filePath)
	{
		string result = "";
		string environmentVariable = Environment.GetEnvironmentVariable("MVDALGO_DEV_ENV");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			string text = Path.Combine(environmentVariable, filePath);
			if (File.Exists(text))
			{
				FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(text);
				string value = Regex.Match(versionInfo.FileVersion, "\\d+.\\d+.\\d+.\\d+").Value;
				string text2 = Regex.Match(versionInfo.FileDescription, "\\d{4}/\\d{2}/\\d{2}").Value.Replace("/", "");
				if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(text2))
				{
					result = "V" + value + " Build" + text2;
				}
			}
		}
		return result;
	}
}
