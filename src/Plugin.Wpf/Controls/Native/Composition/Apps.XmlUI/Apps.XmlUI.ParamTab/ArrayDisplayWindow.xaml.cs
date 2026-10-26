using System;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class ArrayDisplayWindow : BaseWindow, IComponentConnector
{
	public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register("Items", typeof(ObservableCollection<ValueItem>), typeof(ArrayDisplayWindow), new PropertyMetadata((object)null));

	public static readonly DependencyProperty BindItemsProperty = DependencyProperty.Register("BindItems", typeof(ObservableCollection<ValueItem>), typeof(ArrayDisplayWindow), new PropertyMetadata((object)null));

	private int totalPageNum = 0;

	public static readonly DependencyProperty ItemsTotalCountProperty = DependencyProperty.Register("ItemsTotalCount", typeof(int), typeof(ArrayDisplayWindow), new PropertyMetadata(0));

	public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register("PageCount", typeof(int), typeof(ArrayDisplayWindow), new PropertyMetadata(40));

	private int _CurrentPageNum = 0;

	public string WindowTitle { get; set; }

	public ObservableCollection<ValueItem> Items
	{
		get
		{
			return (ObservableCollection<ValueItem>)GetValue(ItemsProperty);
		}
		set
		{
			SetValue(ItemsProperty, value);
		}
	}

	public ObservableCollection<ValueItem> BindItems
	{
		get
		{
			return (ObservableCollection<ValueItem>)GetValue(BindItemsProperty);
		}
		set
		{
			SetValue(BindItemsProperty, value);
		}
	}

	public int ItemsTotalCount
	{
		get
		{
			return (int)GetValue(ItemsTotalCountProperty);
		}
		set
		{
			SetValue(ItemsTotalCountProperty, value);
		}
	}

	public int PageCount
	{
		get
		{
			return (int)GetValue(PageCountProperty);
		}
		set
		{
			SetValue(PageCountProperty, value);
		}
	}

	public int CurrentPageNum
	{
		get
		{
			return _CurrentPageNum;
		}
		set
		{
			if (_CurrentPageNum != value)
			{
				_CurrentPageNum = value;
				OnPropertyChanged("CurrentPageNum");
				SetBindData();
			}
		}
	}

	public ArrayDisplayWindow(string title, string[] arryValue)
	{
		WindowTitle = title;
		InitializeComponent();
		InitItems(arryValue);
	}

	public void UpdateArrayValue(string[] arryValue)
	{
		Items.Clear();
		BindItems.Clear();
		InitItems(arryValue);
		InitPageData();
	}

	private void InitItems(string[] arryValue)
	{
		Items = new ObservableCollection<ValueItem>();
		BindItems = new ObservableCollection<ValueItem>();
		_CurrentPageNum = 0;
		if (arryValue != null)
		{
			int count = 0;
			Array.ForEach(arryValue, (string x) =>
			{
				count++;
				Items.Add(new ValueItem
				{
					Index = count + ":",
					Value = x
				});
			});
		}
	}

	private void SetBindData()
	{
		if (CurrentPageNum > 0)
		{
			int num = (CurrentPageNum - 1) * PageCount;
			int num2 = ((CurrentPageNum < totalPageNum) ? (num + PageCount) : Items.Count);
			BindItems.Clear();
			for (int i = num; i < num2; i++)
			{
				BindItems.Add(Items[i]);
			}
		}
	}

	private void InitPageData()
	{
		if (Items != null)
		{
			int num = 0;
			ItemsTotalCount = Items.Count;
			if (PageCount > 0)
			{
				num = ((ItemsTotalCount % PageCount <= 0) ? (ItemsTotalCount / PageCount) : (ItemsTotalCount / PageCount + 1));
			}
			totalPageNum = num;
			CurrentPageNum = 1;
		}
	}

	private void arryDlg_Loaded(object sender, RoutedEventArgs e)
	{
		InitPageData();
	}

}
