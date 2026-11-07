using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Apps.Localization;
using Apps.Log;

namespace Apps.XmlParser.ParamTab;

public class EnumerationAllCom : Enumeration
{
	public override ObservableCollection<object> EnumEntrysA
	{
		get
		{
			EnumEntrys = GetAllCommunicationDevices();
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public static ObservableCollection<object> GetAllCommunicationDevices()
	{
		try
		{
			ObservableCollection<object> observableCollection = new ObservableCollection<object>();
			IList<EnumEntry> list = null;
			list = (IList<EnumEntry>)CommunicationMgrHelper.GetAllCommunicationDevices(true, false, (string[])null);
			if (list != null && list.Count > 0)
			{
				observableCollection.Add(new EnumEntry
				{
					Name = AppLocalizationService.GetLang("Lang_PlzChoose"),
					DisplayName = "%" + AppLocalizationService.GetLang("Lang_PlzChoose") + "%",
					Value = 0,
					Description = "%" + AppLocalizationService.GetLang("Lang_PlzChoose") + "%"
				});
				foreach (EnumEntry item in list)
				{
					if (item.DeviceType == 0 || item.DeviceType == 2 || item.DeviceType == 3)
					{
						observableCollection.Add(item);
					}
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
