using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Apps.BaseData;
using Apps.XmlParser.ItemInterface;
using Hik.Xml;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class AddList_MultiSubModel : AddList, IROISelecter
{
	private IROI _CurROIData;

	private int curImageWidth;

	private int curImageHeight;

	private bool _IsStateForDrawROI;

	private Type _DrawROIType;

	public List<SubModuleInfo> AddItemTemplates;

	private SubData_EdgeInspGroup _CurItem;

	private ROISelecter CurSelectedROI = null;

	public IROI CurROIData
	{
		get
		{
			return _CurROIData;
		}
		set
		{
			if (_CurROIData != value)
			{
				_CurROIData = value;
				OnPropertyChanged("CurROIData");
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
			if (curImageWidth != value)
			{
				curImageWidth = value;
				OnPropertyChanged("CurImageWidth");
				if (CurSelectedROI != null)
				{
					CurSelectedROI.CurImageWidth = value;
				}
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
			if (curImageHeight != value)
			{
				curImageHeight = value;
				OnPropertyChanged("CurImageHeight");
				if (CurSelectedROI != null)
				{
					CurSelectedROI.CurImageHeight = value;
				}
			}
		}
	}

	public bool IsStateForDrawROI
	{
		get
		{
			return _IsStateForDrawROI;
		}
		set
		{
			if (_IsStateForDrawROI == value)
			{
				return;
			}
			_IsStateForDrawROI = value;
			OnPropertyChanged("IsStateForDrawROI");
			if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
			{
				return;
			}
			if (value)
			{
				algorithmTabRoot.ROISelecters.ForEach((IROISelecter item) =>
				{
					if (item != this)
					{
						item.IsStateForDrawROI = false;
					}
				});
				algorithmTabRoot.SelectedROI = this;
			}
			else if (algorithmTabRoot.SelectedROI == this)
			{
				algorithmTabRoot.SelectedROI = null;
			}
		}
	}

	public Type DrawROIType
	{
		get
		{
			return _DrawROIType;
		}
		set
		{
			if (_DrawROIType != value)
			{
				_DrawROIType = value;
				OnPropertyChanged("DrawROIType");
			}
		}
	}

	bool IROISelecter.IsStateForDrawROI
	{
		get
		{
			return IsStateForDrawROI;
		}
		set
		{
			IsStateForDrawROI = value;
		}
	}

	public ObservableRangeCollection<IROI> ROIList { get; set; } = new ROIObservableRangeCollection<IROI>();

	public ICommand AddROICommand => CurSelectedROI?.AddROICommand;

	public IList Operators => AddItemTemplates;

	public SubData_EdgeInspGroup CurItem
	{
		get
		{
			return _CurItem;
		}
		set
		{
			if (_CurItem == value)
			{
				return;
			}
			if (value == null)
			{
				if (_CurItem != null)
				{
					CloseTab(_CurItem);
				}
				InitROI();
			}
			if (_CurItem != null)
			{
				DelROIAttach(_CurItem);
			}
			_CurItem = value;
			OnPropertyChanged("CurItem");
			if (_CurItem == null)
			{
				return;
			}
			foreach (SubData_EdgeInspGroup item in EnumEntrysA)
			{
				if (item != _CurItem)
				{
					CloseTab(item);
				}
			}
			SetROIAttach(value);
			_CurItem.info.AlgorithmRoot.DoIniter();
			_CurItem.info.AlgorithmRoot.GetItemsValueUnit();
		}
	}

	bool IROISelecter.CanUpdateROI
	{
		get
		{
			IROISelecter curSelectedROI;
			if ((curSelectedROI = CurSelectedROI) != null)
			{
				return curSelectedROI.CanUpdateROI;
			}
			return true;
		}
		set
		{
			IROISelecter curSelectedROI;
			if ((curSelectedROI = CurSelectedROI) != null)
			{
				curSelectedROI.CanUpdateROI = value;
			}
		}
	}

	public AddList_MultiSubModel()
	{
		ROIList.CollectionChanged += ROIList_CollectionChanged1;
	}

	void IROISelecter.SelectNone()
	{
	}

	private void InitROI()
	{
		CurSelectedROI = null;
		IsStateForDrawROI = false;
		DrawROIType = null;
		ROIList.Clear();
	}

	private void DelROIAttach(SubData_EdgeInspGroup item)
	{
		if (CurSelectedROI != null)
		{
			CurSelectedROI.PropertyChanged -= CurSelectedROI_PropertyChanged;
			CurSelectedROI.ROIList.CollectionChanged -= ROIList_CollectionChanged;
			PropertyChangedEventManager.RemoveHandler(CurSelectedROI, CustomVisibleChanged, "CustomVisibleA");
		}
		InitROI();
	}

	private void SetROIAttach(SubData_EdgeInspGroup item)
	{
		((IROISelecter)this).ROIList.Clear();
		IsStateForDrawROI = false;
		BaseNode baseNode = ((item.EnumEntrysA.First() as SubModuleInfoItem).info.AlgorithmRoot.Tabs[0] as Tab).FindBaseNodeByName("RoiType");
		if (baseNode is ROISelecter curSelectedROI)
		{
			CurSelectedROI = curSelectedROI;
			CurSelectedROI.needSetImageSize = false;
			CurSelectedROI.PropertyChanged += CurSelectedROI_PropertyChanged;
			CurSelectedROI.ROIList.CollectionChanged += ROIList_CollectionChanged;
			CurSelectedROI.CurImageWidth = curImageWidth;
			CurSelectedROI.CurImageHeight = curImageHeight;
			CurSelectedROI.IsStateForDrawROI = false;
			CurSelectedROI.DrawROIType = null;
			(ROIList as IROIVisible).IsVisible = CurSelectedROI.CustomVisible;
			PropertyChangedEventManager.AddHandler(CurSelectedROI, CustomVisibleChanged, "CustomVisibleA");
		}
		OnPropertyChanged("AddROICommand");
	}

	private void CustomVisibleChanged(object sender, PropertyChangedEventArgs e)
	{
		(ROIList as IROIVisible).IsVisible = (sender as ROISelecter).CustomVisible;
	}

	private void CurSelectedROI_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (!(propertyName == "IsStateForDrawROI"))
		{
			if (propertyName == "DrawROIType")
			{
				DrawROIType = CurSelectedROI.DrawROIType;
			}
		}
		else
		{
			IsStateForDrawROI = CurSelectedROI.IsStateForDrawROI;
		}
	}

	private void ROIList_CollectionChanged1(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (CurSelectedROI != null && e.Action == NotifyCollectionChangedAction.Add)
		{
			CurSelectedROI.ROIList.CollectionChanged -= ROIList_CollectionChanged;
			CurSelectedROI.ROIList.AddRange(e.NewItems.OfType<IROI>());
			CurSelectedROI.ROIList.CollectionChanged += ROIList_CollectionChanged;
		}
	}

	private void ROIList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
		case NotifyCollectionChangedAction.Add:
			ROIList.CollectionChanged -= ROIList_CollectionChanged1;
			ROIList.AddRange(e.NewItems.OfType<IROI>());
			ROIList.CollectionChanged += ROIList_CollectionChanged1;
			break;
		case NotifyCollectionChangedAction.Remove:
		{
			foreach (object oldItem in e.OldItems)
			{
				ROIList.Remove(oldItem as IROI);
			}
			break;
		}
		case NotifyCollectionChangedAction.Reset:
			ROIList.Clear();
			break;
		case NotifyCollectionChangedAction.Replace:
		case NotifyCollectionChangedAction.Move:
			break;
		}
	}

	protected virtual void CloseTab(SubData_EdgeInspGroup item)
	{
	}

	void IROISelecter.DoRoiSelect(IROI roi)
	{
		IROISelecter curSelectedROI;
		if ((curSelectedROI = CurSelectedROI) != null)
		{
			curSelectedROI.DoRoiSelect(roi);
		}
	}

	void IROISelecter.DoRoiDeleted(IROI roi)
	{
		IROISelecter curSelectedROI;
		if ((curSelectedROI = CurSelectedROI) != null)
		{
			curSelectedROI.DoRoiDeleted(roi);
		}
	}

	void IROISelecter.DoRoiPaste(IROI roi)
	{
		IROISelecter curSelectedROI;
		if ((curSelectedROI = CurSelectedROI) != null)
		{
			curSelectedROI.DoRoiPaste(roi);
		}
	}

	void IROISelecter.UpdateROI()
	{
		IROISelecter curSelectedROI;
		if ((curSelectedROI = CurSelectedROI) != null)
		{
			curSelectedROI.UpdateROI();
		}
	}

	bool IROISelecter.CanDeleted(IROI roi)
	{
		return false;
	}

	bool IROISelecter.CanCopy(IROI roi)
	{
		return false;
	}
}
