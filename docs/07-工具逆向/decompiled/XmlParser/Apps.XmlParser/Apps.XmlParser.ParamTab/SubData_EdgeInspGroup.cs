namespace Apps.XmlParser.ParamTab;

public class SubData_EdgeInspGroup : SubData
{
	private bool _IsOpenDetail;

	private Tab _SourceTab;

	public SubModuleInfo info { get; set; }

	public bool IsOpenDetail
	{
		get
		{
			return _IsOpenDetail;
		}
		set
		{
			if (_IsOpenDetail != value)
			{
				_IsOpenDetail = value;
				OnPropertyChanged("IsOpenDetail");
			}
		}
	}

	public Tab SourceTab
	{
		get
		{
			return _SourceTab;
		}
		set
		{
			if (_SourceTab != value)
			{
				if (value == null && _SourceTab != null)
				{
					_SourceTab.DoFinish();
				}
				_SourceTab = value;
				OnPropertyChanged("SourceTab");
			}
		}
	}

	protected override void DeleteCommandCmdExecute()
	{
		base.DeleteCommandCmdExecute();
		AddList_EdgeInspGroup addList_EdgeInspGroup = GetParentNode() as AddList_EdgeInspGroup;
		foreach (ROISelecter rOISelecter in info.AlgorithmRoot.ROISelecters)
		{
			rOISelecter.NormalizeROIList.Clear();
		}
		if (addList_EdgeInspGroup.SetParamUnit("Delete:" + info.HashCode) == 0)
		{
			addList_EdgeInspGroup.DeleteIo(info.HashCode);
		}
	}

	public void SetListSource(SubModuleInfo source, int index)
	{
		if (source == null)
		{
			SourceTab = null;
		}
		else if (source.AlgorithmItems != null && source.AlgorithmItems.Tabs != null)
		{
			if (source.AlgorithmItems.Tabs.Count > index)
			{
				SourceTab = source.AlgorithmItems.Tabs[index] as Tab;
			}
			else
			{
				SourceTab = null;
			}
		}
	}
}
