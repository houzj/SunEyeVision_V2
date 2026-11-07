using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Input;
using System.Xml;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Hik.Util;
using Hik.Xml;
using HikPcUI.Commom;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class NameValueItem : BaseNode, INotifyPropertyChanged, IParamItem, ISaveNode, IName, IRunService, ICanTriggerExecute
{
	public delegate void CurValueChangedHandler(object sender, EventArgs e);

	private const int FilterLen = 64;

	protected static bool _IsResetAlgoParamsing = false;

	public string AlgorithmIndex;

	private string TriggerCondition = "";

	public bool GroupRunParamEnable = true;

	protected bool isInniter = false;

	private IParamsConfig _moduleTag;

	private VmModule _vmModule = null;

	[XmlMark(true)]
	public string Name;

	[XmlMark(true)]
	public string NameSpace;

	public bool Enable = true;

	public string Description;

	public string DisplayName;

	public string Visibility;

	public string AccessMode;

	public ObservableCollection<object> Triggers;

	public ObservableCollection<object> Initers;

	public ObservableCollection<object> ExternIniters;

	public ObservableCollection<object> DeIniters;

	public bool CustomVisible = true;

	public bool SupportCommTrigger = false;

	private ItemState _MyItemState = ItemState.Normal;

	public string StayName;

	public bool SubscribeVisible = true;

	private bool? _canTriggerExecute = null;

	public bool IsDeleteSubscribe = true;

	public const uint IMVS_E_MODU_CALLORDER = 2684354562u;

	public const uint IMVS_E_MODU_OVERTIME = 3758096648u;

	protected IUserStringData userStringData;

	public static ILogManager logManager;

	private bool _isGetFromModule = false;

	public const string ParamPre = "Param.";

	private object _operation;

	private PropertyInfo NamePro;

	protected bool IsGetShowError = true;

	public static EventHandler SetParamUnitExtern;

	public static EventHandler GetParamUnitExtern;

	private static int FilterPreRun = 0;

	private static int FilterPreRunSub = 0;

	private DelegateCommand paramItemSelectedCommand = null;

	public string AlgorithmIndexA
	{
		get
		{
			return AlgorithmIndex;
		}
		set
		{
			AlgorithmIndex = value;
			OnPropertyChanged("AlgorithmIndex");
		}
	}

	public IParamsConfig ParamsSource
	{
		get
		{
			if (_moduleTag == null && GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				_moduleTag = algorithmTabRoot.Owner as IParamsConfig;
				if (_moduleTag == null)
				{
					_moduleTag = algorithmTabRoot.UserStringData;
				}
			}
			return _moduleTag;
		}
	}

	public virtual string NameA
	{
		get
		{
			return Name;
		}
		set
		{
			if (Name != value)
			{
				Name = value;
				OnPropertyChanged("NameA");
			}
		}
	}

	public string NameSpaceA
	{
		get
		{
			return NameSpace;
		}
		set
		{
			NameSpace = value;
			OnPropertyChanged("NameSpaceA");
		}
	}

	public virtual bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public bool SupportCommTriggerA
	{
		get
		{
			return SupportCommTrigger;
		}
		set
		{
			if (SupportCommTrigger != value)
			{
				SupportCommTrigger = value;
				OnPropertyChanged("SupportCommTriggerA");
			}
		}
	}

	public virtual bool NeedInit => true;

	public bool EnableA
	{
		get
		{
			return Enable;
		}
		set
		{
			if (Enable != value)
			{
				Enable = value;
				OnPropertyChanged("EnableA");
			}
		}
	}

	public virtual string DescriptionA
	{
		get
		{
			return Description;
		}
		set
		{
			if (Description != value)
			{
				Description = value;
				OnPropertyChanged("DescriptionA");
			}
		}
	}

	public virtual string DisplayNameA
	{
		get
		{
			return DisplayName;
		}
		set
		{
			if (DisplayName != value)
			{
				DisplayName = value;
				OnPropertyChanged("DisplayNameA");
			}
		}
	}

	public string FullDisplayName => GetVmModule()?.FullName + "." + MultiLanguageDisplayNameA;

	public virtual string MultiLanguageDisplayNameA => string.IsNullOrEmpty(DisplayNameA) ? "" : AppLocalizationService.GetLang(DisplayNameA);

	public string VisibilityA
	{
		get
		{
			return Visibility;
		}
		set
		{
			Visibility = value;
			OnPropertyChanged("VisibilityA");
		}
	}

	public string AccessModeA
	{
		get
		{
			return AccessMode;
		}
		set
		{
			AccessMode = value;
			OnPropertyChanged("AccessModeA");
		}
	}

	public ObservableCollection<object> TriggersA
	{
		get
		{
			return Triggers;
		}
		set
		{
			Triggers = value;
			OnPropertyChanged("TriggersA");
		}
	}

	public ObservableCollection<object> InitersA
	{
		get
		{
			return Initers;
		}
		set
		{
			Initers = value;
			OnPropertyChanged("InitersA");
		}
	}

	public string OldValue { get; set; }

	public virtual string Value { get; set; }

	public virtual string DisplayType
	{
		get
		{
			return GetType().Name;
		}
		set
		{
		}
	}

	public object GroupName => GetParentNode();

	public int LangCameraNomalWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return 120;
			}
			return 65;
		}
	}

	public virtual ItemState MyItemState
	{
		get
		{
			return _MyItemState;
		}
		set
		{
			if (_MyItemState != value)
			{
				_MyItemState = value;
				OnPropertyChanged("MyItemState");
			}
		}
	}

	public string ComTailName { get; set; }

	public string ComName => Name + ((ComTailName == null) ? "" : ComTailName);

	private bool CanSubscribe
	{
		get
		{
			if (parentNode is Category category)
			{
				if (category.GetParentNode() is Tab { TabTypeA: TabTypeEnum.Panal })
				{
					return false;
				}
				return category.SubscribeVisibleA;
			}
			return true;
		}
	}

	public bool SubscribeVisibleA
	{
		get
		{
			return CanSubscribe && SubscribeVisible;
		}
		set
		{
			if (SubscribeVisible != value)
			{
				SubscribeVisible = value;
				OnPropertyChanged("SubscribeVisibleA");
			}
		}
	}

	public bool CanTriggerExecute
	{
		get
		{
			if (!_canTriggerExecute.HasValue)
			{
				if (parentNode is ICanTriggerExecute canTriggerExecute)
				{
					_canTriggerExecute = canTriggerExecute.CanTriggerExecute;
					return _canTriggerExecute.Value;
				}
				_canTriggerExecute = false;
				return _canTriggerExecute.Value;
			}
			return _canTriggerExecute.Value;
		}
		set
		{
		}
	}

	public bool IsDeleteSubscribeA
	{
		get
		{
			return IsDeleteSubscribe;
		}
		set
		{
			if (IsDeleteSubscribe != value)
			{
				IsDeleteSubscribe = value;
				OnPropertyChanged("IsDeleteSubscribeA");
			}
		}
	}

	public bool IsGetFromModule
	{
		get
		{
			return _isGetFromModule;
		}
		set
		{
			if (value != _isGetFromModule)
			{
				_isGetFromModule = value;
			}
		}
	}

	string IName.Name
	{
		get
		{
			return Name;
		}
		set
		{
		}
	}

	public object Operation
	{
		get
		{
			if (_operation == null && GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				_operation = algorithmTabRoot.Owner;
			}
			return _operation;
		}
		set
		{
			if (_operation != value)
			{
				_operation = value;
				OnPropertyChanged("Operation");
			}
		}
	}

	public PropertyInfo NameProA
	{
		get
		{
			if (NamePro == null && Name != null)
			{
				if (!Name.Contains("."))
				{
					NamePro = Operation?.GetType().GetProperty(Name);
				}
				else
				{
					string[] array = Name.Split('.');
					object obj = Operation;
					string[] array2 = array;
					foreach (string text in array2)
					{
						if (!(array.Last() == text))
						{
							obj = GetPropertyValue(obj, text);
							if (obj == null)
							{
								return null;
							}
						}
					}
					NamePro = obj.GetType().GetProperty(array.Last());
				}
			}
			if (NamePro == null && Operation != null && Operation is IName)
			{
				throw new Exception($"操作项：{(Operation as IName).Name} 中找不到名称为{Name}的属性或属性路径");
			}
			return NamePro;
		}
	}

	public ICommand ParamItemSelectedCommand
	{
		get
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Expected Obj, but got Unknown
			if (paramItemSelectedCommand == null)
			{
				paramItemSelectedCommand = new DelegateCommand((Action)ParamItemSelectedCommandExecute);
			}
			return (ICommand)paramItemSelectedCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler SetValueCompleted;

	public event EventHandler GetValueCompleted;

	public event EventHandler SetValueBegin;

	public static event ParamValueChangedEventHandler OnParamValueChanged;

	public event CurValueChangedHandler OnCurValueChanged;

	public virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
		if (!string.IsNullOrEmpty(TriggerCondition) && TriggerCondition.Equals(proName))
		{
			return;
		}
		try
		{
			TriggerBusiness(proName);
			if ("CustomVisibleA" == proName && parentNode is Apps.XmlParser.Category category && category.VisibleChanged())
			{
				GetParentNode<Tab>()?.VisibleChanged();
			}
			if ("CurValueA" == proName && OnCurValueChanged != null)
			{
				OnCurValueChanged(this, null);
			}
		}
		catch (Exception ex)
		{
			if (proName == "CurValueA" && this is ButtonSelecter)
			{
				throw new Exception(ex.Message);
			}
		}
	}

	protected internal void SetTriggerDisable(string Condition)
	{
		TriggerCondition = Condition;
	}

	protected internal void SetTriggerEnable()
	{
		TriggerCondition = "";
	}

	protected void RaiseOnCurValueChangedEvent()
	{
		if (OnCurValueChanged != null)
		{
			OnCurValueChanged(this, null);
		}
	}

	protected virtual void NotifyChanges()
	{
	}

	protected bool IsUnConnect()
	{
		return false;
	}

	public virtual void XmlLoadedTask()
	{
		if (Triggers != null)
		{
			foreach (ITrigger trigger4 in Triggers)
			{
				if (trigger4.GetSetters() == null)
				{
					continue;
				}
				foreach (Setter setter4 in trigger4.GetSetters())
				{
					setter4.Init();
				}
			}
		}
		if (Initers != null)
		{
			foreach (object initer in Initers)
			{
				(initer as Setter)?.Init();
			}
		}
		if (DeIniters != null)
		{
			foreach (ITrigger deIniter in DeIniters)
			{
				if (deIniter.GetSetters() == null)
				{
					continue;
				}
				foreach (Setter setter5 in deIniter.GetSetters())
				{
					setter5.Init();
				}
			}
		}
		if (ExternIniters == null)
		{
			return;
		}
		foreach (ITrigger externIniter in ExternIniters)
		{
			if (externIniter.GetSetters() == null)
			{
				continue;
			}
			foreach (Setter setter6 in externIniter.GetSetters())
			{
				setter6.Init();
			}
		}
	}

	public void NotifyTrigger()
	{
		if (NeedInit)
		{
			OnPropertyChanged("CurValueA");
			OnPropertyChanged("CustomVisibleA");
		}
	}

	protected void TriggerBusiness(string proName)
	{
		if (Triggers == null)
		{
			return;
		}
		try
		{
			Interlocked.Increment(ref FilterPreRun);
			foreach (ITrigger trigger3 in Triggers)
			{
				if (trigger3.CheckProperty(proName))
				{
					trigger3.ReOnTrigger();
				}
			}
			foreach (ITrigger trigger4 in Triggers)
			{
				if (trigger4.CheckProperty(proName))
				{
					trigger4.OnTrigger(this);
				}
			}
		}
		finally
		{
			Interlocked.Decrement(ref FilterPreRun);
			if (FilterPreRun == 0 && FilterPreRunSub > 0)
			{
				PreExecuteRun();
			}
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (Initers != null && Initers.Count > 0)
		{
			foreach (Setter initer in Initers)
			{
				initer.CreatOperateInstance();
			}
		}
		return this;
	}

	public virtual void InniterBusiness()
	{
		if (Initers != null)
		{
			foreach (Setter initer in Initers)
			{
				initer.Operate(null);
			}
		}
		if (ExternIniters == null)
		{
			return;
		}
		foreach (ITrigger externIniter in ExternIniters)
		{
			externIniter.OnTrigger(this);
		}
	}

	public virtual void OnInniterBusinessFinish()
	{
	}

	public virtual void OnShownFinish()
	{
		if (DeIniters == null)
		{
			return;
		}
		foreach (ITrigger deIniter in DeIniters)
		{
			deIniter.OnTrigger(this);
		}
	}

	public string GetSaveString(NodePathType mode)
	{
		BaseNode baseNode = this;
		BaseNode baseNode2 = baseNode.GetParentNode() as BaseNode;
		StringBuilder stringBuilder = new StringBuilder(Name);
		while (baseNode2 != null)
		{
			FieldInfo[] fields = baseNode2.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
			IEnumerable<FieldInfo> enumerable = fields.Where((FieldInfo p) => typeof(IList).IsAssignableFrom(p.FieldType));
			bool flag = false;
			foreach (FieldInfo item in enumerable)
			{
				if (item.GetValue(baseNode2) is IList list && list.Contains(baseNode))
				{
					stringBuilder.Insert(0, '.').Insert(0, item.Name);
					if (baseNode2 is IName)
					{
						stringBuilder.Insert(0, '.').Insert(0, (baseNode2 as IName).Name);
					}
					baseNode = baseNode2;
					baseNode2 = baseNode.GetParentNode() as BaseNode;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return "";
			}
		}
		if (baseNode is IOwner)
		{
			if (mode == NodePathType.Normal)
			{
			}
			return stringBuilder.ToString();
		}
		return "";
	}

	public IParamsConfig GetModuleTagInfo()
	{
		if (_moduleTag != null)
		{
			return _moduleTag;
		}
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			_moduleTag = algorithmTabRoot.UserStringData;
		}
		return _moduleTag;
	}

	public virtual void SetPropertyValueByName(string Name, object value)
	{
		PropertyInfo[] properties = GetType().GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (!(propertyInfo.Name == Name))
			{
				continue;
			}
			if (value is string text)
			{
				TypeConverter converter = TypeDescriptor.GetConverter(propertyInfo.PropertyType);
				if (converter.CanConvertFrom(typeof(string)))
				{
					object value2 = converter.ConvertFromInvariantString(text);
					propertyInfo.SetValue(this, value2);
				}
				else
				{
					propertyInfo.SetValue(this, value);
				}
			}
			else
			{
				propertyInfo.SetValue(this, value);
			}
			break;
		}
	}

	public void SetPropertyValueByNameNotrigger(string Name, object value)
	{
		SetTriggerDisable(Name);
		SetPropertyValueByName(Name, value);
		SetTriggerEnable();
	}

	public virtual object GetPropertyValueByName(string Name)
	{
		PropertyInfo[] properties = GetType().GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (propertyInfo.Name == Name)
			{
				return propertyInfo.GetValue(this, null);
			}
		}
		return null;
	}

	public VmModule GetVmModule()
	{
		if (_vmModule != null)
		{
			return _vmModule;
		}
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			_vmModule = algorithmTabRoot.Owner as VmModule;
		}
		return _vmModule;
	}

	public override string ToString()
	{
		return DisplayType + " " + Name + " " + DisplayName;
	}

	protected virtual void OnSetValueCompleted(bool success)
	{
		if (SetValueCompleted != null)
		{
			SetValueCompleted(this, success ? new EventArgs() : null);
		}
	}

	protected virtual void OnGetValueCompleted(bool success)
	{
		if (GetValueCompleted != null)
		{
			GetValueCompleted(this, success ? new EventArgs() : null);
		}
	}

	protected virtual void OnSetValueBegin()
	{
		if (SetValueBegin != null)
		{
			SetValueBegin(this, null);
		}
	}

	public void InvokeGetValueCompleted(bool success)
	{
		if (GetValueCompleted != null)
		{
			GetValueCompleted(this, success ? new EventArgs() : null);
		}
	}

	public void InvokeSetValueCompleted(bool success)
	{
		if (SetValueCompleted != null)
		{
			SetValueCompleted(this, success ? new EventArgs() : null);
		}
	}

	internal int SetParamUnit(NameValueItem parent)
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("w"))
		{
			OnSetValueCompleted(success: false);
			return -2;
		}
		string value = Value;
		try
		{
			Check(parent, value);
		}
		catch (Exception ex)
		{
			string lang = AppLocalizationService.GetLang("Lang_ParamSetTip");
			HikMessageBox.Show(lang + ex.Message, MessageIcons.Error);
			Value = OldValue;
			OnSetValueCompleted(success: false);
			return -2;
		}
		return SetParamUnitByZmq(parent, value);
	}

	public int SetParamUnitByZmq(NameValueItem parent, string value)
	{
		if (userStringData == null)
		{
			userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
		}
		MyItemState = ItemState.Normal;
		try
		{
			CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
			{
				string text = ConvertEscape((parent == null) ? userStringData[ComName] : parent.InnerGetStringParamValue(ComName));
				string text2 = ConvertEscape(value);
				if (FullDisplayName.Contains("密码") || FullDisplayName.ToLower().Contains("password"))
				{
					text = "*****";
					text2 = "*****";
				}
				logManager?.LogCreate("信息", FullDisplayName + " " + AppLocalizationService.GetLang("参数修改") + text + AppLocalizationService.GetLang("新值") + text2, ifLang: false);
				InvokeOnParamValueChanged(text, text2);
			});
			SetParamUnitDirect(parent, value);
		}
		catch (VmException ex)
		{
			if (ex.errorCode == -1610612734)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + ex.errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + " " + AppLocalizationService.GetLang("Lang_FdPCreateErrtoParam"), MessageIcons.Error);
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + " " + ex.errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + " " + AppLocalizationService.GetLang(ErrorCodeInfo.GetErrorInfo(ex.errorCode)), MessageIcons.Error);
			}
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return -1;
		}
		catch
		{
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return -1;
		}
		OldValue = value;
		OnSetValueCompleted(success: true);
		return 0;
	}

	public virtual int SetParamUnit()
	{
		return SetParamUnit(null);
	}

	public void SetParamUnitDirect(NameValueItem parent, string value)
	{
		if (userStringData == null)
		{
			userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
		}
		if (parent == null)
		{
			userStringData[ComName] = value;
		}
		else
		{
			parent.InnerSetStringParamValue(ComName, value);
		}
	}

	public void SetParamUnitDirect(string value)
	{
		SetParamUnitDirect(null, value);
	}

	public int SetParamUnitByZmq(string value)
	{
		return SetParamUnitByZmq(null, value);
	}

	private string ConvertEscape(string value)
	{
		if (value.Contains("\r\n"))
		{
			value = value.Replace("\r\n", "'\\r\\n'");
		}
		else if (value.Contains("\r"))
		{
			value = value.Replace("\r", "'\\r'");
		}
		else if (value.Contains("\n"))
		{
			value = value.Replace("\n", "'\\n'");
		}
		return value;
	}

	public void InvokeOnParamValueChanged(string oldValue, string newValue)
	{
		ParamValueChangedEventArgs args = new ParamValueChangedEventArgs
		{
			ParamName = FullDisplayName,
			OldValue = oldValue,
			NewValue = newValue
		};
		OnParamValueChanged?.Invoke(this, args);
	}

	internal int GetParamUnit(NameValueItem parent)
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("r"))
		{
			OnGetValueCompleted(success: false);
			return -2;
		}
		return GetParamUnitByZmq(parent);
	}

	public int GetParamUnitByZmq(NameValueItem parent)
	{
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		if (userStringData == null)
		{
			userStringData = algorithmTabRoot.UserStringData;
		}
		MyItemState = ItemState.Normal;
		string empty = string.Empty;
		try
		{
			empty = ((parent == null) ? userStringData[ComName] : parent.InnerGetStringParamValue(ComName));
			try
			{
				if (parent != null)
				{
					Check(parent, empty);
				}
				else
				{
					Check(empty);
				}
				if (Value != empty)
				{
					OldValue = empty;
					Value = empty;
				}
			}
			catch
			{
				OnGetValueCompleted(success: false);
				SetParamUnit();
			}
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			OnGetValueCompleted(success: false);
			return -1;
		}
		OnGetValueCompleted(success: true);
		return 0;
	}

	public virtual int GetParamUnit()
	{
		return GetParamUnit(null);
	}

	public int GetParamUnitByZmq()
	{
		return GetParamUnitByZmq(null);
	}

	public void IsGetFromDictionary(ref string tempStr, AlgorithmTabRoot algorithRoot)
	{
		if (userStringData == null)
		{
			userStringData = algorithRoot.UserStringData;
		}
		if (IsGetFromModule || _IsResetAlgoParamsing)
		{
			tempStr = userStringData[ComName];
		}
		else
		{
			tempStr = (((algorithRoot.Owner as IParamsOperator)?.ParamsDictionary?.ContainsKey(ComName) ?? false) ? Hik.Util.StringUtil.UTF8GetString((algorithRoot.Owner as IParamsOperator).ParamsDictionary[ComName]) : userStringData[ComName]);
		}
	}

	public int SetParamUnitForFormatBinaryData(string value)
	{
		if (!AccessMode.ToLower().Contains("w"))
		{
			OnSetValueCompleted(success: false);
			return -2;
		}
		return SetParamUnitByZmqForFormatBinaryData(value);
	}

	public int SetParamUnitByZmqForFormatBinaryData(string value)
	{
		return 0;
	}

	public virtual void Check(NameValueItem parent, string value)
	{
	}

	public virtual void Check(string value)
	{
	}

	protected virtual void ReSetCurValueA()
	{
	}

	internal void SetUserData(string value)
	{
		if ((AccessMode != null && AccessMode.ToLower().Contains('w')) || !((GetTopBaseNode() as IOwner)?.Owner is IUserData userData) || string.IsNullOrEmpty(ComName))
		{
			return;
		}
		bool flag = true;
		byte[] array = userData["Param." + ComName];
		if (array != null)
		{
			string text = Hik.Util.StringUtil.UTF8GetString(array);
			if (text == value)
			{
				flag = false;
			}
		}
		if (flag)
		{
			userData["Param." + ComName] = Hik.Util.StringUtil.UTF8GetBytesPadZero(value);
		}
	}

	internal void GetUserData()
	{
		if ((AccessMode == null || !AccessMode.ToLower().Contains('r')) && (GetTopBaseNode() as IOwner)?.Owner is IUserData userData && !string.IsNullOrEmpty(ComName))
		{
			byte[] array = userData["Param." + ComName];
			if (array != null)
			{
				Value = Hik.Util.StringUtil.UTF8GetString(array);
			}
			else
			{
				SetDefaultValue();
			}
		}
	}

	protected virtual void SetDefaultValue()
	{
	}

	public string InnerGetStringParamValue(string key)
	{
		if (!string.IsNullOrEmpty(Name) && Name.StartsWith("%"))
		{
			return ((IUserStringData)ParamsSource)[Name + "," + key];
		}
		return ((IUserStringData)ParamsSource)[key];
	}

	public void InnerSetStringParamValue(string key, string value)
	{
		if (!string.IsNullOrEmpty(Name) && Name.StartsWith("%"))
		{
			((IUserStringData)ParamsSource)[Name + "," + key] = value;
		}
		else
		{
			((IUserStringData)ParamsSource)[key] = value;
		}
	}

	public byte[] InnerGetByteParamValue(string key)
	{
		if (!string.IsNullOrEmpty(Name) && Name.StartsWith("%"))
		{
			return ((IUserBytesData)ParamsSource)[Name + "," + key];
		}
		return ((IUserBytesData)ParamsSource)[key];
	}

	public void InnerSetByteParamValue(string key, byte[] value)
	{
		if (!string.IsNullOrEmpty(Name) && Name.StartsWith("%"))
		{
			((IUserBytesData)ParamsSource)[Name + "," + key] = value;
		}
		else
		{
			((IUserBytesData)ParamsSource)[key] = value;
		}
	}

	public byte[] InnerGetByteUserDataValue(string key)
	{
		if (ParamsSource is IVmModule vmModule)
		{
			if (!string.IsNullOrEmpty(Name) && Name.StartsWith("%"))
			{
				return vmModule.InnerUserData[Name + "&?;?" + key];
			}
			return vmModule.UserData[key];
		}
		return null;
	}

	public void InnerSetByteUserDataValue(string key, byte[] value)
	{
		if (ParamsSource is IVmModule vmModule)
		{
			if (!string.IsNullOrEmpty(Name) && Name.StartsWith("%"))
			{
				vmModule.InnerUserData[Name + "&?;?" + key] = value;
			}
			else
			{
				vmModule.UserData[key] = value;
			}
		}
	}

	private static object GetPropertyValue(object target, string PropertyName)
	{
		PropertyInfo[] properties = target.GetType().GetProperties();
		PropertyInfo propertyInfo;
		if ((object)(propertyInfo = properties.FirstOrDefault((PropertyInfo p) => p.Name == PropertyName)) != null)
		{
			return propertyInfo.GetValue(target);
		}
		return null;
	}

	public object GetParam()
	{
		if (NameProA == null)
		{
			return null;
		}
		return Operation.GetPropertyValueByName(Name);
	}

	public void SetParam(object value)
	{
		if (!(NameProA == null))
		{
			Operation.SetPropertyValueByName(Name, value);
		}
	}

	public void SetIntParam(int value)
	{
		switch (Type.GetTypeCode(NameProA.PropertyType))
		{
		case TypeCode.Int16:
		case TypeCode.Int32:
		case TypeCode.Int64:
			SetParam(value);
			break;
		case TypeCode.UInt16:
		case TypeCode.UInt32:
		case TypeCode.UInt64:
			SetParam((uint)value);
			break;
		}
	}

	public int GetIntParam()
	{
		switch (Type.GetTypeCode(NameProA.PropertyType))
		{
		case TypeCode.Int16:
		case TypeCode.Int32:
		case TypeCode.Int64:
			return (int)GetParam();
		case TypeCode.UInt16:
		case TypeCode.UInt32:
		case TypeCode.UInt64:
			return (int)(uint)GetParam();
		default:
			throw new Exception("In_IntFail");
		}
	}

	public virtual void SetParamValue(string value)
	{
		if (SetParamUnitExtern != null)
		{
			SetParamUnitExtern(this, new ObjectEventArgs
			{
				Param = value
			});
		}
	}

	public virtual void GetParamValue()
	{
	}

	public void PreExecuteRun()
	{
		if (FilterPreRun > 0)
		{
			FilterPreRunSub++;
			return;
		}
		FilterPreRunSub = 0;
		if (VmSolution.IsParamModifyExecute)
		{
			if (parentNode is IDynamicItemOfGroup || (_vmModule is VmModuleSet && parentNode is NameValueItem nameValueItem && nameValueItem.parentNode is IDynamicItemOfGroup))
			{
				ExecuteRun(isDynamic: true);
			}
			else if (CanTriggerExecute)
			{
				ExecuteRun();
			}
		}
	}

	public void PreExecuteRunModel()
	{
		if (VmSolution.IsParamModifyExecute && VmSolution.IsModelModifyExecute)
		{
			if (parentNode is IDynamicItemOfGroup || (_vmModule is VmModuleSet && parentNode is NameValueItem nameValueItem && nameValueItem.parentNode is IDynamicItemOfGroup))
			{
				ExecuteRun(isDynamic: true);
			}
			else
			{
				ExecuteRun();
			}
		}
	}

	protected virtual void ExecuteRun(bool isDynamic = false)
	{
		if (((IVMRun)VmSolution.Instance).IsRunning)
		{
			return;
		}
		VmModule vmModule = GetVmModule();
		if (isDynamic)
		{
			NameValueItem nameValueItem = parentNode as NameValueItem;
			VmModuleSet obj = nameValueItem.ParamsSource as VmModuleSet;
			vmModule = ((obj != null) ? obj.GetFirstParamRelationModule(nameValueItem.NameA) : null) as VmModule;
		}
		if (((IVMRun)VmSolution.Instance).IsRunning)
		{
			return;
		}
		IList<object> list = ((vmModule?["[Display]"] is ParamRoot paramRoot) ? paramRoot.GetDisplayList() : null);
		if (list != null)
		{
			foreach (object item in list)
			{
				(item as Object)?.ClearValueStore();
			}
		}
		vmModule?.Run();
	}

	protected bool IsSubscribeStr(string str)
	{
		if (string.IsNullOrWhiteSpace(str) || str.Length > 64)
		{
			return false;
		}
		if (MathHelper.IsNumber(str) || MathHelper.IsConst(str))
		{
			return false;
		}
		return true;
	}

	protected virtual void ParamItemSelectedCommandExecute()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot { IsSubscribeInWindow: not false } algorithmTabRoot)
		{
			algorithmTabRoot.SelectedSubscribeParam = null;
		}
	}
}
