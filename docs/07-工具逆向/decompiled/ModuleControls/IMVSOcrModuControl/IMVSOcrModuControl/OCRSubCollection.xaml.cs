using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;

namespace IMVSOcrModuControl;

public partial class OCRSubCollection : BaseWindow, INotifyPropertyChanged, IComponentConnector
{
	public OCRTrainUI parentControl;

	private int _SelectedItemsCount;

	public int SelectedItemsCount
	{
		get
		{
			return _SelectedItemsCount;
		}
		set
		{
			if (_SelectedItemsCount != value)
			{
				_SelectedItemsCount = value;
				NotifyPropertyChanged("SelectedItemsCount");
			}
		}
	}

	public new event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public OCRSubCollection()
	{
		InitializeComponent();
	}

	private void Button_Click_Enhance(object sender, RoutedEventArgs e)
	{
		try
		{
			if (parentControl == null || LCon.SelectedItems == null)
			{
				return;
			}
			object[] array = new object[LCon.SelectedItems.Count];
			LCon.SelectedItems.CopyTo(array, 0);
			int num = 0;
			object[] array2 = array;
			foreach (object obj in array2)
			{
				num++;
				if (num > OCRTrainUI.MaxEnhanceCount)
				{
					break;
				}
				parentControl.EnhanceSelectedItem(obj, AddToParent: true);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(Owner, AppLocalizationService.GetLang("OcR"), ex.Message, MessageIcons.Error);
		}
	}

	private void Button_Click_Clear(object sender, RoutedEventArgs e)
	{
		DeleteSelectedItem();
	}

	private void LCon_KeyDown(object sender, KeyEventArgs e)
	{
		if (Key.Delete == e.Key)
		{
			DeleteSelectedItem();
		}
	}

	private void DeleteSelectedItem()
	{
		if (LCon.SelectedItems != null && LCon.SelectedItems.Count > 0)
		{
			object[] array = new object[LCon.SelectedItems.Count];
			LCon.SelectedItems.CopyTo(array, 0);
			object[] array2 = array;
			foreach (object item in array2)
			{
				DeleteSelectedItem(item);
			}
		}
	}

	private void DeleteSelectedItem(object item)
	{
		if (parentControl == null)
		{
			return;
		}
		CharacterItemData characterItemData = DataContext as CharacterItemData;
		if (!characterItemData.Collection.Contains(item))
		{
			return;
		}
		CharacterItemData characterItemData2 = item as CharacterItemData;
		char c = characterItemData2.Character_Tilte.Split(',')[0][0];
		int num = int.Parse(characterItemData2.Character_Tilte.Split(',')[1]);
		for (int i = num; i < characterItemData.Collection.Count; i++)
		{
			CharacterItemData characterItemData3 = characterItemData.Collection[i];
			if (characterItemData3.Character_Tilte.Split(',')[0][0] == c)
			{
				characterItemData3.Character_Tilte = $"{c},{num:d}";
				num++;
				continue;
			}
			break;
		}
		uint fontID = OCRTrainUI.GetFontID(characterItemData2.ChFontLabel);
		parentControl.nFontTranNum[fontID]--;
		characterItemData.Collection.Remove(characterItemData2);
	}

	private void LCon_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is ListView { SelectedItems: not null } listView)
		{
			SelectedItemsCount = listView.SelectedItems.Count;
		}
	}
}
