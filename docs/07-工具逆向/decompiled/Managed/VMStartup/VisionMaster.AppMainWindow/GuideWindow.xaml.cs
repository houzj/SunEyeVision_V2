using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Apps.Localization;
using VM.AppData;
using VM.Controls;
using VM.Utility;

namespace VisionMaster.AppMainWindow;

public partial class GuideWindow : BaseWindow, IComponentConnector
{
	private string _LearnStr;

	public static readonly DependencyProperty RecentlySolutionsProperty = DependencyProperty.Register("RecentlySolutions", typeof(IList), typeof(GuideWindow), new PropertyMetadata(null));

	public static readonly DependencyProperty OpenSolutionCommandProperty = DependencyProperty.Register("OpenSolutionCommand", typeof(ICommand), typeof(GuideWindow), new PropertyMetadata(null));

	public string LearnStr
	{
		get
		{
			return _LearnStr;
		}
		set
		{
			if (_LearnStr != value)
			{
				_LearnStr = value;
				OnPropertyChanged("LearnStr");
			}
		}
	}

	public IList RecentlySolutions
	{
		get
		{
			return (IList)GetValue(RecentlySolutionsProperty);
		}
		set
		{
			SetValue(RecentlySolutionsProperty, value);
		}
	}

	public ICommand OpenSolutionCommand
	{
		get
		{
			return (ICommand)GetValue(OpenSolutionCommandProperty);
		}
		set
		{
			SetValue(OpenSolutionCommandProperty, value);
		}
	}

	public GuideWindow()
	{
		InitializeComponent();
		Loaded += GuideWindow_Loaded;
		AppData appData = AppDataFactory.Instance();
		if (appData.IsUseCustomName)
		{
			LearnStr = AppLocalizationService.GetLang("Learn use") + appData.CustomName.ToUpper();
		}
		else
		{
			LearnStr = AppLocalizationService.GetLang("Learn use") + "VISIONMASTER";
		}
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "Resources\\guide_logo.png", UriKind.RelativeOrAbsolute);
		bitmapImage.EndInit();
		titleLogo.Source = bitmapImage;
		if (appData.IsHideLogo)
		{
			titleLogo.Visibility = Visibility.Collapsed;
		}
		if (appData.IsHideGuideLine)
		{
			areaGuideLine.Visibility = Visibility.Collapsed;
		}
	}

	private void GuideWindow_Loaded(object sender, RoutedEventArgs e)
	{
		buttonShow.IsChecked = !GuideHelper.IsGuideWindowShow();
	}

	private void LocalHelp_OnClick(object sender, RoutedEventArgs e)
	{
		GuideHelper.GetHelp(local: true);
	}

	private void OnLineHelp_OnClick(object sender, RoutedEventArgs e)
	{
		GuideHelper.GetHelp(local: false);
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}

	private void DoNotShow_OnChecked(object sender, RoutedEventArgs e)
	{
		GuideHelper.SetGuideWindowOff();
	}

	private void DoNotShow_OnUnchecked(object sender, RoutedEventArgs e)
	{
		GuideHelper.SetGuideWindowOn();
	}

	private void GuideButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (sender is GuideButton guideButton)
		{
			ConfigurationHelper.SaveConfig("ToolBoxType", guideButton.ToolBoxType.ToString());
		}
		Close();
	}

	private void Open_Demo(object sender, RoutedEventArgs e)
	{
		GuideHelper.OpenSolution(opendemo: true, out var filename);
		if (File.Exists(filename))
		{
			OpenSolutionCommand?.Execute(filename);
			Close();
		}
	}

	private void OpenFile_OnClick(object sender, RoutedEventArgs e)
	{
		OpenSolutionCommand?.Execute(null);
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
