using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

public static class UIHelper
{
	public const string SiteImagePathFront = "pack://SiteOfOrigin:,,,/";

	public static T GetParent<T>(this DependencyObject source) where T : class
	{
		DependencyObject dependencyObject = VisualTreeHelper.GetParent(source);
		if (dependencyObject == null)
		{
			dependencyObject = (source as FrameworkElement)?.Parent;
		}
		if (dependencyObject == null)
		{
			return null;
		}
		while (dependencyObject != null && !(dependencyObject is T))
		{
			dependencyObject = dependencyObject.GetParent<T>() as DependencyObject;
		}
		return dependencyObject as T;
	}

	public static T GetChild<T>(DependencyObject obj) where T : DependencyObject
	{
		if (obj != null)
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(obj, i);
				if (child != null)
				{
					if (child is T result)
					{
						return result;
					}
					T child2 = GetChild<T>(child);
					if (child2 != null)
					{
						return child2;
					}
				}
			}
		}
		return null;
	}

	public static string GenerateUniqueText(int num = 8)
	{
		string text = string.Empty;
		string text2 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		char[] array = new char[num];
		byte[] array2 = Guid.NewGuid().ToByteArray();
		for (int i = 0; i < num; i++)
		{
			array[i] = text2[(array2[i] + array2[num + i]) % 35];
		}
		char[] array3 = array;
		foreach (char c in array3)
		{
			text += c;
		}
		return text;
	}

	public static bool CheckInitDirectory(FileDialog openfile, string path)
	{
		if (openfile == null)
		{
			return false;
		}
		if (path == null)
		{
			return false;
		}
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(path);
			if (directoryInfo.Exists)
			{
				openfile.InitialDirectory = path;
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsDesignMode()
	{
		bool result = false;
		if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
		{
			result = true;
		}
		else if (Process.GetCurrentProcess().ProcessName == "devenv")
		{
			result = true;
		}
		else if (Process.GetCurrentProcess().ProcessName == "LabVIEW")
		{
			string friendlyName = AppDomain.CurrentDomain.FriendlyName;
			if (friendlyName == "LabVIEW Domain for Edit")
			{
				result = true;
			}
		}
		return result;
	}

	public static T FindFirstChildByInterface<T>(DependencyObject parent) where T : class
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T result)
			{
				return result;
			}
			T val = FindFirstChildByInterface<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public static T FindAndConvertDataContext<T>(DependencyObject parent) where T : class
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is FrameworkElement { DataContext: T dataContext })
			{
				return dataContext;
			}
			T val = FindAndConvertDataContext<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}
}
