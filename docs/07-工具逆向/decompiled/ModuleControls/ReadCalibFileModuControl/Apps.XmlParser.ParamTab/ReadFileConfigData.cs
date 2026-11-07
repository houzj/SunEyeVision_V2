using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Apps.Localization;
using Apps.Log;
using VM.Core;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ReadFileConfigData : NameValueItem
{
	private ObservableCollection<object> _CalibParam = new ObservableCollection<object>();

	public ObservableCollection<object> CalibParam
	{
		get
		{
			return _CalibParam;
		}
		set
		{
			if (_CalibParam != value)
			{
				_CalibParam = value;
				OnPropertyChanged("CalibParam");
			}
		}
	}

	public override int GetParamUnit()
	{
		return ReadData();
	}

	public int ReadData(bool needAddoutput = false)
	{
		if (!((GetTopBaseNode() as AlgorithmTabRoot).Owner is IVmModule vmModule))
		{
			return -1;
		}
		string empty = string.Empty;
		try
		{
			byte[] bytes = ((IUserBytesData)vmModule.Params)[$"Get{NameA}"];
			empty = Encoding.UTF8.GetString(bytes);
		}
		catch (Exception arg)
		{
			LogHelper.objLog.Error($"Get Param Error:{arg}");
			return -1;
		}
		if (string.IsNullOrWhiteSpace(empty))
		{
			return -1;
		}
		List<object> list = AnalyzeString(empty);
		CalibParam = new ObservableCollection<object>(list);
		if (needAddoutput)
		{
			for (int num = vmModule.Outputs.Count - 1; num >= 0; num--)
			{
				if (vmModule.Outputs[num] is DynamicVmIO || vmModule.Outputs[num] is Combination)
				{
					vmModule.Outputs.Remove(vmModule.Outputs[num]);
				}
			}
			foreach (object item in list)
			{
				if (item is IOutput output)
				{
					output.AddOutput(vmModule);
				}
			}
		}
		return 0;
	}

	private List<object> AnalyzeString(string input)
	{
		List<object> list = new List<object>();
		string[] array = input.Split(';');
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split('#');
			if (array3.Count() != 2)
			{
				continue;
			}
			switch (array3[0])
			{
			case "int":
			{
				string[] array14 = array3[1].Split('$');
				string[] array15 = array14;
				foreach (string text5 in array15)
				{
					string[] array16 = text5.Split(',');
					if (array16.Count() == 2)
					{
						IntParam intParam = new IntParam
						{
							NameA = AppLocalizationService.GetLang(array16[0])
						};
						if (int.TryParse(array16[1], out var result5))
						{
							intParam.ValueA = result5;
							list.Add(intParam);
						}
					}
				}
				break;
			}
			case "float":
			{
				string[] array21 = array3[1].Split('$');
				string[] array22 = array21;
				foreach (string text7 in array22)
				{
					string[] array23 = text7.Split(',');
					if (array23.Count() == 2)
					{
						FloatParam floatParam = new FloatParam
						{
							NameA = AppLocalizationService.GetLang(array23[0])
						};
						if (float.TryParse(array23[1], out var result9))
						{
							floatParam.ValueA = result9;
							list.Add(floatParam);
						}
					}
				}
				break;
			}
			case "string":
			{
				string[] array7 = array3[1].Split('$');
				string[] array8 = array7;
				foreach (string text3 in array8)
				{
					string[] array9 = text3.Split(',');
					if (array9.Count() == 2)
					{
						list.Add(new StringParam
						{
							NameA = AppLocalizationService.GetLang(array9[0]),
							ValueA = array9[1]
						});
					}
				}
				break;
			}
			case "CalibPointList":
			{
				string[] array17 = array3[1].Split('$');
				string[] array18 = array17;
				foreach (string text6 in array18)
				{
					string[] array19 = text6.Split(',');
					if (array19.Count() < 2)
					{
						continue;
					}
					CalibPointList calibPointList = new CalibPointList
					{
						NameA = AppLocalizationService.GetLang(array19[0])
					};
					for (int num2 = 1; num2 < array19.Count(); num2++)
					{
						string[] array20 = array19[num2].Split('~');
						if (array20.Count() == 3 && float.TryParse(array20[0], out var result6) && float.TryParse(array20[1], out var result7) && float.TryParse(array20[2], out var result8))
						{
							PointR item = new PointR
							{
								X = result6,
								Y = result7,
								R = result8
							};
							calibPointList.PointList.Add(item);
						}
					}
					list.Add(calibPointList);
				}
				break;
			}
			case "CalibPointF":
			{
				string[] array10 = array3[1].Split('$');
				string[] array11 = array10;
				foreach (string text4 in array11)
				{
					string[] array12 = text4.Split(',');
					if (array12.Count() == 2)
					{
						CalibPointParam calibPointParam = new CalibPointParam
						{
							NameA = AppLocalizationService.GetLang(array12[0])
						};
						string[] array13 = array12[1].Split('~');
						if (array13.Count() == 3 && float.TryParse(array13[0], out var result2) && float.TryParse(array13[1], out var result3) && float.TryParse(array13[2], out var result4))
						{
							PointR point = new PointR
							{
								X = result2,
								Y = result3,
								R = result4
							};
							calibPointParam.Point = point;
							list.Add(calibPointParam);
						}
					}
				}
				break;
			}
			case "FloatList":
			{
				string[] array4 = array3[1].Split('$');
				string[] array5 = array4;
				foreach (string text2 in array5)
				{
					string[] array6 = text2.Split(',');
					if (array6.Count() < 2)
					{
						continue;
					}
					FloatList floatList = new FloatList
					{
						NameA = AppLocalizationService.GetLang(array6[0]),
						EnumEntrysA = new ObservableCollection<FloatParam>()
					};
					for (int k = 1; k < array6.Count(); k++)
					{
						if (float.TryParse(array6[k], out var result))
						{
							floatList.EnumEntrysA.Add(new FloatParam
							{
								ValueA = result
							});
						}
					}
					list.Add(floatList);
				}
				break;
			}
			}
		}
		return list;
	}
}
