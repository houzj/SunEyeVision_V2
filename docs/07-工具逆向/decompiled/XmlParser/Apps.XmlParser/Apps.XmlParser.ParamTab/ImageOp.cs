using System;
using Apps.Data;
using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class ImageOp : NameValueItem
{
	public string CurValue;

	public double Width = 300.0;

	public double Height = 300.0;

	public string ImagePath = "";

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA;
		}
		set
		{
			CurValueA = value;
		}
	}

	public double WidthA
	{
		get
		{
			return Width;
		}
		set
		{
			Width = value;
			OnPropertyChanged("WidthA");
		}
	}

	public double HeightA
	{
		get
		{
			return Height;
		}
		set
		{
			Height = value;
			OnPropertyChanged("HeightA");
		}
	}

	public string ImagePathA
	{
		get
		{
			return ImagePath;
		}
		set
		{
			ImagePath = value;
			OnPropertyChanged("ImagePathA");
		}
	}

	public override void GetParamValue()
	{
		MyItemState = ItemState.Normal;
		try
		{
			CurValueA = (string)Operation.GetPropertyValueByName(Name);
		}
		catch (Exception ex)
		{
			MyItemState = ItemState.GetParamErro;
			OnGetValueCompleted(success: false);
			string lang = AppLocalizationService.GetLang("Lang_ParamGet", "（获取参数）");
			if (IsGetShowError)
			{
				HikMessageBox.Show(lang + ex.ToString(), MessageIcons.Error);
			}
		}
	}
}
