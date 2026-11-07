using Apps.XmlParser.DateForDisplayInGroup;
using Apps.XmlParser.ParamTab;
using VMControls.Interface;

namespace DynamicObjectConfigControl.XmlData.Extern;

public static class DynamicExtern
{
	public static IDecorate GetSupportDisplayForGroupData(this IGroupRunParam param)
	{
		if (param is ROISelecter decorateSource)
		{
			return new ROISelecterForGroupDecorate
			{
				DecorateSource = decorateSource
			};
		}
		return null;
	}
}
