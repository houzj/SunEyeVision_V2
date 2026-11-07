using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Apps.XmlUI;
using DynamicObjectConfigControl;
using VM.CommonData;
using VM.Core;
using VM.Utility;

namespace GraphicsSetModuleControl;

internal class DisplayObjectForGraphicsSetDecorate : DisplayObjectDecorate
{
	public const string RelateWay = "RelateWay";

	public const string Region = "Region";

	public const string Status = "Status";

	private IEnumerable<VmIO> _OutputIO;

	private VmIO _StatusIO;

	private Tab RelateTab = null;

	private ICommand _ConfigCommand = null;

	public string StatusIoName => AppLocalizationService.UnDoTranslate("Status_" + ((DisplayObjectDecorate)this).NoSignName);

	public IEnumerable<VmIO> OutputIO
	{
		get
		{
			if (_OutputIO == null)
			{
				_OutputIO = ((DisplayObjectDecorate)this).Owner.Owner.Outputs.Where((VmIO p) => (p.Name.EndsWith("_" + ((DisplayObjectDecorate)this).NoSignName + "%") || p.Name.EndsWith("_" + ((DisplayObjectDecorate)this).Name + "%")) && (p.Name != ((DisplayObjectDecorate)this).Name || p.Name != StatusIoName));
			}
			return _OutputIO;
		}
	}

	public VmIO StatusIO
	{
		get
		{
			if (_StatusIO == null)
			{
				_StatusIO = ((DisplayObjectDecorate)this).Owner.Owner.Outputs.FirstOrDefault((VmIO p) => p.Name == StatusIoName);
			}
			return _StatusIO;
		}
	}

	public string RelateStr
	{
		get
		{
			List<string> list = new List<string>();
			int num = 0;
			foreach (VmIO item in OutputIO)
			{
				string text = item.SubscriptionInfo;
				if (string.IsNullOrEmpty(text))
				{
					text = item.GetSubscriptionInfo();
				}
				if (!string.IsNullOrEmpty(text))
				{
					if (OutputIO.Count() > 1)
					{
						text = GetInfo(num, text);
					}
					list.Add(text);
				}
				num++;
			}
			return string.Join(",", list);
		}
	}

	public ICommand ConfigCommand => _ConfigCommand ?? (_ConfigCommand = new DelegateCommand(ConfigCommandExecute));

	private string GetInfo(int i, string info)
	{
		info = ((i == 0) ? ((info.Last() != ')') ? ("(" + info) : info.Substring(0, info.Length - 1)) : ((info.First() != '(') ? (info + ")") : info.Substring(1, info.Length - 1)));
		return info;
	}

	private void ConfigCommandExecute()
	{
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected Obj, but got Unknown
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected Obj, but got Unknown
		//IL_00f2: Expected Obj, but got Unknown
		if (RelateTab == null)
		{
			RelateRoot instance = RelateRoot.GetInstance();
			Tab tab = instance.Items.OfType<Tab>().FirstOrDefault((Tab p) => p.Name == ((DisplayObjectDecorate)this).DecorateSource.TypeA);
			if (tab == null)
			{
				throw new Exception("类型异常");
			}
			RelateTab = tab.Clone<Tab>();
			RelateTab.SetParentNode(((DisplayObjectDecorate)this).Owner);
			ChangeTriger();
		}
		RelateTab.InitCollection();
		RelateTab.OnXmlLoaded();
		RelateTab.DoIniter();
		int num = ((AppLocalizationService.GetLangType() == "zh-cn") ? 350 : 500);
		ConfigWin configWin = new ConfigWin
		{
			Title = AppLocalizationService.GetLang("订阅配置")
		};
		ParamListView val = new ParamListView();
		((FrameworkElement)val).Width = num;
		((FrameworkElement)val).Height = 300.0;
		configWin.MainControl = (FrameworkElement)val;
		configWin.DataContext = RelateTab;
		configWin.Loaded += ConfigWindow_Loaded;
		configWin.ShowDialog();
		((NotifyPropertyChangedBase)this).OnPropertyChanged("RelateStr");
	}

	private void ConfigWindow_Loaded(object sender, RoutedEventArgs e)
	{
		GroupDisplayParam owner = ((DisplayObjectDecorate)this).Owner;
		AlgorithmTabRoot algorithmTabRoot = ((owner == null) ? null : owner.Owner?.Params.GetParamRoot());
		if (algorithmTabRoot != null && algorithmTabRoot.IsSubscribeInWindow)
		{
			algorithmTabRoot.IsSubscribeInWindow = false;
			algorithmTabRoot.IsSubscribeInWindow = true;
		}
		if (sender is Window window)
		{
			window.Loaded -= ConfigWindow_Loaded;
		}
	}

