using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Util;
using VM.Controls;
using VMControls.BaseInterface;

namespace IMVSBlobFindLabelsModuControl;

public partial class ThresholdAreaPanel : UserControl, IComponentConnector, IStyleConnector
{
	private object _lock = new object();

	private bool init;

	private bool changeByget;

	private IParamsConfig _param;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(ThresholdAreaPanel), new PropertyMetadata(null));

	public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register("Items", typeof(ObservableCollection<ThresholdAreaItem>), typeof(ThresholdAreaPanel), new PropertyMetadata((object)null));

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

	public ObservableCollection<ThresholdAreaItem> Items
	{
		get
		{
			return (ObservableCollection<ThresholdAreaItem>)GetValue(ItemsProperty);
		}
		set
		{
			SetValue(ItemsProperty, value);
		}
	}

	public int MaxAreaCount => 255;

	public Thickness GrayValueSpace
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return new Thickness(4.0, 0.0, -15.0, 0.0);
			}
			return new Thickness(-3.0, 0.0, 0.0, 0.0);
		}
	}

	public ThresholdAreaPanel()
	{
		InitializeComponent();
		Items = new ObservableCollection<ThresholdAreaItem>();
		WeakEventManager<ObservableCollection<ThresholdAreaItem>, NotifyCollectionChangedEventArgs>.AddHandler(Items, "CollectionChanged", OnCollectionChanged);
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!init)
			{
				GetAreaList();
				init = true;
			}
		};
	}

	private void AddModuleItem_OnClick(object sender, RoutedEventArgs e)
	{
		if (Items.Count >= MaxAreaCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " : " + MaxAreaCount, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		ThresholdAreaItem thresholdAreaItem = new ThresholdAreaItem
		{
			VmParam = Param,
			Owner = this
		};
		int nValue;
		for (nValue = 1; Enumerable.Count(Items, (ThresholdAreaItem p) => p.GrayValue.CurValueA == nValue) != 0; nValue++)
		{
		}
		AddNewItem(thresholdAreaItem);
		thresholdAreaItem.GrayValue.CurValueA = nValue;
	}

	private void Delete_OnHandler(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: ThresholdAreaItem dataContext })
		{
			dataContext.IsAdd = false;
			SetThresholdArea(dataContext);
			WeakEventManager<ThresholdAreaItem, PropertyChangedEventArgs>.RemoveHandler(dataContext, "PropertyChanged", ValueChanged);
			if (Items.Contains(dataContext))
			{
				Items.Remove(dataContext);
			}
		}
	}

	private void ValueChanged(object sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (propertyName == "IsInverse")
		{
			SetThresholdArea(sender as ThresholdAreaItem);
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
		}
	}

	private void Source_MouseDown(object sender, MouseButtonEventArgs e)
	{
		controlItem.Focus();
	}

	private void ItemsControl_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		e.Handled = true;
	}

	private void AddNewItem(ThresholdAreaItem item)
	{
		Items.Add(item);
		WeakEventManager<ThresholdAreaItem, PropertyChangedEventArgs>.AddHandler(item, "PropertyChanged", ValueChanged);
	}

	private void SetThresholdArea(ThresholdAreaItem item)
	{
		if (Param == null || item == null)
		{
			return;
		}
		lock (_lock)
		{
			if (changeByget)
			{
				return;
			}
			try
			{
				((IUserStringData)Param)["ThresholdArea"] = item.ThresholdAreaData;
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				LogHelper.moduleLog.Error("Set threshold area error: " + text);
			}
		}
	}

	private void GetAreaList()
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
				byte[] array = ((IUserBytesData)Param)["ThresholdAreaList"];
				if (array != null && array.Length != 0)
				{
					string[] array2 = StringUtil.UTF8GetString(array).Split(';');
					for (int i = 0; i < array2.Length; i++)
					{
						string[] array3 = array2[i].Split('#');
						if (array3.Length >= 4)
						{
							ThresholdAreaItem thresholdAreaItem = new ThresholdAreaItem();
							thresholdAreaItem.GrayValue.Value = array3[0];
							(thresholdAreaItem.AreaRange.IntegersA[0] as Integer).Value = array3[1];
							(thresholdAreaItem.AreaRange.IntegersA[1] as Integer).Value = array3[2];
							thresholdAreaItem.IsInverse = Convert.ToBoolean(int.Parse(array3[3]));
							thresholdAreaItem.VmParam = Param;
							thresholdAreaItem.Owner = this;
							AddNewItem(thresholdAreaItem);
						}
					}
				}
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				LogHelper.moduleLog.Error("Get area list error: " + text);
			}
			changeByget = false;
		}
	}
}
