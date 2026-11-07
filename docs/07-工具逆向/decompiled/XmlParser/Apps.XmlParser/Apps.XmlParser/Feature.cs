using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser;

public class Feature : FeatureBase
{
	private string oriValue;

	private string tranceValue;

	private string oriFullValue;

	private string curReturnValue;

	public override string MapValueA
	{
		get
		{
			string text = Value;
			if (Mapping != null && Mapping != "NULL" && Mapping != "")
			{
				if (IsMultiMappingFlag)
				{
					text = AppLocalizationService.GetLang(text);
					string[] array = Value.Replace("{ID}", "\rID\r").Split('{');
					string[] array2 = Mapping.Split(',');
					for (int i = 0; i < array2.Length; i++)
					{
						try
						{
							string text2 = array[i + 1].Substring(0, array[i + 1].IndexOf('}'));
							text = text.Replace("{" + text2 + "}", "{" + i + "}");
						}
						catch
						{
						}
					}
				}
				else
				{
					text = AppLocalizationService.GetLang(Mapping);
				}
			}
			return text;
		}
	}

	public override string ValueA
	{
		get
		{
			if (Value == null)
			{
				return null;
			}
			return curReturnValue;
		}
		set
		{
			if (!(Value != value))
			{
				return;
			}
			if (value.ByteCount() > 512)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ContentTooLarge") + AppLocalizationService.GetLang("长度上限") + " : 512");
				return;
			}
			Value = value;
			float.TryParse(Value, out _Value_Float);
			int.TryParse(Value, out _Value_Int);
			if (Name == "Content" || Name == "Tips" || Name == "PositionX" || Name == "PositionY")
			{
				string text = ((value == null) ? "" : value);
				string text2 = "";
				while (text.Contains("{") && text.Contains("}"))
				{
					string text3 = text.Substring(text.IndexOf("{") + 1, text.IndexOf("}") - text.IndexOf("{") - 1);
					string text4 = text3;
					VmIO mapIO = GetMapIO(text3);
					if (mapIO != null)
					{
						text4 = ((!text3.Contains('.')) ? mapIO.Name : (mapIO.Root.ID + " " + mapIO.Root.Name + "." + mapIO.Name));
					}
					text2 = text2 + text4 + ",";
					text = text.Remove(text.IndexOf("{" + text3 + "}", 0), ("{" + text3 + "}").Length);
				}
				if (text2.Length > 0)
				{
					text2 = text2.Remove(text2.Length - 1);
				}
				(GetParentNode() as Object)?.RemoveOutputFilter();
				MappingA = text2;
				(GetParentNode() as Object)?.SetOutputFilter();
			}
			else
			{
				curReturnValue = value;
				(GetParentNode() as Object)?.InitVmIOs();
			}
			(GetParentNode() as Object)?.ClearTempValue();
			(GetParentNode() as Object)?.OnValueChanged(new PropertyEventArgs
			{
				PropertyName = NameA
			});
			OnPropertyChanged("ValueA");
			SaveValue(Value, (GetParentNode() as Object).NameA);
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		Initializer();
		return this;
	}

