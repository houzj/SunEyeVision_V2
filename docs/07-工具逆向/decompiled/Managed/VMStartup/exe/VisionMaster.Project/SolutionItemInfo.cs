using System;
using System.IO;
using System.Windows.Input;
using System.Xml.Serialization;
using Apps.Data;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;
using VisionMaster.AppMainWindow;

namespace VisionMaster.Project;

[XmlRoot("SolutionInfo")]
public class SolutionItemInfo : NotifyPropertyChangedBase
{
	private string _solutionPath;

	private DateTime _time;

	private ICommand _SelectSolutionCommand = null;

	[XmlAttribute("Path")]
	public string SolutionPath
	{
		get
		{
			return _solutionPath;
		}
		set
		{
			if (_solutionPath != value)
			{
				_solutionPath = value;
				OnPropertyChanged("SolutionPath");
			}
		}
	}

	[XmlAttribute("Time")]
	public DateTime LastTime
	{
		get
		{
			return _time;
		}
		set
		{
			if (_time != value)
			{
				_time = value;
				OnPropertyChanged("LastTime");
			}
		}
	}

	[XmlIgnore]
	public string FileName
	{
		get
		{
			return Path.GetFileNameWithoutExtension(SolutionPath);
		}
		set
		{
			OnPropertyChanged("FileName");
		}
	}

	private string strDay => AppLocalizationService.GetLang("Lang_Day");

	private string strHour => AppLocalizationService.GetLang("Lang_Hour");

	private string strJust => AppLocalizationService.GetLang("Lang_JustNow");

	[XmlIgnore]
	public string LastTimeStr
	{
		get
		{
			TimeSpan timeSpan = GeTimeSpan();
			if (timeSpan.Days >= 1)
			{
				return $"{timeSpan.Days}" + strDay;
			}
			if (timeSpan.Hours >= 1)
			{
				return $"{timeSpan.Hours}" + strHour;
			}
			return (timeSpan.Hours < 1) ? strJust : string.Empty;
		}
		set
		{
			OnPropertyChanged("LastTimeStr");
		}
	}

	public ICommand SelectSolutionCommand => _SelectSolutionCommand ?? (_SelectSolutionCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectSolutionCommandExecute));

	public TimeSpan GeTimeSpan()
	{
		return new TimeSpan(DateTime.Now.Ticks).Subtract(new TimeSpan(LastTime.Ticks)).Duration();
	}

	private void SelectSolutionCommandExecute(object obj)
	{
		ProjectManager.OpenRecentSolutionInfo = this;
		GuideWindow val;
		if ((val = (GuideWindow)((obj is GuideWindow) ? obj : null)) != null)
		{
			((BaseWindow)(object)val).Close();
		}
	}
}
