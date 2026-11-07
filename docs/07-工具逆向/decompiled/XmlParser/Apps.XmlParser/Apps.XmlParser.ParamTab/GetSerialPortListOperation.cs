using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using Apps.Localization;
using Apps.Log;

namespace Apps.XmlParser.ParamTab;

public class GetSerialPortListOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSerialPortListOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			string[] portNames = SerialPort.GetPortNames();
			int num = portNames.Count();
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
			IList value = observableCollection;
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", value);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Debug(ex.Message);
			throw new Exception(ex.Message);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
