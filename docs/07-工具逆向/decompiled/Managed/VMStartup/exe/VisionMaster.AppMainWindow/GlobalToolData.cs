using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Data;
using VM.Utility;
using VMControls.Interface;

namespace VisionMaster.AppMainWindow;

public class GlobalToolData : NotifyPropertyChangedBaseNode, IDecorate
{
	private TitleMenueItemData toolControlItemData;

	private object toolSource;

	public object DecorateSource
	{
		get
		{
			return GetItems(ref toolSource);
		}
		set
		{
			throw new NotImplementedException();
		}
	}

	private object GetItems(ref object source)
	{
		if (source == null)
		{
			LoadData();
		}
		if (source == null)
		{
			List<IGrouping<string, TitleMenueItemData>> list = (from p in toolControlItemData.GetAllChildren().OfType<TitleMenueItemData>()
				where p.ToolGroupItemInfo != null
				group p by p.ToolGroupItemInfo.groupName).ToList();
			list.Sort((IGrouping<string, TitleMenueItemData> p1, IGrouping<string, TitleMenueItemData> p2) => string.Compare(p1.Key, p2.Key));
			List<List<TitleMenueItemData>> list2 = list.Select((IGrouping<string, TitleMenueItemData> p) => p.ToList()).ToList();
			list2.ForEach((List<TitleMenueItemData> p) =>
			{
				p.Sort((TitleMenueItemData titleMenueItemData, TitleMenueItemData titleMenueItemData2) => (titleMenueItemData.ToolGroupItemInfo.Priority > titleMenueItemData2.ToolGroupItemInfo.Priority) ? 1 : (-1));
			});
			List<TitleMenueItemData> source2 = list2.SelectMany((List<TitleMenueItemData> p) => p.ToArray()).ToList();
			source = source2.Where((TitleMenueItemData p) => p.IsShowExternal).ToList();
		}
		return source;
	}

	private void LoadData()
	{
		toolControlItemData = new TitleMenueItemData();
		string config = ConfigurationHelper.GetConfig("ServerSetting");
		if (config != null && config.Contains("127.0.0.1"))
		{
			toolControlItemData.loadXml("Resources//MenuItems.xml");
		}
		else
		{
			toolControlItemData.loadXml("Resources//MenuItemsLinux.xml");
		}
	}
}