	private void ChangeTriger()
	{
		if (RelateTab == null)
		{
			return;
		}
		List<VmIO> allIO = GetAllIO();
		if (allIO.Count < 1)
		{
			throw new Exception("IO错误");
		}
		foreach (NameValueItem item in RelateTab.Collection)
		{
			switch (item.NameA)
			{
			case "Region":
				foreach (Apps.XmlParser.ParamTab.Trigger item2 in item.TriggersA)
				{
					foreach (Apps.XmlParser.ParamTab.Setter item3 in item2.SettersA)
					{
						item3.OperationParamsA = OutputIO.First().Name;
					}
				}
				foreach (Apps.XmlParser.ParamTab.Setter item4 in item.InitersA)
				{
					item4.OperationParamsA = OutputIO.First().Name;
				}
				continue;
			case "Status":
			{
				VmIO vmIO = allIO.FirstOrDefault((VmIO p) => p.Name == AppLocalizationService.UnDoTranslate("Status_" + ((DisplayObjectDecorate)this).Name));
				string operationParamsA = ((vmIO == null) ? StatusIoName : vmIO.Name);
				foreach (Apps.XmlParser.ParamTab.Trigger item5 in item.TriggersA)
				{
					foreach (Apps.XmlParser.ParamTab.Setter item6 in item5.SettersA)
					{
						item6.OperationParamsA = operationParamsA;
					}
				}
				foreach (Apps.XmlParser.ParamTab.Setter item7 in item.InitersA)
				{
					item7.OperationParamsA = operationParamsA;
				}
				continue;
			}
			case "RelateWay":
				continue;
			}
			VmIO vmIO2 = allIO.FirstOrDefault((VmIO p) => p.Name.ToLower().Contains(item.NameA.ToLower()));
			if (vmIO2 == null)
			{
				continue;
			}
			foreach (Apps.XmlParser.ParamTab.Trigger item8 in item.TriggersA)
			{
				foreach (Apps.XmlParser.ParamTab.Setter item9 in item8.SettersA)
				{
					item9.OperationParamsA = vmIO2.Name;
				}
			}
			foreach (Apps.XmlParser.ParamTab.Setter item10 in item.InitersA)
			{
				item10.OperationParamsA = vmIO2.Name;
			}
		}
	}

	internal List<VmIO> GetAllIO()
	{
		List<VmIO> list = new List<VmIO>();
		foreach (VmIO item in OutputIO)
		{
			list.AddRange(item.GetAllIO());
		}
		return list;
	}

	protected override void DeleteIOCommandExecute()
	{
		try
		{
			((DisplayObjectDecorate)this).DeleteIOCommandExecute();
			foreach (VmIO item in OutputIO)
			{
				((DisplayObjectDecorate)this).Owner.Owner.Outputs.Remove(item);
			}
			((DisplayObjectDecorate)this).Owner.Owner.Outputs.Remove(StatusIO);
			SetIoInfo();
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
	}

	private void SetIoInfo()
	{
		if (((DisplayObjectDecorate)this).Owner is GroupDisplayParamForGraphicsSet groupDisplayParamForGraphicsSet)
		{
			groupDisplayParamForGraphicsSet.SetIOInfoToModule();
		}
	}

	protected override void NameChanding(string oldVal, string newVal)
	{
		if (string.IsNullOrEmpty(oldVal) || string.IsNullOrEmpty(newVal))
		{
			LogHelper.objLog.Error("oldVal or newVal is Null");
			return;
		}
		if (oldVal != null && oldVal.Contains('%'))
		{
			oldVal = oldVal.Substring(1, oldVal.Length - 2);
		}
		if (newVal != null && newVal.Contains('%'))
		{
			newVal = newVal.Substring(1, newVal.Length - 2);
		}
		try
		{
			foreach (VmIO item in OutputIO)
			{
				ChangeIoName(item, oldVal, newVal);
			}
			if (!OutputIO.Contains(StatusIO))
			{
				ChangeIoName(StatusIO, oldVal, newVal);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
	}

	protected override void NameChanded(string oldVal, string newVal)
	{
		try
		{
			ChangeMapping();
			ChangeTriger();
			SetIoInfo();
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
	}

	internal void ChangeMapping()
	{
		List<VmIO> allIO = GetAllIO();
		foreach (Feature feature in ((DisplayObjectDecorate)this).DecorateSource.FeaturesA)
		{
			feature.MappingA = allIO.FirstOrDefault((VmIO p) => p.Name.ToLower().Contains(feature.NameA.ToLower()))?.Name;
			if (((DisplayObjectDecorate)this).ValueType == "circle" && (feature.NameA == "MajorRadius" || feature.NameA == "MinorRadius"))
			{
				feature.MappingA = allIO.FirstOrDefault((VmIO p) => p.Name.ToLower().Contains("radius"))?.Name;
			}
			if (feature.NameA.ToLower() == "Status".ToLower())
			{
				feature.MappingA = StatusIO.Name;
			}
			feature.InitVmIOs();
		}
		((DisplayObjectDecorate)this).DecorateSource.InitVmIOs();
	}

	private void ChangeIoName(VmIO io, string oldVal, string newVal)
	{
		foreach (VmIO child in io.Children)
		{
			ChangeIoName(child, oldVal, newVal);
		}
		io.Name = io.Name.Replace($"_{oldVal}%", $"_{newVal}%");
	}

	protected override void SetTabItemProperty(Tab cloneTab)
	{
		if (!(((DisplayObjectDecorate)this).DecorateSource.TypeA == "text") || cloneTab.CategorysA.Count <= 0)
		{
			return;
		}
		ObservableCollection<object> observableCollection = (cloneTab.CategorysA[0] as Apps.XmlParser.ParamTab.Category)?.ItemsA;
		if (observableCollection == null)
		{
			return;
		}
		foreach (object item in observableCollection)
		{
			if (item is ButtonSelecter buttonSelecter && (buttonSelecter.Name == "PositionX" || buttonSelecter.Name == "PositionY"))
			{
				buttonSelecter.CustomVisibleA = false;
			}
		}
	}
}
