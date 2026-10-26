using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Commom;

public class CurrentUIHelper : DependencyObject
{
	private static readonly CurrentUIHelper instance;

	public static CurrentUIHelper Instance => instance;

	static CurrentUIHelper()
	{
		instance = new CurrentUIHelper();
	}

	private CurrentUIHelper()
	{
	}
}
