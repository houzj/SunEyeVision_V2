using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace OCRModuleCommonControl;

public class SingleFilterData : NotifyPropertyChangedBase
{
	private int index;

	private bool fontFilterEnable = false;

	private Integer fontFilterNum;

	private string fontFilterInfo;

	private OCRFilerUIData oCRFilerUIData = new OCRFilerUIData();

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

	public bool FontFilterEnable
	{
		get
		{
			return fontFilterEnable;
		}
		set
		{
			if (fontFilterEnable != value)
			{
				fontFilterEnable = value;
				OnPropertyChanged("FontFilterEnable");
			}
		}
	}

	public Integer FontFilterNum
	{
		get
		{
			if (fontFilterNum == null)
			{
				fontFilterNum = new Integer
				{
					DefaultValue = 1,
					MaxValue = 50,
					MinValue = 1,
					IncValue = 1,
					SubscribeVisible = false
				};
				fontFilterNum.OnCurValueChanged += FontFilterNum_OnCurValueChanged;
			}
			return fontFilterNum;
		}
	}

	public string FontFilterInfo
	{
		get
		{
			return fontFilterInfo;
		}
		set
		{
			if (fontFilterInfo != value)
			{
				fontFilterInfo = value;
				OnPropertyChanged("FontFilterInfo");
			}
		}
	}

	public OCRFilerUIData OCRFilerUIData
	{
		get
		{
			return oCRFilerUIData;
		}
		set
		{
			if (oCRFilerUIData != value)
			{
				oCRFilerUIData = value;
				OnPropertyChanged("OCRFilerUIData");
			}
		}
	}

	public void Init(string version)
	{
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Expected Obj, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Expected Obj, but got Unknown
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Expected Obj, but got Unknown
		string text = FontFilterInfo;
		List<char> list = new List<char>();
		list.AddRange(CustomPanelConfigUI.num);
		list.AddRange(CustomPanelConfigUI.capital);
		list.AddRange(CustomPanelConfigUI.lower);
		list.AddRange(CustomPanelConfigUI.spe);
		if (!string.IsNullOrEmpty(text))
		{
			string[] array = text.Split(',');
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				OCRFilerItem val = null;
				if (text2.Length == 1)
				{
					switch (text2)
					{
					case "0":
					case "1":
					case "2":
					case "3":
					case "4":
					case "5":
					case "7":
						val = new OCRFilerItem
						{
							parent = OCRFilerUIData
						};
						val.FilerData = byte.Parse(text2);
						break;
					}
				}
				else
				{
					try
					{
						if (text2.Length > 2)
						{
							string text3 = text2.Substring(2);
							StringBuilder stringBuilder = new StringBuilder();
							if (version == "OldVersion")
							{
								string s = "0000000000000000";
								string s2 = text3;
								if (text3.Length > 16)
								{
									s = text3.Substring(0, text3.Length - 16);
									s2 = text3.Substring(text3.Length - 16, 16);
								}
								ulong num = ulong.Parse(s, NumberStyles.AllowHexSpecifier);
								ulong num2 = ulong.Parse(s2, NumberStyles.AllowHexSpecifier);
								ulong num3 = 1uL;
								for (int j = 0; j < 64 && j < list.Count; j++)
								{
									if ((num2 & num3) != 0)
									{
										char value = list[j];
										stringBuilder.Append(value);
									}
									num3 <<= 1;
								}
								num3 = 1uL;
								for (int k = 0; k < 64 && k < list.Count - 64; k++)
								{
									if ((num & num3) != 0)
									{
										char value2 = list[k + 64];
										stringBuilder.Append(value2);
									}
									num3 <<= 1;
								}
							}
							else
							{
								stringBuilder.Append(text3);
							}
							val = new OCRFilerItem
							{
								parent = OCRFilerUIData
							};
							val.FilerData = 6;
							val.CustomString = stringBuilder.ToString();
						}
						else
						{
							val = new OCRFilerItem
							{
								parent = OCRFilerUIData
							};
							val.FilerData = 6;
							val.CustomString = "";
						}
					}
					catch (Exception message)
					{
						LogHelper.Error(message);
					}
				}
				if (val == null)
				{
					OCRFilerUIData.Collection.Clear();
					break;
				}
				OCRFilerUIData.Collection.Add(val);
			}
		}
		FontFilterNum.CurValueA = OCRFilerUIData._Collection.Count;
		if (OCRFilerUIData._Collection.Count > 0)
		{
			OCRFilerUIData.curSelectedItem = OCRFilerUIData._Collection[0];
		}
	}

	private void FontFilterNum_OnCurValueChanged(object sender, EventArgs e)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected Obj, but got Unknown
		if (FontFilterNum != null)
		{
			int curValue = FontFilterNum.CurValue;
			while (OCRFilerUIData.Collection.Count > curValue)
			{
				OCRFilerUIData.Collection.Remove(OCRFilerUIData.Collection.Last());
			}
			while (OCRFilerUIData.Collection.Count < curValue)
			{
				OCRFilerUIData.Collection.Add(new OCRFilerItem
				{
					parent = OCRFilerUIData
				});
			}
		}
	}

	public void SetParam(IParamsConfig paramsConfig, string version, bool isCustom)
	{
		string text = (isCustom ? "CustomFontFilterEnable-" : "FontFilterEnable-");
		string text2 = (isCustom ? "CustomFontFilterInfo-" : "FontFilterInfo-");
		StringBuilder stringBuilder = new StringBuilder();
		foreach (OCRFilerItem item in OCRFilerUIData.Collection)
		{
			stringBuilder.Append(item.GetMsg(FontFilterEnable, version)).Append(',');
		}
		string text3 = stringBuilder.ToString(0, stringBuilder.Length - 1);
		FontFilterInfo = text3;
		try
		{
			((IUserStringData)paramsConfig)[text2 + Index] = FontFilterInfo;
			((IUserStringData)paramsConfig)[text + Index] = (FontFilterEnable ? "True" : "False");
		}
		catch (Exception ex)
		{
			string text4 = "ROI-" + Index + AppLocalizationService.GetLang("设置") + AppLocalizationService.GetLang("失败");
			if (ex is VmException ex2)
			{
				text4 += ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			throw new Exception(text4);
		}
	}

	public SingleFilterData()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected Obj, but got Unknown
	}
}
