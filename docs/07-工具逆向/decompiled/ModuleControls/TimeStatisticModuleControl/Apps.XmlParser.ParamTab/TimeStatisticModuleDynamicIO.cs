using System;
using Apps.Interface;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class TimeStatisticModuleDynamicIO : DynamicIOContainer<TsDynamicIO>
{
	private bool _isAllowSet = true;

	private DynamicIOContainer<TsDynamicIO> outputio;

	public bool IsAllowSet
	{
		get
		{
			return _isAllowSet;
		}
		set
		{
			if (_isAllowSet != value)
			{
				_isAllowSet = value;
				OnPropertyChanged("IsAllowSet");
			}
		}
	}

	public DynamicIOContainer<TsDynamicIO> Outputio
	{
		get
		{
			if (outputio == null)
			{
				outputio = new DynamicIOContainer<TsDynamicIO>
				{
					IsOutput = true,
					DataSource = (Owner as VmModule)
				};
			}
			return outputio;
		}
		set
		{
			outputio = value;
		}
	}

	public TimeStatisticModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		return base.AddIO(name, type, index);
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
		foreach (TsDynamicIO item in Outputio.ItemsA)
		{
			if (item.DisplayNameA.Contains((dymaicIO as TsDynamicIO).DisplayNameA))
			{
				Outputio.RemoveIO(item);
				break;
			}
		}
	}

	public void InitSource()
	{
		GetParam();
	}

	public void GetParam()
	{
		IsAllowSet = false;
		if (Owner == null)
		{
			return;
		}
		string text = "";
		byte[] array = null;
		try
		{
			array = ((IUserBytesData)(Owner as VmModule).Params)["TimeStatisticText"];
		}
		catch (Exception ex)
		{
			LogHelper.Error("获取TimeStatisticText失败：" + ex.Message);
			IsAllowSet = true;
			return;
		}
		text = StringUtil.UTF8GetString(array);
		string[] array2 = text.Split(';');
		if (array2.Length >= 1)
		{
			foreach (TsDynamicIO item in ItemsA)
			{
				if (!item.CustomVisibleA)
				{
					continue;
				}
				try
				{
					string[] array3 = array2;
					foreach (string text2 in array3)
					{
						string[] array4 = text2.Split(':');
						if (array4.Length > 2 && array4[0] == item.NameA)
						{
							item.StartEnumeration.Value = array4[1];
							item.EndEnumeration.Value = array4[2];
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}
		IsAllowSet = true;
	}
}
