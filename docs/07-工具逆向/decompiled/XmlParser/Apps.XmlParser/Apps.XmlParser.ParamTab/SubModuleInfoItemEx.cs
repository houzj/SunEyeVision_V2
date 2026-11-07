namespace Apps.XmlParser.ParamTab;

public class SubModuleInfoItemEx : SubModuleInfoItem
{
	protected override void DoClick(object obj, SubModuleInfo source, int index)
	{
		if (!(obj is bool) || !(GetParentNode() is SubData_EdgeInspGroup subData_EdgeInspGroup))
		{
			return;
		}
		subData_EdgeInspGroup.IsOpenDetail = (bool)obj;
		if (subData_EdgeInspGroup.IsOpenDetail)
		{
			AddList_ImageStitch addList_ImageStitch = GetParentNode<AddList_ImageStitch>();
			try
			{
				foreach (SubData_EdgeInspGroup item in addList_ImageStitch.EnumEntrysA)
				{
					if (item != subData_EdgeInspGroup && item.IsOpenDetail)
					{
						item.IsOpenDetail = false;
						(item.EnumEntrysA[1] as SubModuleInfoItemEx).Checked_Btn1 = false;
						item.SetListSource(null, index);
					}
				}
			}
			catch
			{
			}
			subData_EdgeInspGroup.SetListSource(source, index);
		}
		else
		{
			subData_EdgeInspGroup.SetListSource(null, index);
		}
	}
}
