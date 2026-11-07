using System.ComponentModel;
using Apps.Interface;
using Hik.Xml;
using SaveImageCs;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class FrontDisplayItem : BaseNode, INotifyPropertyChanged, IOwner
{
	private object source;

	private object requester;

	public bool IsDisplay = true;

	private object _Owner;

	public object Source
	{
		get
		{
			return source;
		}
		set
		{
			if (source != value)
			{
				source = value;
				OnPropertyChanged("Source");
			}
		}
	}

	public object Requester
	{
		get
		{
			return requester;
		}
		set
		{
			if (requester != value)
			{
				requester = value;
				OnPropertyChanged("Requester");
			}
		}
	}

	public bool IsDisplayA
	{
		get
		{
			return IsDisplay;
		}
		set
		{
			if (IsDisplay == value)
			{
				return;
			}
			if (Source == null || Requester == null)
			{
				IsDisplay = false;
				return;
			}
			if (value)
			{
				SaveImageHelper.SetSaveStatus(Source as Object, Requester as VmModule, Owner as VmModule, isVis: true);
			}
			else
			{
				SaveImageHelper.SetSaveStatus(Source as Object, Requester as VmModule, Owner as VmModule, isVis: false);
			}
			IsDisplay = value;
			OnPropertyChanged("IsDisplayA");
		}
	}

	public object Owner
	{
		get
		{
			return _Owner;
		}
		set
		{
			_Owner = value;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}
}
