using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Interface;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSImageCombineProcessModuControl;

public partial class ImageProcessPanel : UserControl, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private const string GetSetData = "ImageProcessInfo";

	private int _dataLen = Marshal.SizeOf<IMAGEPROCESS_COMBINATION_INFO>();

	private ObservableCollection<ImageProcessItem> _items;

	private uint IMVS_OK;

	private uint IMVS_E_MODU_PARAMETER = 2684354561u;

	private string[] _operators;

	private IVmModule _module;

	private IParamsConfig _param;

	private AlgorithmTabRoot _root;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(ImageProcessPanel), new PropertyMetadata(null));

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

	public AlgorithmTabRoot Root
	{
		get
		{
			if (_root != null)
			{
				return _root;
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
			_root = nameValueItem.GetTopBaseNode() as AlgorithmTabRoot;
			return _root;
		}
	}

	public ObservableCollection<ImageProcessItem> Items
	{
		get
		{
			return _items;
		}
		set
		{
			_items = value;
			OnPropertyChanged("Items");
		}
	}

	public string[] Operators
	{
		get
		{
			return _operators;
		}
		set
		{
			_operators = value;
			OnPropertyChanged("Operators");
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

	public event PropertyChangedEventHandler PropertyChanged;

	public ImageProcessPanel()
	{
		InitializeComponent();
		Items = new ObservableCollection<ImageProcessItem>();
		bool initial = false;
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!initial)
			{
				if (Param != null)
				{
					GetData();
					if (Root != null)
					{
						Tab tab = Root.Tabs.OfType<Tab>().FirstOrDefault((Tab p) => p.NameA == "运行参数" || p.NameA == "Tab_Run Params");
						if (tab != null)
						{
							Operators = (from p in tab.Categorys.OfType<Category>()
								where !(p is CategoryNoHead)
								select AppLocalizationService.GetLang(p.Name)).ToArray();
						}
					}
				}
				WeakEventManager<ObservableCollection<ImageProcessItem>, NotifyCollectionChangedEventArgs>.AddHandler(Items, "CollectionChanged", OnCollectionChanged);
				initial = true;
			}
		};
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public ImageProcessCategory Clone(string categoryname)
	{
		if (Param == null)
		{
			return null;
		}
		if (Root == null)
		{
			return null;
		}
		Tab tab = Root.Tabs.OfType<Tab>().FirstOrDefault((Tab p) => p.NameA == "运行参数" || p.NameA == "Tab_Run Params");
		if (tab == null)
		{
			return null;
		}
		ImageProcessCategory imageProcessCategory = tab.Categorys.OfType<ImageProcessCategory>().FirstOrDefault((ImageProcessCategory p) => AppLocalizationService.GetLang(p.Name) == categoryname || p.Name == categoryname);
		if (imageProcessCategory != null)
		{
			ImageProcessCategory imageProcessCategory2 = imageProcessCategory.Clone<ImageProcessCategory>();
			if (imageProcessCategory2 != null)
			{
				imageProcessCategory2.SetParentNode(imageProcessCategory.GetParentNode());
				foreach (NameValueItem item in imageProcessCategory2.ItemsA.OfType<NameValueItem>())
				{
					item.XmlLoadedTask();
					item.NotifyTrigger();
				}
			}
			return imageProcessCategory2;
		}
		return null;
	}

	public NameValueItem GetNameValueItem(Category category, int algIndex)
	{
		return ((ImageProcessCategory)category).GetNameValueItems().FirstOrDefault((NameValueItem p) => Convert.ToInt32(p.AlgorithmIndex, 16) == algIndex);
	}

	public ImageProcessCategory CreatCategory(IMAGEPROCESS_SINGLE_INFO pImageprocessSingleInfo)
	{
		if (Param == null)
		{
			return null;
		}
		if (Root == null)
		{
			return null;
		}
		Tab tab = Root.Tabs.OfType<Tab>().FirstOrDefault((Tab p) => p.NameA == "运行参数" || p.NameA == "Tab_Run Params");
		ImageProcessCategory imageProcessCategory = null;
		if (tab == null)
		{
			return null;
		}
		int type = pImageprocessSingleInfo.nTypeIndex;
		ImageProcessCategory imageProcessCategory2 = tab.Categorys.OfType<ImageProcessCategory>().FirstOrDefault((ImageProcessCategory p) => p.TypeIndex == type);
		if (imageProcessCategory2 != null)
		{
			ImageProcessCategory imageProcessCategory3 = imageProcessCategory2.Clone<ImageProcessCategory>();
			if (imageProcessCategory3 != null)
			{
				imageProcessCategory3.SetParentNode(imageProcessCategory2.GetParentNode());
				foreach (NameValueItem item in imageProcessCategory3.ItemsA.OfType<NameValueItem>())
				{
					item.XmlLoadedTask();
					item.NotifyTrigger();
				}
				imageProcessCategory = imageProcessCategory3;
			}
		}
		if (imageProcessCategory == null)
		{
			return null;
		}
		for (int num = 0; num < pImageprocessSingleInfo.nParamCount; num++)
		{
			IMAGEPROCESS_PARAM_INFO iMAGEPROCESS_PARAM_INFO = pImageprocessSingleInfo.stParamInfo[num];
			NameValueItem nameValueItem = GetNameValueItem(imageProcessCategory, iMAGEPROCESS_PARAM_INFO.nAlgorithmIndex);
			if (nameValueItem != null)
			{
				if (nameValueItem is Integer)
				{
					(nameValueItem as Integer).CurValueA = int.Parse(iMAGEPROCESS_PARAM_INFO.chszValue);
				}
				else if (nameValueItem is Float)
				{
					(nameValueItem as Float).CurValueA = double.Parse(iMAGEPROCESS_PARAM_INFO.chszValue);
				}
				else if (nameValueItem is Enumeration)
				{
					(nameValueItem as Enumeration).CurValueA = int.Parse(iMAGEPROCESS_PARAM_INFO.chszValue);
				}
			}
		}
		return imageProcessCategory;
	}

	private void ImageProcessItemChanged(object sender, EventArgs e)
	{
		SetData();
		DoSelectChanged();
		(ParentNode as IRunService)?.PreExecuteRun();
	}

	private void GetData()
	{
		if (Param == null)
		{
			return;
		}
		IntPtr intPtr = IntPtr.Zero;
		byte[] array = ((IUserBytesData)Param)["ImageProcessInfo"];
		if (array != null && array.Length != 0)
		{
			intPtr = Marshal.AllocHGlobal(array.Length);
			Marshal.Copy(array, 0, intPtr, array.Length);
			IMAGEPROCESS_COMBINATION_INFO iMAGEPROCESS_COMBINATION_INFO = Marshal.PtrToStructure<IMAGEPROCESS_COMBINATION_INFO>(intPtr);
			for (int i = 0; i < iMAGEPROCESS_COMBINATION_INFO.nProcessCount; i++)
			{
				IMAGEPROCESS_SINGLE_INFO pImageprocessSingleInfo = iMAGEPROCESS_COMBINATION_INFO.stProcessInfo[i];
				ImageProcessCategory imageProcessCategory = CreatCategory(pImageprocessSingleInfo);
				if (imageProcessCategory != null)
				{
					ImageProcessItem imageProcessItem = new ImageProcessItem();
					imageProcessItem.Name = imageProcessCategory.Name;
					imageProcessItem.Enable = pImageprocessSingleInfo.bEnabled;
					imageProcessItem.Init(imageProcessCategory, Items);
					WeakEventManager<ImageProcessItem, EventArgs>.AddHandler(imageProcessItem, "ValueChanged", ImageProcessItemChanged);
				}
			}
		}
		if (intPtr != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	private void SetData()
	{
		if (Param != null)
		{
			IMAGEPROCESS_SINGLE_INFO[] array = Items.Select((ImageProcessItem p) => p.IMAGEPROCESS_SINGLE_INFO).ToArray();
			IMAGEPROCESS_COMBINATION_INFO structure = new IMAGEPROCESS_COMBINATION_INFO
			{
				nProcessCount = array.Length,
				stProcessInfo = new IMAGEPROCESS_SINGLE_INFO[32]
			};
			Array.Copy(array, structure.stProcessInfo, array.Length);
			IntPtr intPtr = Marshal.AllocHGlobal(_dataLen);
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: false);
			byte[] array2 = new byte[_dataLen];
			Marshal.Copy(intPtr, array2, 0, _dataLen);
			((IUserBytesData)Param)["ImageProcessInfo"] = array2;
			Marshal.FreeHGlobal(intPtr);
			intPtr = IntPtr.Zero;
		}
	}

	private void AddNew_OnClick(object sender, RoutedEventArgs e)
	{
		ToggleButton.IsChecked = false;
		if (sender is Button { DataContext: not null } button)
		{
			string categoryname = button.DataContext.ToString();
			ImageProcessCategory imageProcessCategory = Clone(categoryname);
			if (imageProcessCategory != null)
			{
				ImageProcessItem imageProcessItem = new ImageProcessItem();
				imageProcessItem.Name = imageProcessCategory.Name;
				imageProcessItem.Init(imageProcessCategory, Items);
				WeakEventManager<ImageProcessItem, EventArgs>.AddHandler(imageProcessItem, "ValueChanged", ImageProcessItemChanged);
			}
		}
	}

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		SetData();
		if (e.Action != NotifyCollectionChangedAction.Add)
		{
			DoSelectChanged();
		}
	}

	private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		DoSelectChanged();
	}

	private void Selector_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
	{
		ListBox.SelectedIndex = -1;
	}

	private void DoSelectChanged()
	{
		if (Param == null)
		{
			return;
		}
		try
		{
			((IUserStringData)Param)["ProcedureImageIndex"] = ListBox.SelectedIndex.ToString();
		}
		catch
		{
		}
	}
}
