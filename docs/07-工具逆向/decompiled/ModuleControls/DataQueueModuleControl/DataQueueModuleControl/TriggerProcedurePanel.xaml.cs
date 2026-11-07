using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using VM.Controls;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace DataQueueModuleControl;

public partial class TriggerProcedurePanel : UserControl, IComponentConnector, IStyleConnector
{
	private object _lock = new object();

	private bool init;

	private bool changeByget;

	private IVmModule _module;

	private IParamsConfig _param;

	private Apps.XmlParser.ParamTab.Boolean _triggerBoolean;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(TriggerProcedurePanel), new PropertyMetadata(null));

	public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register("Items", typeof(ObservableCollection<TriggerProcessItem>), typeof(TriggerProcedurePanel), new PropertyMetadata((object)null));

	public IVmModule Module
	{
		get
		{
			if (_module != null)
			{
				return _module;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return _module;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return _module;
			}
			if (nameValueItem.GetTopBaseNode() is IOwner owner)
			{
				_module = owner.Owner as IVmModule;
			}
			return _module;
		}
	}

	public IParamsConfig Param
	{
		get
		{
			if (_param != null)
			{
				return _param;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return null;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return null;
			}
			if (nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				_param = algorithmTabRoot.UserStringData;
			}
			return _param;
		}
	}

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public ObservableCollection<TriggerProcessItem> Items
	{
		get
		{
			return (ObservableCollection<TriggerProcessItem>)GetValue(ItemsProperty);
		}
		set
		{
			SetValue(ItemsProperty, value);
		}
	}

	public int MaxTriggerCount => 16;

	public Apps.XmlParser.ParamTab.Boolean TriggerBoolean
	{
		get
		{
			if (_triggerBoolean == null)
			{
				_triggerBoolean = new Apps.XmlParser.ParamTab.Boolean();
				_triggerBoolean.NameA = "AsynchronousTrigger";
				_triggerBoolean.AccessModeA = "RW";
				_triggerBoolean.CurValueA = false;
			}
			return _triggerBoolean;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public TriggerProcedurePanel()
	{
		InitializeComponent();
		Items = new ObservableCollection<TriggerProcessItem>();
		WeakEventManager<ObservableCollection<TriggerProcessItem>, NotifyCollectionChangedEventArgs>.AddHandler(Items, "CollectionChanged", OnCollectionChanged);
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!init)
			{
				GetProcessList();
				if (_triggerBoolean != null)
				{
					_triggerBoolean.SetParentNode(ParentNode);
					if (Param != null)
					{
						string empty = string.Empty;
						empty = ((IUserStringData)Param)["AsynchronousTrigger"];
						bool result = false;
						_triggerBoolean.CurValueA = bool.TryParse(empty, out result) && result;
					}
					else
					{
						_triggerBoolean.CurValueA = false;
					}
					NotifyPropertyChanged("TriggerBoolean");
				}
				init = true;
			}
		};
	}

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private void AddProcessItem_OnClick(object sender, RoutedEventArgs e)
	{
		if (Items.Count >= MaxTriggerCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " : " + MaxTriggerCount, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			AddNewItem(new TriggerProcessItem());
		}
	}

	private void Delete_OnHandler(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: TriggerProcessItem dataContext })
		{
			WeakEventManager<TriggerProcessItem, PropertyChangedEventArgs>.RemoveHandler(dataContext, "PropertyChanged", ValueChanged);
			if (Items.Contains(dataContext))
			{
				Items.Remove(dataContext);
			}
		}
	}

	private void ValueChanged(object sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (propertyName == "TriggerProcessID")
		{
			SetTriggerProcess(sender as TriggerProcessItem);
		}
	}

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Reset)
		{
			for (int i = 0; i < Items.Count; i++)
			{
				Items[i].Index = i + 1;
			}
			SetProcessList();
		}
	}

	private void Source_MouseDown(object sender, MouseButtonEventArgs e)
	{
		controlItem.Focus();
	}

	private void AddNewItem(TriggerProcessItem item)
	{
		Items.Add(item);
		WeakEventManager<TriggerProcessItem, PropertyChangedEventArgs>.AddHandler(item, "PropertyChanged", ValueChanged);
	}

	private void SetTriggerProcess(TriggerProcessItem item)
	{
		if (changeByget || Param == null || item == null)
		{
			return;
		}
		lock (_lock)
		{
			try
			{
				((IUserStringData)Param)["TriggerProcess"] = item.TriggerProcessData;
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				LogHelper.moduleLog.Error("SetTriggerProcess error: " + text);
			}
		}
	}

	private void SetProcessList()
	{
		if (Param == null || changeByget)
		{
			return;
		}
		string text = "";
		for (int i = 0; i < Items.Count; i++)
		{
			text += Items[i].TriggerProcessID;
			text += ",";
		}
		lock (_lock)
		{
			try
			{
				((IUserStringData)Param)["TriggerProcessList"] = text;
			}
			catch
			{
			}
		}
	}

	private void GetProcessList()
	{
		if (Param == null)
		{
			return;
		}
		lock (_lock)
		{
			changeByget = true;
			try
			{
				string[] array = ((IUserStringData)Param)["TriggerProcessList"].Split(',');
				for (int i = 0; i < array.Length - 1; i++)
				{
					AddNewItem(new TriggerProcessItem
					{
						TriggerProcessID = int.Parse(array[i])
					});
				}
			}
			catch
			{
			}
			changeByget = false;
		}
	}
}
