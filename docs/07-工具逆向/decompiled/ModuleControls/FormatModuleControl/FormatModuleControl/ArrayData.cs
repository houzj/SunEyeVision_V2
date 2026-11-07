using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;

namespace FormatModuleControl;

public class ArrayData : NameValueItem
{
	public ObservableCollection<object> _ArrayList = new ObservableCollection<object>();

	public ComboxEditForFormat delimiter;

	public ButtonSelecter_FormatIndex indexInfo;

	private DelegateCommand<ObjectEventArgs> _AddCommand = null;

	public ObservableCollection<object> ArrayList
	{
		get
		{
			return _ArrayList;
		}
		set
		{
			if (_ArrayList != value)
			{
				_ArrayList = value;
				OnPropertyChanged("ArrayList");
			}
		}
	}

	public ComboxEditForFormat Delimiter
	{
		get
		{
			if (delimiter == null)
			{
				delimiter = new ComboxEditForFormat
				{
					DisplayName = "RunParam_Split Symbol",
					Description = "RunParam_Split Symbol",
					AccessMode = "RW",
					CurValue = ";"
				};
				delimiter.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntryStr
					{
						Description = "Dot",
						DisplayName = "RunParam_Dot",
						Value = ","
					},
					new EnumEntryStr
					{
						Description = "Colon",
						DisplayName = "RunParam_Colon",
						Value = ":"
					},
					new EnumEntryStr
					{
						Description = "Title",
						DisplayName = "RunParam_Title",
						Value = ";"
					},
					new EnumEntryStr
					{
						Description = "Underline",
						DisplayName = "RunParam_Underline",
						Value = "_"
					}
				};
			}
			return delimiter;
		}
	}

	public ButtonSelecter_FormatIndex IndexInfo
	{
		get
		{
			if (indexInfo == null)
			{
				indexInfo = new ButtonSelecter_FormatIndex
				{
					Name = "下标",
					Description = "数组下标",
					DisplayName = "数组下标",
					CustomVisible = true,
					DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontFilterTreeOperation",
						OperationParams = "int"
					},
					Triggers = new ObservableCollection<object>(),
					Initers = new ObservableCollection<object>()
				};
				indexInfo.SetParentNode(this);
				indexInfo.DropOpenSetter.SetParentNode(indexInfo);
				Setter setter = new Setter
				{
					TargetName = "CurValue",
					OperationName = "GetSelectedFilterOperation"
				};
				setter.CreatOperateInstance();
				setter.SetParentNode(indexInfo);
				indexInfo.Initers.Add(setter);
			}
			return indexInfo;
		}
	}

	public ICommand AddCommand
	{
		get
		{
			if (_AddCommand == null)
			{
				_AddCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)AddCommandExcute);
			}
			return (ICommand)_AddCommand;
		}
	}

	private void AddCommandExcute(ObjectEventArgs e)
	{
		bool flag = ArrayList.Count > 0;
		int num = (flag ? ArrayList.Max((object a) => (a as ArrayInfo).RealIndex) : 0);
		ArrayInfo arrayInfo = new ArrayInfo();
		num = (arrayInfo.RealIndex = num + 1);
		arrayInfo.UpEnable = flag;
		arrayInfo.DownEnable = false;
		ArrayInfo arrayInfo2 = arrayInfo;
		if (flag)
		{
			(ArrayList[ArrayList.Count - 1] as ArrayInfo).DownEnable = true;
		}
		ArrayList.Add(arrayInfo2);
		arrayInfo2.SetParentNode(this);
		ResetIndex();
	}

	public void ResetIndex()
	{
		foreach (ArrayInfo array in ArrayList)
		{
			array.RealIndex = ArrayList.IndexOf(array);
		}
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Delimiter.CurValueA).Append("；").Append(IndexInfo.CurValueA)
			.Append("；");
		for (int i = 0; i < ArrayList.Count; i++)
		{
			stringBuilder.Append(ArrayList[i].ToString()).Append((i == ArrayList.Count - 1) ? "" : "，");
		}
		return stringBuilder.ToString();
	}

	public static ArrayData GetArrayDataFromString(string str)
	{
		ArrayData arrayData = null;
		if (!string.IsNullOrWhiteSpace(str))
		{
			try
			{
				string[] array = str.Split('；');
				if (array.Count() != 3)
				{
					throw new Exception();
				}
				arrayData = new ArrayData();
				arrayData.Delimiter.CurValueA = array[0];
				arrayData.IndexInfo.CurValueA = array[1];
				string[] array2 = array[2].Split('，');
				string[] array3 = array2;
				foreach (string text in array3)
				{
					string[] array4 = text.Split('`');
					if (array4.Count() == 2)
					{
						ArrayInfo arrayInfo = new ArrayInfo();
						if (int.TryParse(array4[0], out var result))
						{
							arrayInfo.RealIndex = result;
							arrayInfo.IndexInfo.CurValueA = array4[1];
							arrayData.ArrayList.Add(arrayInfo);
							continue;
						}
						throw new Exception();
					}
					throw new Exception();
				}
			}
			catch (Exception)
			{
				arrayData = null;
			}
		}
		return arrayData;
	}
}
