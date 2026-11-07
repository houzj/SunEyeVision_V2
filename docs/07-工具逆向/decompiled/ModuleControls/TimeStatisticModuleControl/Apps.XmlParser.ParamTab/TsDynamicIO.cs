using System;
using System.Collections.ObjectModel;
using System.Text;
using Apps.Log;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class TsDynamicIO : DynamicIO
{
	private Enumeration_TimeStatistic startEnumeration;

	private Enumeration_TimeStatistic endEnumeration;

	public Enumeration_TimeStatistic StartEnumeration
	{
		get
		{
			if (startEnumeration == null)
			{
				startEnumeration = new Enumeration_TimeStatistic
				{
					DisplayNameA = "Start",
					DescriptionA = "Start",
					AccessMode = "RW"
				};
				startEnumeration.Initers = new ObservableCollection<object>();
				Setter setter = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetLinkedModulesOperation",
					OperationParams = "start"
				};
				setter.CreatOperateInstance();
				setter.SetParentNode(startEnumeration);
				startEnumeration.Initers.Add(setter);
				startEnumeration.SetParentNode(this);
				startEnumeration.OnCurValueChanged += OnValuePropertyChanged;
			}
			if (startEnumeration != null)
			{
				startEnumeration.InniterBusiness();
				startEnumeration.OnInniterBusinessFinish();
			}
			return startEnumeration;
		}
		set
		{
			startEnumeration = value;
			OnPropertyChanged("StartEnumeration");
		}
	}

	public Enumeration_TimeStatistic EndEnumeration
	{
		get
		{
			if (endEnumeration == null)
			{
				endEnumeration = new Enumeration_TimeStatistic
				{
					Description = "End",
					DisplayName = "End",
					AccessMode = "RW"
				};
				endEnumeration.Initers = new ObservableCollection<object>();
				Setter setter = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetLinkedModulesOperation",
					OperationParams = "end"
				};
				setter.CreatOperateInstance();
				setter.SetParentNode(endEnumeration);
				endEnumeration.Initers.Add(setter);
				endEnumeration.SetParentNode(this);
				endEnumeration.OnCurValueChanged += OnValuePropertyChanged;
			}
			if (endEnumeration != null)
			{
				endEnumeration.InniterBusiness();
				endEnumeration.OnInniterBusinessFinish();
			}
			return endEnumeration;
		}
		set
		{
			endEnumeration = value;
			OnPropertyChanged("EndEnumeration");
		}
	}

	public event EventHandler ValuePropertyChanged;

	public void OnValuePropertyChanged(object sender, EventArgs e)
	{
		if (ValuePropertyChanged != null)
		{
			SetParamUnit();
		}
	}

	public override int SetParamUnit()
	{
		if ((GetParentNode() as DynamicIOContainer<TsDynamicIO>).IsOutput)
		{
			return 0;
		}
		if ((GetParentNode() as TimeStatisticModuleDynamicIO).IsAllowSet)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (TsDynamicIO item in (GetParentNode() as DynamicIOContainer<TsDynamicIO>).ItemsA)
			{
				if (item.CustomVisibleA)
				{
					stringBuilder.Append(item.NameA);
					stringBuilder.Append(":");
					stringBuilder.Append(item.StartEnumeration.Value);
					stringBuilder.Append(":");
					stringBuilder.Append(item.EndEnumeration.Value);
					stringBuilder.Append(";");
				}
			}
			if (string.IsNullOrEmpty(stringBuilder.ToString()))
			{
				stringBuilder = stringBuilder.Append("0");
			}
			try
			{
				((IUserBytesData)IoDataSource.Root.Params)["TimeStatisticText"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				LogHelper.Error("设置TimeStatisticText失败：" + ex.Message);
			}
		}
		return 0;
	}
}
