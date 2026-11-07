using System;
using System.Collections;
using System.Windows;
using Apps.Data;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class NineBox : NameValueItem
{
	public IList EnumEntrys;

	public bool IsEditable = true;

	public virtual IList EnumEntrysA
	{
		get
		{
			if (!string.IsNullOrEmpty(Name))
			{
				return EnumEntrys = (IList)Operation.GetPropertyValueByName(Name);
			}
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

	public bool IsEditableA
	{
		get
		{
			return IsEditable;
		}
		set
		{
			IsEditable = value;
			OnPropertyChanged("IsEditableA");
		}
	}

	public override void GetParamValue()
	{
		MyItemState = ItemState.Normal;
		try
		{
			if (!string.IsNullOrEmpty(Name))
			{
				EnumEntrysA = (IList)Operation.GetPropertyValueByName(Name);
			}
		}
		catch (Exception ex)
		{
			MyItemState = ItemState.GetParamErro;
			OnGetValueCompleted(success: false);
			string text = ((string)Application.Current.TryFindResource("Lang_ParamGet")) ?? "（获取参数）";
			if (IsGetShowError)
			{
				HikMessageBox.Show(text + ex.ToString(), MessageIcons.Error);
			}
		}
	}
}
