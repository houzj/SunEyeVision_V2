using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class TimeConfig : NameValueItem
{
	public ObservableCollection<TimeStatisticItem> _TimeStatisticItems;

	private ICommand _AddCommond = null;

	public bool isInit = false;

	private VmModule Owner => GetVmModule();

	public ObservableCollection<TimeStatisticItem> TimeStatisticItems
	{
		get
		{
			if (_TimeStatisticItems == null)
			{
				_TimeStatisticItems = new ObservableCollection<TimeStatisticItem>();
			}
			return _TimeStatisticItems;
		}
		set
		{
			if (_TimeStatisticItems != value)
			{
				_TimeStatisticItems = value;
				OnPropertyChanged("TimeStatisticItems");
			}
		}
	}

	public ICommand AddCommond
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_AddCommond == null)
			{
				_AddCommond = (ICommand)new DelegateCommand((Action)AddCmdExecute);
			}
			return _AddCommond;
		}
	}

	public ObservableCollection<VmModule> StartModules { get; set; } = new ObservableCollection<VmModule>();

	public ObservableCollection<VmModule> EndModules { get; set; } = new ObservableCollection<VmModule>();

	public ObservableCollection<VmModule> AllModules { get; set; } = new ObservableCollection<VmModule>();

	public void AddCmdExecute()
	{
		if (TimeStatisticItems.Count >= 64)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("TimeConfigCountError"));
			return;
		}
		TimeStatisticItem statisticItem = new TimeStatisticItem();
		int num = 0;
		int i;
		for (i = 0; TimeStatisticItems.Where((TimeStatisticItem c) => c.TimeSeq == i).Count() != 0; i++)
		{
		}
		num = i;
		statisticItem.TimeSeq = num;
		StartModules.ToList().ForEach((VmModule x) =>
		{
			statisticItem.StartModules.Add(x);
		});
		StartModules.ToList().ForEach((VmModule x) =>
		{
			statisticItem.EndModules.Add(x);
		});
		StartModules.ToList().ForEach((VmModule x) =>
		{
			statisticItem.AllModules.Add(x);
		});
		statisticItem.timeConfig = this;
		string name = AppLocalizationService.GetLang("Lang_Time") + num;
		AddIO(num, ref name);
		statisticItem.TimeName = name;
		TimeStatisticItems.Add(statisticItem);
		SetTimeParam();
	}

	public override int GetParamUnit()
	{
		try
		{
			isInit = true;
			VmModule vmModule = GetVmModule();
			if (vmModule == null || Owner == null)
			{
				return 1;
			}
			StartModules.Clear();
			EndModules.Clear();
			AllModules.Clear();
			TimeStatisticItems.Clear();
			int num = vmModule.FrontModules.Count - 1;
			for (int num2 = num; num2 >= 0; num2--)
			{
				object obj = vmModule.FrontModules[num2];
				if (obj is VmModuleSet)
				{
					if (obj is IMVSGroup item)
					{
						StartModules.Add(item);
						EndModules.Add(item);
						AllModules.Add(item);
					}
				}
				else if (obj is VmModule item2)
				{
					StartModules.Add(item2);
					EndModules.Add(item2);
					AllModules.Add(item2);
				}
			}
			byte[] bt = ((IUserBytesData)Owner.Params)["TimeStatisticText"];
			string text = StringUtil.UTF8GetString(bt);
			if (!string.IsNullOrEmpty(text))
			{
				string[] array = text.Split(';');
				string[] array2 = array;
				foreach (string text2 in array2)
				{
					string[] array3 = text2.Split(':');
					if (array3.Count() != 3)
					{
						continue;
					}
					string text3 = array3[0].Replace("%", "");
					int num3 = -1;
					if (!string.IsNullOrEmpty(array3[1]))
					{
						num3 = Convert.ToInt32(array3[1]);
					}
					int num4 = -1;
					if (!string.IsNullOrEmpty(array3[2]))
					{
						num4 = Convert.ToInt32(array3[2]);
					}
					TimeStatisticItem timeStatisticItem = new TimeStatisticItem();
					timeStatisticItem.StartModules = StartModules;
					timeStatisticItem.EndModules = EndModules;
					timeStatisticItem.AllModules = AllModules;
					timeStatisticItem.timeConfig = this;
					timeStatisticItem.TimeName = text3;
					timeStatisticItem.TimeSeq = Convert.ToInt32(text3.Replace("Time", "").Replace("时间", ""));
					foreach (object frontModule in Owner.FrontModules)
					{
						if (frontModule is VmModuleSet)
						{
							if (frontModule is IMVSGroup iMVSGroup)
							{
								if (iMVSGroup.ID == num3)
								{
									timeStatisticItem.StartModule = iMVSGroup;
								}
								if (iMVSGroup.ID == num4)
								{
									timeStatisticItem.EndModule = iMVSGroup;
								}
							}
						}
						else if (frontModule is VmModule vmModule2)
						{
							if (vmModule2.ID == num3)
							{
								timeStatisticItem.StartModule = vmModule2;
							}
							if (vmModule2.ID == num4)
							{
								timeStatisticItem.EndModule = vmModule2;
							}
						}
					}
					TimeStatisticItems.Add(timeStatisticItem);
				}
			}
			isInit = false;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
		finally
		{
			isInit = false;
		}
		return 0;
	}

	public void SetTimeParam()
	{
		try
		{
			if (Owner == null)
			{
				return;
			}
			string text = "";
			foreach (TimeStatisticItem timeStatisticItem in TimeStatisticItems)
			{
				string text2 = "";
				string text3 = "";
				if (timeStatisticItem.StartModule != null)
				{
					text2 = timeStatisticItem.StartModule.ID.ToString();
				}
				if (timeStatisticItem.EndModule != null)
				{
					text3 = timeStatisticItem.EndModule.ID.ToString();
				}
				string text4 = "%" + timeStatisticItem.TimeName + "%:" + text2 + ":" + text3 + ";";
				text += text4;
			}
			((IUserBytesData)Owner.Params)["TimeStatisticText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void AddIO(int Seq, ref string name)
	{
		if (Owner != null)
		{
			AddLeafIO(LeafIOTypeEnum.FLOAT.ToString().ToLower(), Owner, Owner.Outputs, ref name);
		}
	}

	protected VmIO AddLeafIO(string typeName, object parent, IList parentList, ref string name)
	{
		if (GetVmModule() == null)
		{
			return null;
		}
		FixName(ref name);
		name = GetUniqueName(name);
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = name,
			Root = GetVmModule(),
			Parent = parent,
			TypeName = typeName,
			IsResultShow = true
		};
		parentList.Add(dynamicVmIO);
		return dynamicVmIO;
	}

	private string GetUniqueName(string startName)
	{
		if (Owner == null)
		{
			return "";
		}
		IEnumerable<string> source = from p in Owner.Inputs.GetAllIO().Concat(Owner.Outputs.GetAllIO())
			select p.Name;
		string text = startName;
		if (!source.Contains("%" + text + "%"))
		{
			return text;
		}
		int num = 0;
		while (true)
		{
			text = startName + num;
			if (!source.Contains("%" + text + "%"))
			{
				break;
			}
			num++;
		}
		return text;
	}

	public static void FixName(ref string name)
	{
		if (name.Length > 32)
		{
			name = name.Substring(0, 32);
		}
	}
}