	internal override void Initializer()
	{
		oriValue = Value;
		tranceValue = Value;
		if (!string.IsNullOrEmpty(tranceValue) && !StringUtil.IsNum(tranceValue))
		{
			tranceValue = AppLocalizationService.GetLang(tranceValue);
		}
		if (!IsMultiMapping())
		{
			return;
		}
		oriFullValue = oriValue;
		if (string.IsNullOrEmpty(Value) || string.IsNullOrEmpty(Mapping))
		{
			return;
		}
		string[] indexMap = GetIndexMap(Value);
		string[] array = Mapping.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			try
			{
				string text = indexMap[i + 1].Substring(0, indexMap[i + 1].IndexOf('}'));
				string allMapDisplayName = GetAllMapDisplayName(array[i]);
				oriFullValue = oriFullValue.Replace("{" + text + "}", "{" + allMapDisplayName + "}");
			}
			catch (Exception ex)
			{
				LogHelper.Error("结果显示字符串翻译异常:" + ex.Message);
			}
		}
	}

	private static string GetAllMapDisplayName(string allMapName)
	{
		string[] array = allMapName.Split('.');
		string empty = string.Empty;
		if (array.Length > 1)
		{
			return array[0] + "." + (AppLocalizationService.GetLang(array[1]) ?? array[1]);
		}
		return AppLocalizationService.GetLang(array[0]) ?? array[0];
	}

	public override void InitVmIOs()
	{
		base.InitVmIOs();
		Initializer();
		RefreshParseValue();
		if (needUpdateMapValue == true)
		{
			UpdateMapValue();
		}
	}

	protected override void RefreshParseValue()
	{
		curReturnValue = ((Value == oriValue) ? tranceValue : Value);
		if (curReturnValue == null)
		{
			curReturnValue = "0";
		}
		curReturnValue = curReturnValue.Replace("{ID}", "NEED_NOT_REPLACE_STTRING");
		curReturnValue = curReturnValue.Replace("{ModuleDisplayName}", (Owner as VmModule)?.Name);
		if (isMappingValid)
		{
			if (IsMultiMappingFlag)
			{
				string[] indexMap = GetIndexMap(Value);
				for (int i = 0; i < MappingArray.Length; i++)
				{
					try
					{
						if (indexMap.Length - 1 >= i + 1)
						{
							string text = indexMap[i + 1].Substring(0, indexMap[i + 1].IndexOf('}'));
							string allMapDisplayName = GetAllMapDisplayName(MappingArray[i]);
							curReturnValue = curReturnValue.Replace("{" + text + "}", "{" + allMapDisplayName + "}");
						}
					}
					catch (Exception ex)
					{
						LogHelper.Error("结果显示字符串翻译异常:" + ex.Message);
					}
				}
			}
			else
			{
				string[] array = Mapping.Split('.');
				if (array.Length <= 1)
				{
					curReturnValue = AppLocalizationService.GetLang(Mapping) ?? Mapping;
				}
			}
		}
		ParsedValue = curReturnValue;
		string[] indexMap2 = GetIndexMap(ParsedValue);
		for (int j = 0; j < MappingArray.Length; j++)
		{
			if (j + 1 < indexMap2.Length)
			{
				string text2 = indexMap2[j + 1].Substring(0, indexMap2[j + 1].IndexOf('}'));
				if (!string.IsNullOrEmpty(text2))
				{
					ParsedValue = ParsedValue.Replace("{" + text2 + "}", "{" + j + "}");
				}
			}
		}
	}

	public void SetMapping(string tempMapping)
	{
		MappingA = tempMapping;
		(GetParentNode() as Object)?.ClearTempValue();
		(GetParentNode() as Object)?.OnValueChanged(new PropertyEventArgs
		{
			PropertyName = NameA
		});
		OnPropertyChanged("ValueA");
		SaveValue(Value, (GetParentNode() as Object).NameA);
	}

	internal void RefreshResult()
	{
		ExecuteCount = null;
		vmIOs.ForEach((VmIO p) =>
		{
			p.Root.RefreshResultForce();
		});
	}

	public override VmModule GetModuleByNodeID(int nodeID)
	{
		throw new NotImplementedException();
	}

	public override VmIO GetOutputFilter(object owner, string mapValue)
	{
		return (owner as VmModule)?.GetIO(mapValue);
	}

	public void PasteFeature(Dictionary<object, object> map)
	{
		string text = ValueA;
		foreach (VmIO vmIO in vmIOs)
		{
			IVmModule root;
			if ((root = vmIO.Root) != null && map.Keys.Contains(root) && map[root] is IVmModule)
			{
				string text2 = $"{root.ID} {root.Name}";
				string text3 = $"{(map[root] as IVmModule).ID} {(map[root] as IVmModule).Name}";
				if (!string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text3))
				{
					text = text?.Replace(text2, text3);
				}
			}
		}
		if (text != ValueA)
		{
			ValueA = text;
		}
	}
}
