using IMVSGrayMatchModuVAControl;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class GrayMatchVAConfigPanalData : FeaturepatMatchConfigPanalData
{
	public override string DisplayType
	{
		get
		{
			return ((object)this).GetType().BaseType.Name;
		}
		set
		{
		}
	}

	protected override string OpenFileDialogFilter => "(*.gmvaxml)|*.gmvaxml";

	protected override FeaturepatMatchDialogDataBase GetModelData()
	{
		return (FeaturepatMatchDialogDataBase)(object)new GrayMatchVADialogData();
	}

	protected override HPFeaturepatMatchDialogUI GetFeaturepatMatchDialogUI()
	{
		return (HPFeaturepatMatchDialogUI)(object)new GrayMatchVADialogUI();
	}

	protected override string GetOldModelRootNodeName()
	{
		return "Apps.XmlParser.ParamTab.GrayMatchVADialogData";
	}
}
