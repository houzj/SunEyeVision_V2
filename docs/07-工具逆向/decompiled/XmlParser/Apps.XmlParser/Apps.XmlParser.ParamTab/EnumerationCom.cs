using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class EnumerationCom : Enumeration
{
	private DelegateCommand _MouseDownCommand = null;

	public ICommand MouseDownCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_MouseDownCommand == null)
			{
				_MouseDownCommand = new DelegateCommand((Action)MouseDownCmdExecute);
			}
			return (ICommand)_MouseDownCommand;
		}
	}

	public void MouseDownCmdExecute()
	{
		EnumEntrys = GetWindowsCom();
		OnPropertyChanged("EnumEntrysA");
		OnPropertyChanged("CurValueA");
	}

	private ObservableCollection<object> GetWindowsCom()
	{
		try
		{
			string[] portNames = SerialPort.GetPortNames();
			int num = portNames.Length;
			ObservableCollection<object> observableCollection = new ObservableCollection<object>();
			string lang = AppLocalizationService.GetLang("Lang_PlzChoose");
			EnumEntry item = new EnumEntry
			{
				DisplayName = lang,
				Value = 0
			};
			observableCollection.Add(item);
			for (int i = 0; i < num; i++)
			{
				if (portNames[i].ToUpper().Contains("COM"))
				{
					EnumEntry item2 = new EnumEntry
					{
						DisplayName = portNames[i],
						Value = Convert.ToInt32(portNames[i].Remove(0, 3))
					};
					observableCollection.Add(item2);
				}
			}
			return observableCollection;
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Debug(ex.Message);
			return null;
		}
	}
}
