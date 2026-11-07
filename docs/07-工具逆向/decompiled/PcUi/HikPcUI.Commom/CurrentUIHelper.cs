using System.Windows;

namespace HikPcUI.Commom;

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
