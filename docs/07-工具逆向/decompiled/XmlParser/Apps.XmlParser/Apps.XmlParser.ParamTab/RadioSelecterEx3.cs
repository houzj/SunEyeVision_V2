using System;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using VM.Controls;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class RadioSelecterEx3 : RadioSelecter
{
	public override string DisplayType => "RadioSelecter3";

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
				int num = Convert.ToInt32(value);
				if (num != CurValueA)
				{
					CurValueA = num;
				}
			}
		}
	}

	public override int GetParamUnit()
	{
		MyItemState = ItemState.Normal;
		string text = string.Empty;
		IParamsConfig config = GetConfig();
		if (config != null)
		{
			text = ((IUserStringData)config)[Name];
		}
		if (!string.IsNullOrEmpty(text))
		{
			string text2 = text;
			try
			{
				Check(text2);
				if (Value != text2)
				{
					OldValue = text2;
					Value = text2;
				}
			}
			catch (Exception ex)
			{
				string lang = AppLocalizationService.GetLang("Lang_ParamGet");
				HikMessageBox.Show(lang + ex.Message, MessageIcons.Error);
				LogHelper.Error("获取失败！！！！！名称：" + Name + " des:" + Description + "  获取值:" + text);
			}
			return 0;
		}
		MyItemState = ItemState.GetParamErro;
		return -1;
	}

	public override int SetParamUnit()
	{
		string value = Value;
		if (string.IsNullOrWhiteSpace(value))
		{
			MyItemState = ItemState.SetParamErro;
			return -2;
		}
		if (value == OldValue)
		{
			return 0;
		}
		MyItemState = ItemState.Normal;
		try
		{
			IParamsConfig config = GetConfig();
			if (config != null)
			{
				((IUserStringData)config)[Name] = value;
			}
		}
		catch
		{
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return -2;
		}
		OldValue = value;
		OnSetValueCompleted(success: true);
		return 0;
	}
}
