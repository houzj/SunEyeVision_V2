using System.Windows;
using Apps.Localization;
using HikPcUI.Commom;
using VM.Controls;

namespace TranslationCalibModuControl;

public class NPointListWindow : IWindow
{
	private BaseWindow commomConfigWindow = new BaseWindow
	{
		ShowInTaskbar = false
	};

	public NPointListWindow()
	{
		Init();
	}

	private void Init()
	{
		commomConfigWindow.Title = AppLocalizationService.GetLang("Edit Calibration Points");
		commomConfigWindow.FontSize = 14.0;
		commomConfigWindow.FontWeight = FontWeights.Normal;
		commomConfigWindow.MinButtonVisibility = Visibility.Collapsed;
		NPointList nPointList = new NPointList();
		commomConfigWindow.Width = 520.0;
		commomConfigWindow.Height = nPointList.PointList.Count * 30 + 138;
		commomConfigWindow.Content = nPointList;
		nPointList.Owner = commomConfigWindow;
	}

	public bool? ShowDialog()
	{
		bool? flag = commomConfigWindow.ShowDialog();
		if (flag == true)
		{
			DoWork();
		}
		return flag;
	}

	private void DoWork()
	{
	}
}
