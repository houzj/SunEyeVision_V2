using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Interface;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using VM.Controls;
using VM.ModuleControl;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using WPF.JoshSmith.ServiceProviders.UI;

namespace OCRModuleCommonControl;

public partial class MultiROIFilterUI : UserControl, IFixSize, IOwner, IParentNode, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private Size _FixSize = new Size(684.0, 420.0);

	private object ParentNode;

	private AlgorithmTabRoot _Root;

	private MultiFilterData myMultiFilterData = new MultiFilterData();

	private ListViewDragDropManager<OCRFilerItem> dragMgr;

	public const string NewVersionString = "NewVersion";

	public const string OldVersionString = "OldVersion";

	public const int MaxFontNum = 50;

	public const int MinFontNum = 1;

	private const int MaxRoiNum = 64;

	private string Version = "NewVersion";

	public Size FixSize => _FixSize;

	public object Owner { get; set; }

	private AlgorithmTabRoot Root
	{
		get
		{
			if (_Root == null && ParentNode != null && ParentNode is NameValueItem)
			{
				_Root = (ParentNode as NameValueItem).GetTopBaseNode() as AlgorithmTabRoot;
			}
			return _Root;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public object GetParentNode()
	{
		return ParentNode;
	}

	public void SetParentNode(object parentNode)
	{
		ParentNode = parentNode;
	}

	public void OnPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public MultiROIFilterUI()
	{
		InitializeComponent();
		Init();
	}

	public MultiROIFilterUI(string version)
	{
		InitializeComponent();
		Version = version;
		Init();
	}

	public MultiROIFilterUI(string version, string isCustom)
	{
		InitializeComponent();
		Version = version;
		myMultiFilterData.IsROIListCustomizable = isCustom == "True";
		Init();
	}

	private void Init()
	{
		DataContext = myMultiFilterData;
		dragMgr = new ListViewDragDropManager<OCRFilerItem>(listView);
	}

	private void OnListViewDragEnter(object sender, DragEventArgs e)
	{
		e.Effects = DragDropEffects.Move;
	}

	private void OnListViewDrop(object sender, DragEventArgs e)
	{
		if (e.Effects != DragDropEffects.None)
		{
			object data = e.Data.GetData(typeof(OCRFilerItem));
			OCRFilerItem item = (OCRFilerItem)((data is OCRFilerItem) ? data : null);
			if (!dragMgr.IsDragInProgress)
			{
				(listView.ItemsSource as ObservableCollection<OCRFilerItem>).Remove(item);
			}
		}
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		if (myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem != null && sender is Button { Name: var name })
		{
			switch (name)
			{
			default:
				return;
			case "all":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 0;
				break;
			case "num":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 1;
				break;
			case "en":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 2;
				break;
			case "cap":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 3;
				break;
			case "lower":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 4;
				break;
			case "spe":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 5;
				break;
			case "cus":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 6;
				break;
			case "spa":
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData = 7;
				break;
			}
			if (name == "cus")
			{
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.IsConfigPanalDropDownOpen = true;
				PropertyChangedEventManager.AddHandler((INotifyPropertyChanged)myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem, DropDownOpen_PropertyChanged, "IsConfigPanalDropDownOpen");
			}
			else
			{
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.CustomString = "";
				Chosenext();
			}
		}
	}

	private void Chosenext()
	{
		int num = myMultiFilterData.CurSelectItem.OCRFilerUIData.Collection.IndexOf(myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem);
		if (num < myMultiFilterData.CurSelectItem.OCRFilerUIData.Collection.Count - 1)
		{
			myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem = myMultiFilterData.CurSelectItem.OCRFilerUIData.Collection[num + 1];
		}
	}

	private void DropDownOpen_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem != null)
		{
			PropertyChangedEventManager.RemoveHandler((INotifyPropertyChanged)myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem, DropDownOpen_PropertyChanged, "IsConfigPanalDropDownOpen");
			if (!string.IsNullOrEmpty(myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.CustomString))
			{
				Chosenext();
			}
		}
	}

	private void OK_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (SingleFilterData item in myMultiFilterData.FilterDataCollection)
			{
				item.SetParam(Root.UserStringData, Version, myMultiFilterData.IsROIListCustomizable);
				stringBuilder.Append(item.Index.ToString()).Append(" ");
			}
			if (myMultiFilterData.IsROIListCustomizable)
			{
				if (stringBuilder.Length > 1)
				{
					stringBuilder.Remove(stringBuilder.Length - 1, 1);
					((IUserStringData)Root.UserStringData)["CustomRoiTypeSeq"] = stringBuilder.ToString();
				}
				else
				{
					((IUserStringData)Root.UserStringData)["CustomRoiTypeSeq"] = " ";
				}
			}
			((IUserStringData)Root.UserStringData)["GlobalFontFilterEnable"] = (myMultiFilterData.IsSingleCheck ? "1" : "0");
			(Owner as Window).DialogResult = true;
			(ParentNode as IRunService)?.PreExecuteRun();
		}
		catch (Exception ex)
		{
			string strMsg = ex.Message;
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("OcR"), strMsg, MessageIcons.Error);
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		(Owner as Window).Close();
	}

	private void bd_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem != null && myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.FilerData == 6)
			{
				myMultiFilterData.CurSelectItem.OCRFilerUIData.curSelectedItem.IsConfigPanalDropDownOpen = true;
			}
		}));
	}

	private void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
		if (Root == null)
		{
			return;
		}
		try
		{
			string key = (myMultiFilterData.IsROIListCustomizable ? "CustomRoiTypeSeq" : "RoiTypeSeq");
			string text = (myMultiFilterData.IsROIListCustomizable ? "CustomFontFilterEnable-" : "FontFilterEnable-");
			string text2 = (myMultiFilterData.IsROIListCustomizable ? "CustomFontFilterInfo-" : "FontFilterInfo-");
			Integer fontNumber = Root.FindBaseNodeByName("FontFilterNum") as Integer;
			string[] array = ((IUserStringData)Root.UserStringData)[key]?.Split(' ');
			for (int i = 0; i < array.Length; i++)
			{
				int result = 0;
				if (int.TryParse(array[i], out result))
				{
					bool enable = ((IUserStringData)Root.UserStringData)[text + array[i]] == "True";
					string info = ((IUserStringData)Root.UserStringData)[text2 + array[i]];
					AddRoiInfo(enable, info, result, fontNumber);
				}
			}
			if (myMultiFilterData.FilterDataCollection.Count > 0)
			{
				myMultiFilterData.CurSelectItem = myMultiFilterData.FilterDataCollection[0];
			}
			string text3 = ((IUserStringData)Root.UserStringData)["GlobalFontFilterEnable"];
			if (text3 == "-1")
			{
				text3 = (myMultiFilterData.IsROIListCustomizable ? "1" : "0");
				((IUserStringData)Root.UserStringData)["GlobalFontFilterEnable"] = text3;
			}
			myMultiFilterData.IsSingleCheck = text3 == "1";
		}
		catch (Exception ex)
		{
			string strMsg = ex.Message;
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}

	private SingleFilterData AddRoiInfo(bool enable, string info, int num, Integer fontNumber)
	{
		if (info == "")
		{
			info = "0";
		}
		SingleFilterData singleFilterData = new SingleFilterData
		{
			Index = num,
			FontFilterEnable = enable,
			FontFilterInfo = info
		};
		singleFilterData.FontFilterNum.MaxValue = fontNumber?.MaxValue ?? 50;
		singleFilterData.FontFilterNum.MinValue = fontNumber?.MinValue ?? 1;
		singleFilterData.Init(Version);
		myMultiFilterData.FilterDataCollection.Add(singleFilterData);
		return singleFilterData;
	}

	private void Add_Click(object sender, RoutedEventArgs e)
	{
		if (myMultiFilterData.FilterDataCollection.Count >= 64)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamSet"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit"), MessageIcons.Warning);
			return;
		}
		Integer fontNumber = Root.FindBaseNodeByName("FontFilterNum") as Integer;
		SingleFilterData curSelectItem = AddRoiInfo(enable: false, "", myMultiFilterData.FilterDataCollection.Count + 1, fontNumber);
		myMultiFilterData.CurSelectItem = curSelectItem;
	}

	private void SingleButton_Checked(object sender, RoutedEventArgs e)
	{
		if (myMultiFilterData.IsROIListCustomizable && myMultiFilterData.FilterDataCollection.Count == 0)
		{
			Integer fontNumber = Root.FindBaseNodeByName("FontFilterNum") as Integer;
			SingleFilterData singleFilterData = AddRoiInfo(enable: false, "", 1, fontNumber);
		}
		if (myMultiFilterData.FilterDataCollection.Count > 0)
		{
			myMultiFilterData.CurSelectItem = myMultiFilterData.FilterDataCollection[0];
		}
	}
}
