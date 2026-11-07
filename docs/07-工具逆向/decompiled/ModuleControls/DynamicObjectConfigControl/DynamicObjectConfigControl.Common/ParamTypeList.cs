using System.Collections.Generic;
using System.Linq;
using Apps.XmlParser.ParamTab;

namespace DynamicObjectConfigControl.Common;

public class ParamTypeList
{
	internal static GroupRunParamSelectableTypeItem DefaultItem;

	internal static List<GroupRunParamSelectableTypeItem> SelectableList;

	static ParamTypeList()
	{
		DefaultItem = new GroupRunParamSelectableTypeItem
		{
			Name = "All"
		};
		SelectableList = new List<GroupRunParamSelectableTypeItem>
		{
			new GroupRunParamSelectableTypeItem
			{
				Name = "numerical_value",
				TypeList = new List<string> { "Integer", "Float", "IntegerBettween", "FloatBettween", "IntegerBettween_CircleFind", "Integer_CircleFind" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Enum",
				TypeList = new List<string> { "Enumeration" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "on_off",
				TypeList = new List<string> { "Boolean", "BooleanTips" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Single_Select",
				TypeList = new List<string> { "RadioSelecter", "RadioSelecterEx2" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Lang_ROISet",
				TypeList = new List<string> { "ROISelecter", "ROISelecter_Extern" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "File_Path",
				TypeList = new List<string> { "OpenFileDialog", "OpenFileForCNNDialog", "OpenFileForCNNUSDialog", "OpenFileForCalibDialog", "OpenFileForCameraDialog", "OpenFileForDataShellDialog", "OpenFolderDialog", "OpenFileDialog4ReadFile" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Train Pattern Settings",
				TypeList = new List<string> { "FeaturepatMatchConfigPanalData", "FastFeaturepatMatchConfigPanalData", "PyramidFeaturepatMatchConfigPanalData", "GrayMatchVAConfigPanalData", "GrayMatchConfigPanalData", "PcdCutOutConfigPanelData", "RangeRegistMatchConfigPanalData", "CloudModelMatchConfigPanelData" }
			}
		};
		DefaultItem.TypeList = SelectableList.SelectMany((GroupRunParamSelectableTypeItem p) => p.TypeList).ToList();
		SelectableList.Insert(0, DefaultItem);
	}
}
