using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using VM.Controls;

namespace DataQueueModuleControl;

public class DataQueueItem : INotifyPropertyChanged
{
	internal const string NamePattern = "^[\\u4e00-\\u9fa5_a-zA-Z0-9]+$";

	private int index;

	private string _name;

	internal IList<DataQueueItem> Parent;

	private string _type = "string";

	private int BorderThickness;

	public int Index
	{
		get
		{
			return index;
		}
		set
		{
			if (index != value)
			{
				index = value;
				OnPropertyChanged("Index");
			}
		}
	}

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				HikMessageBox.Show("Lang_ParamNameErr", "Lang_NameEmpty", MessageIcons.Error, isTranslate: true);
				return;
			}
			if (DataQueueUtil.StringToByteCount(value) > 256)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamNameErr"), AppLocalizationService.GetLang("Lang_PmNameLong"), MessageIcons.Error);
				return;
			}
			string lang = AppLocalizationService.GetLang("Lang_IlgChar");
			if (!Regex.IsMatch(value, "^[\\u4e00-\\u9fa5_a-zA-Z0-9]+$"))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamErr"), lang, MessageIcons.Error);
				return;
			}
			if (Parent != null && Parent.Any((DataQueueItem p) => p.Name == value))
			{
				HikMessageBox.Show("Lang_ParamNameErr", "Lang_PmNameExited", MessageIcons.Error, isTranslate: true);
				return;
			}
			_name = value;
			OnPropertyChanged("Name");
		}
	}

	public string Type
	{
		get
		{
			return _type;
		}
		set
		{
			if (Parent != null)
			{
				if (Parent.Where((DataQueueItem p) => p.Type == "IMAGE").ToList().Count >= 4 && value.Equals("IMAGE"))
				{
					BorderThicknessA = 1;
					HikMessageBox.Show(AppLocalizationService.GetLang("最多添加图像队列数") + " : " + 4, MessageButtons.Normal, MessageIcons.Warning);
					return;
				}
				BorderThicknessA = 0;
			}
			_type = (string.IsNullOrEmpty(value) ? "string" : value);
			OnPropertyChanged("Type");
		}
	}

	public int BorderThicknessA
	{
		get
		{
			return BorderThickness;
		}
		set
		{
			if (BorderThickness != value)
			{
				BorderThickness = value;
				OnPropertyChanged("BorderThicknessA");
			}
		}
	}

	public byte[] Value { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public static implicit operator IMVS_DATAQUEUE(DataQueueItem value)
	{
		return new IMVS_DATAQUEUE
		{
			nReserved = new int[4],
			chszQueueName = DataQueueUtil.GetBytes_UTF8(256u, value.Name),
			chszDataType = value.Type,
			chszValue = value.Value
		};
	}
}
