using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Apps.Localization;
using Frontend.Base;
using VM.Controls;
using VM.Core;

namespace VisionMasterMain;

public partial class AccessModuleManager : BaseWindow, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private List<CheckBox> _groupCheckBoxs = new List<CheckBox>();

	public static byte[] GlobalModuleAccess = new byte[10] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

	public const string AccessModuleManagerKey = "AccessModuleManager";

	private bool _update = false;

	public List<ModuleTagInfoProxy> Items { get; set; }

	public new event PropertyChangedEventHandler PropertyChanged;

	public AccessModuleManager()
	{
		InitializeComponent();
		List<VmModule> list = new List<VmModule>();
		foreach (VmProcedure item in VmSolution.Instance.Modules.OfType<VmProcedure>())
		{
			List<VmModule> list2 = new List<VmModule>();
			item.GetAllModule(list2);
			list.AddRange(list2);
		}
		List<ModuleTagInfoProxy> items = (from p in list
			orderby p.ID
			select new ModuleTagInfoProxy(p)
			{
				Group = FindProcessName(p)
			}).ToList();
		Items = items;
		List<VmModule> list3 = VmSolution.Instance.Modules.Where((VmModule w) => w.IsGlobalModu).ToList();
		foreach (VmModule item2 in list3)
		{
			Items.Add(new ModuleTagInfoProxy(item2)
			{
				Group = FindResoure("Lang_AccessModuleManagerGlobalTool"),
				IsGlobal = true
			});
		}
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs("Items"));
		}
		Loaded += (object s, RoutedEventArgs e) =>
		{
			UpdateGroup();
		};
	}

	public string FindProcessName(VmModule m)
	{
		object parent = m.Parent;
		return (parent == null) ? string.Empty : (parent as VmModule).FullName;
	}

	public string FindResoure(string strMsg)
	{
		return AppLocalizationService.GetLang(strMsg);
	}

	private void CheckBox_Checked(object sender, RoutedEventArgs e)
	{
		if (_update)
		{
			return;
		}
		foreach (ModuleTagInfoProxy item in Items)
		{
			item.ConfigAvailable = true;
		}
		UpdateGroup();
	}

	private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_update)
		{
			return;
		}
		foreach (ModuleTagInfoProxy item in Items)
		{
			item.ConfigAvailable = false;
		}
		UpdateGroup();
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		int num = 0;
		foreach (ModuleTagInfoProxy item in Items)
		{
			item.ModuleTagInfo.ConfigAvailable = item.ConfigAvailable;
		}
		Close();
	}

	private void Group_OnChecked(object sender, RoutedEventArgs e)
	{
		GroupChecked(sender, check: true);
	}

	private void GroupChecked(object sender, bool check)
	{
		if (_update || !(sender is CheckBox { DataContext: CollectionViewGroup dataContext }))
		{
			return;
		}
		foreach (ModuleTagInfoProxy item in dataContext.Items.OfType<ModuleTagInfoProxy>())
		{
			item.ConfigAvailable = check;
		}
	}

	private void Group_OnUnchecked(object sender, RoutedEventArgs e)
	{
		GroupChecked(sender, check: false);
	}

	private void FrameworkElement_OnInitialized(object sender, EventArgs e)
	{
		if (sender is CheckBox item && !_groupCheckBoxs.Contains(item))
		{
			_groupCheckBoxs.Add(item);
		}
	}

	private void UpdateGroup()
	{
		_update = true;
		IEnumerable<IGrouping<string, ModuleTagInfoProxy>> enumerable = from p in Items
			group p by p.Group;
		foreach (IGrouping<string, ModuleTagInfoProxy> gp in enumerable)
		{
			CheckBox checkBox = _groupCheckBoxs.First((CheckBox p) => p.ToolTip != null && p.ToolTip.ToString() == gp.Key);
			if (checkBox != null)
			{
				checkBox.IsChecked = !gp.Any((ModuleTagInfoProxy p) => !p.ConfigAvailable);
			}
		}
		all.IsChecked = _groupCheckBoxs.All((CheckBox p) => p.IsChecked.HasValue && p.IsChecked.Value);
		_update = false;
	}

	private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
	{
		UpdateGroup();
	}
}
