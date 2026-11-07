using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ItemInterface;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Controls.Utility;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class AlgorithmTabRoot : BaseNode, INotifyPropertyChanged, IOwner, IRoot, IUpdateItemRoot, IGetNode, IDisposable
{
	public static bool NeedNotifyLoaded = true;

	public bool CustomMaxButtonVisibility = false;

	public bool CustomMinButtonVisibility = false;

	public bool CustomShowInTaskbar = false;

	public float CustomTabWidth = 420f;

	public float ENCustomTabWidth;

	public float ENCustomTabHeight;

	public float ZHCustomTabWidth;

	public float ZHCustomTabHeight;

	public float CustomTabCHWidth = 420f;

	public float CustomTabENWidth = 680f;

	public float CustomTabHeight = 500f;

	private float configWindowWidth;

	private float configWindowHeight;

	private bool configMaxButtonVisibility;

	public bool IsFullScreen = true;

	private IROISelecter selectedROI;

	private int curImageWidth;

	private int curImageHeight;

	private ICommand _ImageSizeChangedCommand = null;

	private ICommand _RoiSelectedCommand = null;

	private ICommand _RoiDelCommand = null;

	private ICommand _RoiCopyCommand = null;

	private ICommand _RoiPasteCommand = null;

	private ICommand _AdditionDataCommand = null;

	private ICommand _RoiVisibleChangedCommand = null;

	private Dictionary<string, object> InOutPutObjectData = new Dictionary<string, object>();

	private object _owner;

	private DelegateCommand _openHelpCommand;

	public ObservableCollection<object> Tabs;

	private int tabIndex = 0;

	private ObservableCollection<object> VisibleTabs;

	public ObservableCollection<object> BottomExcuteButtons;

	public ObservableCollection<object> BottomExcuteButtonsLeft;

	public ObservableCollection<object> ExternParamsList;

	private bool isSubscribeInWindow;

	private NameValueItem selectedSubscribeParam;

	private bool _IsSubscribeIndex;

	private DelegateCommand<object> changeSelectedSubParamCommand = null;

	private const int filterStrLen = 64;

	public float ConfigWindowWidth
	{
		get
		{
			configWindowWidth = CustomTabWidth;
			return configWindowWidth;
		}
		set
		{
			if (configWindowWidth != value)
			{
				configWindowWidth = value;
				OnPropertyChanged("ConfigWindowWidth");
			}
		}
	}

	public float ConfigWindowHeight
	{
		get
		{
			configWindowHeight = CustomTabHeight;
			return configWindowHeight;
		}
		set
		{
			if (configWindowHeight != value)
			{
				configWindowHeight = value;
				OnPropertyChanged("ConfigWindowHeight");
			}
		}
	}

	public bool ConfigMaxButtonVisibility
	{
		get
		{
			configMaxButtonVisibility = CustomMaxButtonVisibility;
			return configMaxButtonVisibility;
		}
		set
		{
			if (configMaxButtonVisibility != value)
			{
				configMaxButtonVisibility = value;
				OnPropertyChanged("ConfigMaxButtonVisibility");
			}
		}
	}

	public List<IROISelecter> ROISelecters { get; set; } = new List<IROISelecter>();

	public IROISelecter SelectedROI
	{
		get
		{
			return selectedROI;
		}
		set
		{
			if (selectedROI != value)
			{
				selectedROI = value;
				OnPropertyChanged("SelectedROI");
			}
		}
	}

	public int CurImageWidth
	{
		get
		{
			return curImageWidth;
		}
		set
		{
			if (curImageWidth == value)
			{
				return;
			}
			curImageWidth = value;
			OnPropertyChanged("CurImageWidth");
			foreach (IROISelecter rOISelecter in ROISelecters)
			{
				rOISelecter.CurImageWidth = value;
			}
		}
	}

	public int CurImageHeight
	{
		get
		{
			return curImageHeight;
		}
		set
		{
			if (curImageHeight == value)
			{
				return;
			}
			curImageHeight = value;
			OnPropertyChanged("CurImageHeight");
			foreach (IROISelecter rOISelecter in ROISelecters)
			{
				rOISelecter.CurImageHeight = value;
			}
		}
	}

	public ICommand ImageSizeChangedCommand => _ImageSizeChangedCommand ?? (_ImageSizeChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)ImageSizeChangedCommandExecute));

	public ICommand RoiSelectedCommand => _RoiSelectedCommand ?? (_RoiSelectedCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiSelectedCommandExecute));

	public ICommand RoiDelCommand => _RoiDelCommand ?? (_RoiDelCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiDelCommandExecute, (Func<RoiWithTagEventArgs, bool>)CanRoiDelCommandExecute, false));

	public ICommand RoiCopyCommand => _RoiCopyCommand ?? (_RoiCopyCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiCopyCommandExecute, (Func<RoiWithTagEventArgs, bool>)CanRoiCopyCommandExecute, false));

	public ICommand RoiPasteCommand => _RoiPasteCommand ?? (_RoiPasteCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiPasteCommandExecute));

	public ICommand AdditionDataCommand => _AdditionDataCommand ?? (_AdditionDataCommand = (ICommand)new DelegateCommand<object>((Action<object>)AdditionDataCommandExecute));

	public ICommand RoiVisibleChangedCommand => _RoiVisibleChangedCommand ?? (_RoiVisibleChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)RoiVisibleChangedCommandExecute));

	public IParamsConfig UserStringData { get; set; }

	public bool IsOnInit { get; set; }

	public object Owner
	{
		get
		{
			return _owner;
		}
		set
		{
			if (_owner != value)
			{
				_owner = value;
				if (Owner_Set != null)
				{
					Owner_Set(this, new EventArgs());
				}
				OnPropertyChanged("Owner");
				OnPropertyChanged("OwnerType");
			}
		}
	}

	public string OwnerTypeName => (Owner as IVmModule)?.ModuleTypeName;

	public DelegateCommand OpenHelpCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_openHelpCommand == null)
			{
				_openHelpCommand = new DelegateCommand((Action)ExecuteOpenHelp);
			}
			return _openHelpCommand;
		}
	}

	public ObservableCollection<object> TabsA
	{
		get
		{
			return Tabs;
		}
		set
		{
			if (Tabs != value)
			{
				Tabs = value;
				OnPropertyChanged("TabsA");
			}
		}
	}

	public int TabIndex
	{
		get
		{
			return tabIndex;
		}
		set
		{
			if (tabIndex != value)
			{
				tabIndex = value;
				OnPropertyChanged("TabIndex");
			}
		}
	}

	public ObservableCollection<object> VisibleTabsA
	{
		get
		{
			if (VisibleTabs == null)
			{
				VisibleTabs = new ObservableCollection<object>();
				if (Tabs == null)
				{
					return VisibleTabs;
				}
				foreach (Tab tab in Tabs)
				{
					if (tab.IsVisible)
					{
						VisibleTabs.Add(tab);
					}
				}
			}
			return VisibleTabs;
		}
		set
		{
			VisibleTabs = value;
			OnPropertyChanged("VisibleTabsA");
		}
	}

	public ObservableCollection<object> BottomExcuteButtonsA
	{
		get
		{
			return BottomExcuteButtons;
		}
		set
		{
			if (BottomExcuteButtons != value)
			{
				BottomExcuteButtons = value;
				OnPropertyChanged("BottomExcuteButtonsA");
			}
		}
	}

	public ObservableCollection<object> BottomExcuteButtonsLeftA
	{
		get
		{
			return BottomExcuteButtonsLeft;
		}
		set
		{
			if (BottomExcuteButtonsLeft != value)
			{
				BottomExcuteButtonsLeft = value;
				OnPropertyChanged("BottomExcuteButtonsLeftA");
			}
		}
	}

	public ObservableCollection<object> ExternParamsListA
	{
		get
		{
			return ExternParamsList;
		}
		set
		{
			if (ExternParamsList != value)
			{
				ExternParamsList = value;
				OnPropertyChanged("ExternParamsListA");
			}
		}
	}

	public object Solution => VmSolution.Instance;

	public bool HasSubscribeInWindow { get; set; }

	public virtual bool IsSubscribeInWindow
	{
		get
		{
			return isSubscribeInWindow;
		}
		set
		{
			if (isSubscribeInWindow != value)
			{
				isSubscribeInWindow = value;
				if (!isSubscribeInWindow)
				{
					HasSubscribeInWindow = false;
					(selectedSubscribeParam as ButtonSelecter)?.OnPropertyChanged("IsSelectedSubscribeParam");
				}
				OnPropertyChanged("IsSubscribeInWindow");
			}
		}
	}

	public virtual NameValueItem SelectedSubscribeParam
	{
		get
		{
			return selectedSubscribeParam;
		}
		set
		{
			if (selectedSubscribeParam != value)
			{
				NameValueItem nameValueItem = selectedSubscribeParam;
				selectedSubscribeParam = value;
				OnPropertyChanged("SelectedSubscribeParam");
				if (IsSubscribeInWindow)
				{
					(nameValueItem as ButtonSelecter)?.OnPropertyChanged("IsSelectedSubscribeParam");
					(selectedSubscribeParam as ButtonSelecter)?.OnPropertyChanged("IsSelectedSubscribeParam");
				}
			}
		}
	}

	public virtual bool IsSubscribeIndex
	{
		get
		{
			return _IsSubscribeIndex;
		}
		set
		{
			_IsSubscribeIndex = value;
			OnPropertyChanged("IsSubscribeIndex");
		}
	}

	public ICommand ChangeSelectedSubParamCommand
	{
		get
		{
			if (changeSelectedSubParamCommand == null)
			{
				changeSelectedSubParamCommand = new DelegateCommand<object>((Action<object>)ChangeSelectedSubParamCommandExecute);
			}
			return (ICommand)changeSelectedSubParamCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler Owner_Set;

	public event EventHandler XmlLoaded;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public void UpdateROI()
	{
		foreach (IROISelecter rOISelecter in ROISelecters)
		{
			rOISelecter.UpdateROI();
		}
	}

	private void ImageSizeChangedCommandExecute(object data)
	{
		Size size = (Size)data;
		CurImageWidth = (int)size.Width;
		CurImageHeight = (int)size.Height;
	}

	private void RoiSelectedCommandExecute(RoiWithTagEventArgs args)
	{
		object ls = args.Tag;
		IROI roi = args.Roi;
		IROISelecter iROISelecter;
		if ((iROISelecter = ROISelecters.SingleOrDefault((IROISelecter p) => p.ROIList == ls)) != null)
		{
			iROISelecter.DoRoiSelect(roi);
		}
	}

	private bool CanRoiDelCommandExecute(RoiWithTagEventArgs args)
	{
		object ls = args.Tag;
		IROI roi = args.Roi;
		IROISelecter iROISelecter;
		if ((iROISelecter = ROISelecters.SingleOrDefault((IROISelecter p) => p.ROIList == ls)) != null)
		{
			return iROISelecter.CanDeleted(roi);
		}
		return false;
	}

	private void RoiDelCommandExecute(RoiWithTagEventArgs args)
	{
		object ls = args.Tag;
		IROI roi = args.Roi;
		IROISelecter iROISelecter;
		if ((iROISelecter = ROISelecters.SingleOrDefault((IROISelecter p) => p.ROIList == ls)) != null)
		{
			iROISelecter.DoRoiDeleted(roi);
		}
	}

	private bool CanRoiCopyCommandExecute(RoiWithTagEventArgs args)
	{
		object ls = args.Tag;
		IROI roi = args.Roi;
		IROISelecter iROISelecter;
		if ((iROISelecter = ROISelecters.SingleOrDefault((IROISelecter p) => p.ROIList == ls)) != null)
		{
			return iROISelecter.CanCopy(roi);
		}
		return false;
	}

	private void RoiCopyCommandExecute(RoiWithTagEventArgs args)
	{
	}

	private void RoiPasteCommandExecute(RoiWithTagEventArgs args)
	{
		object ls = args.Tag;
		IROI roi = args.Roi;
		IROISelecter iROISelecter;
		if ((iROISelecter = ROISelecters.SingleOrDefault((IROISelecter p) => p.ROIList == ls)) != null)
		{
			iROISelecter.DoRoiPaste(roi);
		}
	}

	private void AdditionDataCommandExecute(object args)
	{
		IImageViewAddition imageViewAddition = args as IImageViewAddition;
		foreach (IROISelecter rOISelecter in ROISelecters)
		{
			if (rOISelecter is IROISelecterAddition iROISelecterAddition)
			{
				iROISelecterAddition.CloundAddition = imageViewAddition.InnerImageViewAddition;
			}
			else if (rOISelecter is IROISelecterAdditionW iROISelecterAdditionW)
			{
				iROISelecterAdditionW.WplotAddition = imageViewAddition.InnerImageViewWAddition;
			}
		}
	}

	private void RoiVisibleChangedCommandExecute(object args)
	{
		if (!(bool)args)
		{
			foreach (IROISelecter rOISelecter in ROISelecters)
			{
				rOISelecter.DoRoiSelect(null);
			}
			return;
		}
		foreach (IROISelecter rOISelecter2 in ROISelecters)
		{
			rOISelecter2.DoRoiSelect(ROISelecter.OldROI);
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		string text = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser.ParamTab";
		base.Init(node);
		BaseNode.defaultNameSpace = text;
		SetParentNode(null);
		if (NeedNotifyLoaded)
		{
			if (XmlLoaded != null)
			{
				XmlLoaded(this, new EventArgs());
			}
			OnXmlLoaded();
		}
		return this;
	}

	internal void Load(byte[] fileData)
	{
		loadXml(fileData);
	}

	public void OnXmlLoaded()
	{
		IsOnInit = true;
		try
		{
			if (Tabs != null)
			{
				foreach (Tab tab in Tabs)
				{
					tab.OnXmlLoaded();
				}
			}
			if (BottomExcuteButtons != null)
			{
				foreach (object bottomExcuteButton in BottomExcuteButtons)
				{
					(bottomExcuteButton as BottomCommand)?.XmlLoadedTask();
				}
			}
			if (BottomExcuteButtonsLeft != null)
			{
				foreach (object item in BottomExcuteButtonsLeft)
				{
					(item as BottomCommand)?.XmlLoadedTask();
				}
			}
			if (ExternParamsList == null)
			{
				return;
			}
			foreach (object externParams in ExternParamsList)
			{
				(externParams as NameValueItem)?.XmlLoadedTask();
			}
		}
		finally
		{
			IsOnInit = false;
		}
	}

	public AlgorithmTabRoot()
	{
	}

	public AlgorithmTabRoot(IParamsConfig userStringData)
		: this(userStringData?.XmlPath, (userStringData as IParent)?.Parent, userStringData)
	{
	}

	public AlgorithmTabRoot(string path, object owner, IParamsConfig userStringData)
	{
		UserStringData = userStringData;
		Owner = owner;
		if (Owner is ISubjectModel subjectModel)
		{
			subjectModel.InitLoad();
		}
		try
		{
			if (!string.IsNullOrEmpty(path))
			{
				loadXml(path, ignoreUnknown: true);
				DynamicSetProperty();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public object GetInOutputByName(string paramName, object obj = null)
	{
		if (!InOutPutObjectData.ContainsKey(paramName))
		{
			if (obj == null)
			{
				return null;
			}
			InOutPutObjectData.Add(paramName, obj);
		}
		return InOutPutObjectData[paramName];
	}

	private void ExecuteOpenHelp()
	{
		string toolItemInfoPath = "";
		if (Owner is IVmModule { Params: not null } vmModule)
		{
			toolItemInfoPath = Path.GetDirectoryName(vmModule.Params.XmlPath) + "\\ToolItemInfo.xml";
		}
		HelpDocumentUtil.OpenHelpDocument(toolItemInfoPath);
	}

	public void HandleTabSelectionChange(int selectedIndex)
	{
		if (selectedIndex < 0 || selectedIndex >= Tabs.Count)
		{
			return;
		}
		Tab tab = Tabs[selectedIndex] as Tab;
		if (tab != null && !tab.IsVisible)
		{
			if (TabIndex >= 0 && TabIndex < TabsA.Count)
			{
				object obj = TabsA[TabIndex];
				if (obj != null && obj is Tab { IsVisible: false })
				{
					SelectNextVisibleTab(selectedIndex);
				}
			}
		}
		else if (tab != null && tab.IsVisible)
		{
			TabIndex = selectedIndex;
		}
	}

	private void SelectNextVisibleTab(int currentIndex)
	{
		int num = -1;
		int count = Tabs.Count;
		for (int i = currentIndex + 1; i < count; i++)
		{
			if (Tabs[i] is Tab { IsVisible: not false })
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			for (int j = 0; j < currentIndex; j++)
			{
				if (Tabs[j] is Tab { IsVisible: not false })
				{
					num = j;
					break;
				}
			}
		}
		if (num >= 0)
		{
			TabIndex = num;
		}
		else
		{
			TabIndex = 0;
		}
	}

	private void ChangeSelectedSubParamCommandExecute(object param)
	{
		IsSubscribeIndex = false;
		if (IsSubscribeInWindow)
		{
			SelectedSubscribeParam = param as NameValueItem;
		}
	}

	public bool InitItemsValueUnit()
	{
		if (Tabs != null)
		{
			foreach (Tab tab in Tabs)
			{
				if (!tab.InitItemsValueUnit())
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool GetItemsAllValueUnit()
	{
		if (!(Owner is IVmModuleSet))
		{
			(Owner as IParamsOperator)?.GetParamsForConfig();
		}
		return GetItemsValueUnit();
	}

	public bool GetItemsValueUnit(bool isGetFromModuleInRemoteMode = false)
	{
		if (UserStringData == null)
		{
			throw new ArgumentNullException("UserStringData");
		}
		if (Tabs != null)
		{
			foreach (Tab tab in Tabs)
			{
				if (tab.GetItemsValueUnit(isGetFromModuleInRemoteMode))
				{
				}
			}
		}
		return true;
	}

	public bool GetItemsValueUnit()
	{
		if (UserStringData == null)
		{
			throw new ArgumentNullException("UserStringData");
		}
		if (Tabs != null)
		{
			foreach (Tab tab in Tabs)
			{
				if (tab.GetItemsValueUnit())
				{
				}
			}
		}
		return true;
	}

	public void DoIniter()
	{
		if (Tabs != null)
		{
			foreach (Tab tab in Tabs)
			{
				tab.DoIniter();
			}
		}
		if (BottomExcuteButtons != null)
		{
			foreach (object bottomExcuteButton in BottomExcuteButtons)
			{
				(bottomExcuteButton as NameValueItem)?.InniterBusiness();
			}
		}
		if (BottomExcuteButtonsLeft != null)
		{
			foreach (object item in BottomExcuteButtonsLeft)
			{
				(item as NameValueItem)?.InniterBusiness();
			}
		}
		if (ExternParamsList != null)
		{
			foreach (object externParams in ExternParamsList)
			{
				(externParams as NameValueItem)?.InniterBusiness();
			}
		}
		SelectedSubscribeParam = null;
	}

	public void DoFinish()
	{
		if (Tabs == null)
		{
			return;
		}
		foreach (Tab tab in Tabs)
		{
			tab.DoFinish();
		}
	}

	public BaseNode FindBaseNodeByName(string Name)
	{
		if (Tabs != null)
		{
			foreach (Tab tab in Tabs)
			{
				BaseNode baseNode = tab.FindBaseNodeByName(Name);
				if (baseNode != null)
				{
					return baseNode;
				}
			}
		}
		if (BottomExcuteButtons != null)
		{
			foreach (object bottomExcuteButton in BottomExcuteButtons)
			{
				if (bottomExcuteButton is NameValueItem nameValueItem && nameValueItem.Name == Name)
				{
					return nameValueItem;
				}
			}
		}
		if (ExternParamsList != null)
		{
			foreach (object externParams in ExternParamsList)
			{
				if (externParams is NameValueItem nameValueItem2 && nameValueItem2.Name == Name)
				{
					return nameValueItem2;
				}
			}
		}
		return null;
	}

	object IGetNode.GetNode(string name)
	{
		return FindBaseNodeByName(name);
	}

	internal Category FindCategoryByName(string Name)
	{
		if (Tabs != null)
		{
			foreach (Tab tab in Tabs)
			{
				Category category = tab.FindCategoryByName(Name);
				if (category != null)
				{
					return category;
				}
			}
		}
		return null;
	}

	public IEnumerable<NameValueItem> GetAllItems()
	{
		IEnumerable<NameValueItem> enumerable = Tabs.OfType<Tab>().SelectMany((Tab p) =>
		{
			ObservableCollection<object> categorys = p.Categorys;
			return (categorys != null) ? categorys.OfType<Category>().SelectMany((Category pt) => pt.Items) : null;
		}).OfType<NameValueItem>();
		if (enumerable == null)
		{
			enumerable = new List<NameValueItem>();
		}
		return enumerable;
	}

	public void SetParamByName(string Name, string Value)
	{
		if (FindBaseNodeByName(Name) is NameValueItem nameValueItem)
		{
			nameValueItem.Value = Value;
			nameValueItem.SetParamUnit();
		}
	}

	public string GetParamByName(string Name)
	{
		if (!(FindBaseNodeByName(Name) is NameValueItem { Value: var value }))
		{
			return null;
		}
		return value;
	}

	public IEnumerable<NameValueItem> GetModuleSelector()
	{
		try
		{
			ObservableCollection<object> tabs = Tabs;
			object result;
			if (tabs == null)
			{
				result = null;
			}
			else
			{
				IEnumerable<object> enumerable = tabs.SelectMany((object p) => (p as Tab)?.Collection);
				result = ((enumerable == null) ? null : enumerable.OfType<NameValueItem>()?.Where((NameValueItem p) => p is Enumeration_ModuleSelector));
			}
			return (IEnumerable<NameValueItem>)result;
		}
		catch
		{
			return null;
		}
	}

	public IEnumerable<NameValueItem> GetItem(Func<NameValueItem, bool> predicate)
	{
		try
		{
			ObservableCollection<object> tabs = Tabs;
			object result;
			if (tabs == null)
			{
				result = null;
			}
			else
			{
				IEnumerable<object> enumerable = tabs.SelectMany((object p) => (p as Tab)?.Collection);
				result = ((enumerable == null) ? null : enumerable.OfType<NameValueItem>()?.Where(predicate));
			}
			return (IEnumerable<NameValueItem>)result;
		}
		catch
		{
			return null;
		}
	}

	public IList<Holder> GetRunParamsTree()
	{
		ObservableCollection<object> observableCollection = (Tabs.SingleOrDefault((object p) => (p as Tab)?.Name == "Tab_Run Params") as Tab)?.Collection;
		List<Holder> list = new List<Holder>();
		if (observableCollection != null)
		{
			foreach (object item in observableCollection)
			{
				if (item is IDynamicChild dynamicChild)
				{
					foreach (object item2 in dynamicChild.GetDynamicChild())
					{
						AddSubChildToTree(item2, list);
					}
				}
				else
				{
					AddSubChildToTree(item, list);
				}
			}
		}
		return list;
	}

	private void AddSubChildToTree(object runParam, List<Holder> runParamsTree)
	{
		if (!string.IsNullOrEmpty((runParam as NameValueItem).DisplayNameA))
		{
			if (runParam is IntegerBettween integerBettween)
			{
				AddBetweenRunParamsTree(ref runParamsTree, integerBettween.IntegersA);
			}
			else if (runParam is FloatBettween floatBettween)
			{
				AddBetweenRunParamsTree(ref runParamsTree, floatBettween.FloatsA);
			}
			else
			{
				AddRunParamsTree(ref runParamsTree, runParam);
			}
		}
	}

	private void AddBetweenRunParamsTree(ref List<Holder> runParamsTree, List<object> betweenParams)
	{
		foreach (object betweenParam in betweenParams)
		{
			AddRunParamsTree(ref runParamsTree, betweenParam);
		}
	}

	private void DynamicSetProperty()
	{
	}

	private static void AddRunParamsTree(ref List<Holder> runParamsTree, object param)
	{
		runParamsTree.Add(new Holder
		{
			DisplayNameA = (param as NameValueItem)?.MultiLanguageDisplayNameA,
			HolderTarget = param
		});
	}

	void IUpdateItemRoot.UpdateRootItem(string key)
	{
		ParamRoot paramRoot = VmSolution.Instance[(Owner as IVmFullPath).FullPath + ".[Display]"] as ParamRoot;
		Tabs.SelectMany((object p) => (p as Tab).Collection).Where((object q) =>
		{
			BridgeItemBase bridgeItemBase;
			return (bridgeItemBase = q as BridgeItemBase) != null;
		}).ToList()
			.ForEach((object x) =>
			{
				BridgeItemBase bridgeItemBase = x as BridgeItemBase;
				Object bObject;
				if ((bObject = bridgeItemBase.BridgeObjA as Object) != null)
				{
					bridgeItemBase.BridgeObjA = paramRoot.GetDisplayList().OfType<Object>().FirstOrDefault((Object p) => p.Name == bObject.Name);
				}
				else if (bridgeItemBase.BridgeObjA is Feature)
				{
					string operationParams = bridgeItemBase.BrigdgeSetter.OperationParams;
					if (operationParams != null && operationParams.Split(';').Length > 1)
					{
						string objectName = bridgeItemBase.BrigdgeSetter.OperationParams.Split(';')[0];
						string name = bridgeItemBase.BrigdgeSetter.OperationParams.Split(';')[1];
						bridgeItemBase.BridgeObjA = paramRoot.GetDisplayList().OfType<Object>().FirstOrDefault((Object p) => p.Name == objectName)?.GetFeatureByName(name);
					}
				}
			});
	}

	void IUpdateItemRoot.UpdatDynamicIO(string name)
	{
		IEnumerable<object> enumerable = Tabs.SelectMany((object p) => (p as Tab).Collection);
		foreach (object item in enumerable)
		{
			if (item is IUpdateGroupItems updateGroupItems && (updateGroupItems.CurrentParamName == name || item is ISubChild))
			{
				updateGroupItems.SetItemsNull();
			}
			if (item is BridgeItemBase { BridgeObjA: IUpdateGroupItems bridgeObjA })
			{
				bridgeObjA.SetItemsNull();
			}
		}
	}

	public void ResetRunParamBind()
	{
		(from q in Tabs.SelectMany((object p) => (p as Tab).Collection)
			where q is ISubInfoCollection || q is ISubInfo
			select q).ToList().ForEach((object x) =>
		{
			if (x is ISubInfo { IsSubA: not false } subInfo)
			{
				subInfo.IsSubA = false;
			}
			else if (x is ISubInfoCollection subInfoCollection)
			{
				foreach (object item in subInfoCollection.SubChildren.ToList())
				{
					if (item is ISubInfo { IsSubA: not false } subInfo2)
					{
						subInfo2.IsSubA = false;
					}
				}
			}
		});
	}

	void IUpdateItemRoot.DeleteRunParamBind(object module, object curModule, Expression<Action<object>> expression)
	{
		VmModule delModu = module as VmModule;
		if (delModu == null)
		{
			return;
		}
		int registeCount = 0;
		(from q in Tabs.SelectMany((object p) => (p as Tab).Collection)
			where q is ISubInfoCollection || q is ISubInfo
			select q).ToList().ForEach((object x) =>
		{
			if (x is ISubInfo { IsSubA: not false } subInfo)
			{
				DeleteRunParamBind(subInfo, delModu, curModule, expression, ref registeCount);
			}
			else if (x is ISubInfoCollection subInfoCollection)
			{
				foreach (object item in subInfoCollection.SubChildren.ToList())
				{
					if (item is ISubInfo { IsSubA: not false } subInfo2)
					{
						DeleteRunParamBind(subInfo2, delModu, curModule, expression, ref registeCount);
					}
				}
			}
		});
	}

	private void DeleteRunParamBind(ISubInfo info, VmModule module, object curModule, Expression<Action<object>> expression, ref int registCount)
	{
		string curValueA = info.SubData.CurValueA;
		if (string.IsNullOrEmpty(curValueA))
		{
			return;
		}
		string comType = info.SubData.DropOpenSetterA.OperationParamsA;
		IEnumerable<VmIO> enumerable = from q in module.Outputs.GetAllLeafIO()
			where q.TypeName.Equals(comType)
			select q;
		foreach (VmIO item in enumerable)
		{
			if (curValueA.Contains(item.ModulePathName))
			{
				if (registCount == 0)
				{
					expression.Compile()(curModule);
					registCount++;
				}
				info.SubData.CurValueA = null;
			}
		}
	}

	void IUpdateItemRoot.UpdateSingleParamBind(object oldModule, object newModule)
	{
		if ((newModule as VmModule).Params.Data == null)
		{
			CopyParamsNoOpen(oldModule as VmModule, newModule as VmModule);
		}
		else
		{
			CopyParamsWithOpen(oldModule as VmModule, newModule as VmModule);
		}
	}

	private void CopyParamsNoOpen(VmModule oldModule, VmModule newModule)
	{
		Dictionary<string, ISubInfo> oldSubInfos = new Dictionary<string, ISubInfo>();
		DirectForValue(this, oldSubInfos);
		oldSubInfos.Keys.ToList().ForEach((string x) =>
		{
			SignalBindOperation(oldSubInfos[x], oldModule, newModule);
		});
	}

	private void SignalBindOperation(ISubInfo info, VmModule oldModule, VmModule newModule)
	{
		ButtonSelecter subData = info.SubData;
		string operationParamsA = subData.DropOpenSetterA.OperationParamsA;
		string text = (info.IsSubA ? info.SubData.CurValueA : "");
		string comName = (info as NameValueItem).ComName;
		VmModule vmModule;
		if (comName == null || (vmModule = newModule) == null || comName.Length <= 0)
		{
			return;
		}
		string text2 = comName;
		bool flag = true;
		if (string.IsNullOrEmpty(text))
		{
			vmModule.ParamSubManager.DelModuleParamBinding(text2, operationParamsA);
		}
		else
		{
			if (text.Length > 64)
			{
				string[] strArrMsg = new string[3]
				{
					AppLocalizationService.GetLang("Lang_ContentTooLarge"),
					AppLocalizationService.GetLang("长度上限"),
					64.ToString()
				};
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), strArrMsg, MessageIcons.Error);
				return;
			}
			if ((MathHelper.IsNumber(text) || MathHelper.IsConst(text)) && text != "TRIGGER_STRING" && text != "TRIGGER_STRING[]")
			{
				flag = vmModule.ParamSubManager.SetModuleParamBinding(text2, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_CONSTANT, text);
			}
			else if (text.Equals(VmSolution.ExternIO.Name + "[]") || text == VmSolution.ExternIO.Name)
			{
				flag = vmModule.ParamSubManager.SetModuleParamBinding(text2, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_GLOBAL, "", 0u, VmSolution.ExternIO.Name);
			}
			else
			{
				string text3 = "";
				int num = text.IndexOf('[', 0);
				if (num > 0)
				{
					text3 = text.Substring(num);
					text3 = text3.Replace("[", "").Replace("]", "");
					text = text.Remove(text.IndexOf('[', 0));
				}
				VmIO vmIO = VmSolution.Instance.GetIoByPath(text);
				if (vmIO == null)
				{
					vmIO = VmSolution.Instance[text] as VmIO;
				}
				if (vmIO == null)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
					return;
				}
				if (!text3.Contains('.'))
				{
					flag = ((!MathHelper.IsNumber(text3)) ? vmModule.ParamSubManager.SetModuleParamBinding(text2, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", vmIO.Root.ID, vmIO.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL, "All") : vmModule.ParamSubManager.SetModuleParamBinding(text2, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", vmIO.Root.ID, vmIO.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_CONSTANT, text3));
				}
				else
				{
					VmIO ioByPathFromAround = vmModule.GetIoByPathFromAround(text3);
					if (ioByPathFromAround == null)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
						return;
					}
					flag = vmModule.ParamSubManager.SetModuleParamBinding(text2, operationParamsA, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", vmIO.Root.ID, vmIO.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_VARIABLE, "", ioByPathFromAround.Root.ID, ioByPathFromAround.Name);
				}
			}
		}
		if (!flag)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
		}
		vmModule.UserData["@" + text2] = StringUtil.UTF8GetBytesPadZero(info.IsSubA.ToString());
	}

	private void CopyParamsWithOpen(VmModule oldModule, VmModule newModule)
	{
		Dictionary<string, ISubInfo> dictionary = new Dictionary<string, ISubInfo>();
		Dictionary<string, ISubInfo> dictionary2 = new Dictionary<string, ISubInfo>();
		DirectForValue(this, dictionary);
		DirectForValue(newModule.Params.Data as AlgorithmTabRoot, dictionary2);
		foreach (string key in dictionary.Keys)
		{
			if (!dictionary[key].IsSubA)
			{
				if (dictionary2[key].IsSubA)
				{
					dictionary2[key].IsSubA = false;
				}
			}
			else if (!dictionary2[key].IsSubA)
			{
				SignalBindOperation(dictionary[key], oldModule, newModule);
				dictionary2[key].SetInit();
			}
			else if (dictionary2[key].SubData.CurValueA != dictionary[key].SubData.CurValueA)
			{
				SignalBindOperation(dictionary[key], oldModule, newModule);
				dictionary2[key].SetInit();
			}
		}
	}

	private void DirectForValue(AlgorithmTabRoot root, Dictionary<string, ISubInfo> dict)
	{
		(from q in root.Tabs.Where((object t) => (t as Tab).TabType != 1).SelectMany((object p) => (p as Tab).Collection)
			where q is ISubChild || q is ISubInfo
			select q).ToList().ForEach((object x) =>
		{
			if (x is ISubInfo subInfo)
			{
				dict.Add((subInfo as NameValueItem).ComName, subInfo);
			}
			else if (x is ISubInfoCollection { SubChildren: var subChildren })
			{
				subChildren.Where((object p) => p is ISubInfo).ToList().ForEach((object y) =>
				{
					ISubInfo subInfo2 = y as ISubInfo;
					dict.Add((subInfo2 as NameValueItem).ComName, subInfo2);
				});
			}
		});
	}

	void IUpdateItemRoot.UpdateRunParamStatus(IList<string> comNames)
	{
		if (comNames == null || comNames.Count == 0)
		{
			return;
		}
		Dictionary<string, ISubInfo> dict = new Dictionary<string, ISubInfo>();
		(from q in Tabs.Where((object t) => (t as Tab).TabType != 1).SelectMany((object p) => (p as Tab).Collection)
			where q is ISubChild || q is ISubInfo
			select q).ToList().ForEach((object x) =>
		{
			if (x is ISubInfo { IsSubA: not false } subInfo)
			{
				dict.Add((subInfo as NameValueItem).ComName, subInfo);
			}
			else if (x is ISubInfoCollection subInfoCollection)
			{
				foreach (object item in subInfoCollection.SubChildren.ToList())
				{
					if (item is ISubInfo { IsSubA: not false } subInfo2)
					{
						dict.Add((subInfo2 as NameValueItem).ComName, subInfo2);
					}
				}
			}
		});
		comNames.ToList().ForEach((string x) =>
		{
			if (dict.ContainsKey(x))
			{
				dict[x].SetInit();
			}
		});
	}

	void IUpdateItemRoot.UpdateRunParamSubscribe(string paramName, string newSubscribe)
	{
		List<object> list = (from q in Tabs.SelectMany((object p) => (p as Tab).Collection)
			where q is ISubInfoCollection || q is ISubInfo
			select q).ToList();
		foreach (object item in list)
		{
			if (item is ISubInfo { IsSubA: not false } subInfo && paramName == (subInfo as NameValueItem).ComName)
			{
				subInfo.SubData.CurValueA = newSubscribe;
				break;
			}
			if (!(item is ISubInfoCollection { SubChildren: not null } subInfoCollection))
			{
				continue;
			}
			bool flag = false;
			foreach (object item2 in subInfoCollection.SubChildren.ToList())
			{
				if (item2 is ISubInfo { IsSubA: not false } subInfo2)
				{
					subInfo2.SubData.CurValueA = newSubscribe;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
	}

	void IUpdateItemRoot.UpdateRunParamBind(string moduleName, Dictionary<object, object> moduleMap)
	{
		(from q in Tabs.SelectMany((object p) => (p as Tab).Collection)
			where q is ISubInfoCollection || q is ISubInfo
			select q).ToList().ForEach((object x) =>
		{
			if (x is ISubInfo { IsSubA: not false } subInfo)
			{
				BridgeBinding(subInfo, moduleName, moduleMap);
			}
			else if (x is ISubInfoCollection subInfoCollection)
			{
				foreach (object item in subInfoCollection.SubChildren.ToList())
				{
					if (item is ISubInfo { IsSubA: not false } subInfo2)
					{
						BridgeBinding(subInfo2, moduleName, moduleMap);
					}
				}
			}
		});
	}

	private void BridgeBinding(ISubInfo subInfo, string moduleName, Dictionary<object, object> moduleMap)
	{
		ButtonSelecter subData = subInfo.SubData;
		string operationParamsA = subData.DropOpenSetterA.OperationParamsA;
		string curValueA = subInfo.SubData.CurValueA;
		string comName = (subInfo as NameValueItem).ComName;
		if (comName != null && !string.IsNullOrEmpty(curValueA))
		{
			SetBindingOperate(operationParamsA, curValueA, comName, moduleName, moduleMap);
		}
	}

	private void SetBindingOperate(string typeName, string value, string ExtraParams, string moduleName, Dictionary<object, object> moduleMap)
	{
		VmModule vmModule = VmSolution.Instance[moduleName] as VmModule;
		string text = value;
		VmModule node;
		if ((node = vmModule) == null || ExtraParams.Length <= 0)
		{
			return;
		}
		bool flag = true;
		if (string.IsNullOrEmpty(text))
		{
			flag = node.ParamSubManager.DelModuleParamBinding(ExtraParams, typeName);
		}
		else
		{
			if (text.Length > 64)
			{
				string[] strArrMsg = new string[3]
				{
					AppLocalizationService.GetLang("Lang_ContentTooLarge"),
					AppLocalizationService.GetLang("长度上限"),
					64.ToString()
				};
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), strArrMsg, MessageIcons.Error);
				return;
			}
			if ((MathHelper.IsNumber(text) || MathHelper.IsConst(text)) && text != "TRIGGER_STRING" && text != "TRIGGER_STRING[]")
			{
				flag = node.ParamSubManager.SetModuleParamBinding(ExtraParams, typeName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_CONSTANT, text);
			}
			else if (text.Equals(VmSolution.ExternIO.Name + "[]") || text == VmSolution.ExternIO.Name)
			{
				flag = node.ParamSubManager.SetModuleParamBinding(ExtraParams, typeName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_GLOBAL, "", 0u, VmSolution.ExternIO.Name);
			}
			else
			{
				string text2 = "";
				int num = text.IndexOf('[', 0);
				if (num > 0)
				{
					text2 = text.Substring(num);
					text2 = text2.Replace("[", "").Replace("]", "");
					text = text.Remove(text.IndexOf('[', 0));
				}
				VmModule vmModule2 = node;
				object key = moduleMap.FirstOrDefault((KeyValuePair<object, object> i) => i.Value == node).Key;
				if (key is VmModule vmModule3 && vmModule3.Parent != node.Parent)
				{
					vmModule2 = vmModule3;
				}
				VmIO io = vmModule2.GetIoByPathFromAround(text);
				if (io == null)
				{
					io = VmSolution.Instance[text] as VmIO;
				}
				if (io == null)
				{
					return;
				}
				object obj = moduleMap.Keys.Where((object p) => (p as VmModule).ID == io.Root.ID && (p as VmModule).FullName == io.Root.FullName)?.FirstOrDefault();
				if (obj != null)
				{
					VmModule vmModule4 = moduleMap[obj] as VmModule;
					text = text.Replace($"{io.Root.ID} ", $"{vmModule4.ID} ");
					text = text.Replace(io.Root.Name ?? "", vmModule4.Name ?? "");
					io = node.GetIoByPathFromAround(text);
					if (io == null)
					{
						io = VmSolution.Instance[text] as VmIO;
					}
					if (io == null)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
						return;
					}
				}
				else if (vmModule2 != node)
				{
					return;
				}
				if (text2.Contains('.'))
				{
					VmIO ioByPathFromAround = node.GetIoByPathFromAround(text2);
					if (ioByPathFromAround == null)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Err"), AppLocalizationService.GetLang("Lang_Subscribe_Nodata"), MessageIcons.Error, isTranslate: true);
						return;
					}
					flag = node.ParamSubManager.SetModuleParamBinding(ExtraParams, typeName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", io.Root.ID, io.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_VARIABLE, "", ioByPathFromAround.Root.ID, ioByPathFromAround.Name);
				}
				else if (MathHelper.IsNumber(text2))
				{
					flag = node.ParamSubManager.SetModuleParamBinding(ExtraParams, typeName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", io.Root.ID, io.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_CONSTANT, text2);
				}
				else
				{
					flag = node.ParamSubManager.SetModuleParamBinding(ExtraParams, typeName, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE, "", io.Root.ID, io.Name, ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL, "All");
					node.SetLinkWithVar(ExtraParams, typeName, io);
				}
			}
		}
		if (!flag)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
		}
	}

	IList<ISubParam> IUpdateItemRoot.GetAllRunParamBind(string moduleName)
	{
		IList<ISubParam> returnValue = new List<ISubParam>();
		(from q in Tabs.SelectMany((object p) => (p as Tab).Collection)
			where q is ISubInfoCollection || q is ISubInfo
			select q).ToList().ForEach((object x) =>
		{
			if (x is ISubInfo { IsSubA: not false } subInfo)
			{
				BuildSingleParamData(subInfo, returnValue);
			}
			else if (x is ISubInfoCollection subInfoCollection)
			{
				foreach (object item in subInfoCollection.SubChildren.ToList())
				{
					if (item is ISubInfo { IsSubA: not false } subInfo2)
					{
						BuildSingleParamData(subInfo2, returnValue);
					}
				}
			}
		});
		return returnValue;
	}

	private void BuildSingleParamData(ISubInfo info, IList<ISubParam> targetList)
	{
		ISubParam subParam = new SubscribeParam();
		subParam.Type = info.SubData.DropOpenSetterA.OperationParamsA;
		subParam.CurValue = info.SubData.CurValueA;
		subParam.ParamName = (info as NameValueItem).ComName;
		targetList.Add(subParam);
	}

	public void Dispose()
	{
		IsSubscribeInWindow = false;
	}
}
