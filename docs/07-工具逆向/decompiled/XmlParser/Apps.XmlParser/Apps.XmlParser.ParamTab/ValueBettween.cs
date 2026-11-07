using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Data;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ValueBettween : NameValueItem, ISubChild, ISubInfoCollection, IGroupRunParam, IGroupRunParamWithMultiParams
{
	internal class ValueBettweenStore
	{
		public object start;

		public object end;
	}

	public override bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible == value)
			{
				return;
			}
			CustomVisible = value;
			foreach (NameValueItem item in SubChildren.OfType<NameValueItem>())
			{
				item.CustomVisibleA = value;
			}
			OnPropertyChanged("CustomVisibleA");
		}
	}

	public IList<object> SubChildren => GetSubChildren();

	string IGroupRunParam.DisplayForGroupType => null;

	public virtual IList<object> GetSubChildren()
	{
		throw new NotImplementedException();
	}

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}

	public override int GetParamUnit()
	{
		NameValueItem nameValueItem = null;
		if (SubChildren != null && SubChildren.Count > 1)
		{
			bool flag = ParamsSource is IVmModuleSet && Name.StartsWith("%");
			for (int i = 0; i < 2; i++)
			{
				if (SubChildren[i] is NameValueItem nameValueItem2)
				{
					int num = 0;
					num = ((!flag) ? nameValueItem2.GetParamUnit() : nameValueItem2.GetParamUnit(this));
					if (num == -1)
					{
						SetChildState(nameValueItem2.MyItemState);
						return num;
					}
				}
			}
		}
		return 0;
	}

	public override int SetParamUnit()
	{
		NameValueItem nameValueItem = null;
		MyItemState = ItemState.Normal;
		if (SubChildren != null && SubChildren.Count > 1)
		{
			for (int i = 0; i < 2; i++)
			{
				if (SubChildren[i] is NameValueItem nameValueItem2)
				{
					int num = nameValueItem2.SetParamUnit();
					if (num == -1)
					{
						SetChildState(nameValueItem2.MyItemState);
						return num;
					}
				}
			}
		}
		return 0;
	}

	private void SetChildState(ItemState itemState)
	{
		if (itemState != ItemState.Normal)
		{
			MyItemState = itemState;
		}
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}

	internal virtual void SetValueToStore(ValueBettweenStore valueBettweenStore)
	{
	}

	internal virtual void GetValueToStore(ValueBettweenStore valueBettweenStore)
	{
	}

	object IGroupRunParamWithMultiParams.GetAllData()
	{
		ValueBettweenStore valueBettweenStore = new ValueBettweenStore();
		GetParamValue();
		SetValueToStore(valueBettweenStore);
		return valueBettweenStore;
	}

	void IGroupRunParamWithMultiParams.SetAllData(object data)
	{
		if (data is ValueBettweenStore valueBettweenStore)
		{
			GetValueToStore(valueBettweenStore);
			InnerSetStringParamValue((SubChildren[0] as NameValueItem).Name, valueBettweenStore.start.ToString());
			InnerSetStringParamValue((SubChildren[1] as NameValueItem).Name, valueBettweenStore.end.ToString());
		}
	}

	void IGroupRunParamWithMultiParams.Clear()
	{
	}
}
