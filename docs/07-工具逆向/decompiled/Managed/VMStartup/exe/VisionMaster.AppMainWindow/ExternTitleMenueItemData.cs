using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;
using Apps.Localization;
using Hik.Xml;
using ModuleParamTool;
using UIVersionControlManager;
using VM.Controls;

namespace VisionMaster.AppMainWindow;

public class ExternTitleMenueItemData : TitleMenueItemData
{
	public string initMethodName;

	public string info;

	public string InitMethodName => initMethodName;

	public string Info => info;

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		ExcuteMethodByName(InitMethodName);
		return this;
	}

	public void CheckPath()
	{
		CheckPath(Info);
	}

	private void CheckPath(string path)
	{
		if (!File.Exists(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + path))
		{
			IsDisplayInMenu = false;
		}
	}

	public void OpenExe()
	{
		OpenExe(Info);
	}

	private void OpenExe(string path)
	{
		try
		{
			string fullPath = Path.GetFullPath(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + path);
			ProcessStartInfo processStartInfo = new ProcessStartInfo(fullPath, AppLocalizationService.GetLang("Language"));
			processStartInfo.CreateNoWindow = false;
			processStartInfo.WorkingDirectory = fullPath.Substring(0, fullPath.LastIndexOf("\\"));
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	public void OpenExeByLang()
	{
		OpenExe(GetPathByLang());
	}

	public void OpenModuleParamTool()
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected Obj, but got Unknown
		foreach (Window window in Application.Current.Windows)
		{
			if (window.GetType() == typeof(MainWindow))
			{
				return;
			}
		}
		if (UIVersionControlManagerCS.IsDongleLowerThanConfig(UIVersionControlManagerCS.DongleSeriesEnum.PRO))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InsufficientDonglePermissions"));
			return;
		}
		MainWindow val = new MainWindow();
		((BaseWindow)(object)val).Show();
	}

	public void CheckPathByLang()
	{
		CheckPath(GetPathByLang());
	}

	private string GetPathByLang()
	{
		IEnumerable<Tuple<CultureInfo, string>> source = from p in info.Split(';')
			select p.Split(':') into p
			where p.Length == 2
			select new Tuple<CultureInfo, string>(CultureInfo.GetCultureInfo(p[0]), p[1]);
		string langSet = AppLocalizationService.GetLangParm();
		Tuple<CultureInfo, string> tuple = source.Single((Tuple<CultureInfo, string> p) => p.Item1.Equals(CultureInfo.GetCultureInfo(langSet)));
		return tuple.Item2;
	}
}
