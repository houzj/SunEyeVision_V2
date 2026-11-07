using IMVSGrayMatchModuControl;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class GrayMatchConfigPanalData : GrayMatchVAConfigPanalData
{
	public override string DisplayType
	{
		get
		{
			return ((object)this).GetType().BaseType.BaseType.Name;
		}
		set
		{
		}
	}

	protected override string OpenFileDialogFilter => "(*.gmxml)|*.gmxml";

	protected override HPFeaturepatMatchDialogUI GetFeaturepatMatchDialogUI()
	{
		return (HPFeaturepatMatchDialogUI)(object)new GrayMatchDialogUI();
	}

	protected override FeaturepatMatchDialogDataBase GetModelData()
	{
		return (FeaturepatMatchDialogDataBase)(object)new GrayMatchDialogData();
	}

	protected override string GetOldModelRootNodeName()
	{
		return "Apps.XmlParser.ParamTab.GrayMatchDialogData";
	}
}
