using IMVSContourMatchModuControl;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class PyramidFeaturepatMatchConfigPanalData : FeaturepatMatchConfigPanalData
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

	protected override string OpenFileDialogFilter => "(*.contourmxml)|*.contourmxml";

	protected override FeaturepatMatchDialogDataBase GetModelData()
	{
		return (FeaturepatMatchDialogDataBase)(object)new HPPyramidFeaturepatMatchDialogData();
	}

	protected override HPFeaturepatMatchDialogUI GetFeaturepatMatchDialogUI()
	{
		return (HPFeaturepatMatchDialogUI)(object)new HPPyramidFeaturepatMatchDialogUI();
	}

	protected override string GetOldModelRootNodeName()
	{
		return "Apps.XmlParser.ParamTab.HPPyramidFeaturepatMatchDialogData";
	}
}
