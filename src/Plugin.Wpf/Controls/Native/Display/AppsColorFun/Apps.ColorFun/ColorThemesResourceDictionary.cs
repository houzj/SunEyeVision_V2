using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Xml;
using Apps.BaseData;
using Apps.Log;
using VM.Utility.SingleInstance;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Apps.ColorFun;

public class ColorThemesResourceDictionary : DynamicResourceDictionary
{
	private static object syncObj = new object();

	private static ColorThemesResourceDictionary instance = null;

	public static ColorThemesResourceDictionary Instance => GetInstance();

	public static ColorThemesResourceDictionary GetInstance(FrameworkElement framework = null)
	{
		if (instance == null)
		{
			lock (syncObj)
			{
				if (instance == null)
				{
					instance = new ColorThemesResourceDictionary();
					if (Application.Current != null && !Application.Current.Resources.MergedDictionaries.Contains(instance))
					{
						Application.Current.Resources.MergedDictionaries.Add(instance);
					}
				}
			}
		}
		if (framework != null)
		{
			lock (syncObj)
			{
				if (!framework.Resources.MergedDictionaries.Contains(instance))
				{
					framework.Resources.MergedDictionaries.Add(instance);
				}
			}
		}
		return instance;
	}

	private Uri LoadColorStyle(string rootPath)
	{
		string text = $"{rootPath}\\ColorCFG\\ColorStyleSet.cfg";
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(text);
		LogHelper.objLog.Info(text);
		string innerText = xmlDocument.SelectSingleNode("VisionMasterCfgRoot/ColorSet").InnerText;
		return new Uri($"{rootPath}/ColorStyle/{innerText}.xaml", UriKind.RelativeOrAbsolute);
	}

	public ColorThemesResourceDictionary()
	{
		try
		{
			if (CurrentPathHelper.Instance.IsSDKMode)
			{
				string currentPath = CurrentPathHelper.Instance.GetCurrentPath();
				Source = LoadColorStyle(currentPath);
			}
			else
			{
				string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
				Source = LoadColorStyle(baseDirectory);
			}
			foreach (DictionaryEntry item in new ResourceDictionary
			{
				Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/AppsColorFun/colorstyle.xaml", UriKind.RelativeOrAbsolute)
			})
			{
				Add(item.Key, item.Value);
			}
			LogHelper.Info("color resource loaded successfully");
		}
		catch (Exception ex)
		{
			LogHelper.Error($"set color is error：{ex.Message},{ex.StackTrace.ToString()}");
		}
	}

	public static void SetColorResource(string colorStyle)
	{
		lock (syncObj)
		{
			try
			{
				string currentPath = CurrentPathHelper.Instance.GetCurrentPath();
				Uri source = new Uri($"{currentPath}/ColorStyle/{colorStyle}.xaml", UriKind.RelativeOrAbsolute);
				instance.Source = source;
				foreach (DictionaryEntry item in new ResourceDictionary
				{
					Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/AppsColorFun/colorstyle.xaml", UriKind.RelativeOrAbsolute)
				})
				{
					instance.Add(item.Key, item.Value);
				}
				LogHelper.Info("color resource loaded successfully");
			}
			catch (Exception ex)
			{
				Debugger.Log(0, null, "set color resource is error:" + ex.ToString());
			}
		}
	}
}
