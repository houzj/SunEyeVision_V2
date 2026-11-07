using IMVSFastFeatureMatchModuControl;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class FastFeaturepatMatchConfigPanalData : FeaturepatMatchConfigPanalData
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

	protected override string OpenFileDialogFilter => "(*.fmxml)|*.fmxml";

	protected override FeaturepatMatchDialogDataBase GetModelData()
	{
		return (FeaturepatMatchDialogDataBase)(object)new FastFeaturepatMatchDialogData();
	}

	protected override HPFeaturepatMatchDialogUI GetFeaturepatMatchDialogUI()
	{
		return (HPFeaturepatMatchDialogUI)(object)new FastFeaturepatMatchDialogUI();
	}

	protected override string GetOldModelRootNodeName()
	{
		return "Apps.XmlParser.ParamTab.FastFeaturepatMatchDialogData";
	}
}
