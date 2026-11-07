using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class MultipleEnumeration : NameValueItem, ISubChild, ISubInfoCollection
{
	public string strChoice = ((string)Application.Current.TryFindResource("Lang_PlzChoose")) ?? "请选择";

	public ObservableCollection<object> EnumEntrys;

	public int CurValue;

	public int DefaultValue;

	public List<object> Enumerations;

	public List<object> EnumerationsA
	{
		get
		{
			return Enumerations;
		}
		set
		{
			if (Enumerations != value)
			{
				Enumerations = value;
			}
			OnPropertyChanged("EnumerationsA");
		}
	}

	public virtual int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public int DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
			OnPropertyChanged("DefaultValueA");
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				CurValueA = int.Parse(value);
			}
		}
	}

	public IList<object> SubChildren => Enumerations;

	public override void GetParamValue()
	{
		NameValueItem nameValueItem = null;
		if (Enumerations == null || Enumerations.Count <= 1)
		{
			return;
		}
		for (int i = 0; i < 3; i++)
		{
			if (Enumerations[i] is NameValueItem nameValueItem2)
			{
				int paramUnit = nameValueItem2.GetParamUnit();
				if (paramUnit == -1)
				{
					SetChildState(nameValueItem2.MyItemState);
				}
			}
		}
	}

	private void SetChildState(ItemState itemState)
	{
		if (itemState != ItemState.Normal)
		{
			MyItemState = itemState;
		}
	}

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}
}
