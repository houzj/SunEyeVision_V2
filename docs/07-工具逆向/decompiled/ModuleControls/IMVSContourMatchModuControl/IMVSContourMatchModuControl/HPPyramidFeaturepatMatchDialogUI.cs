using System.Windows;
using Apps.XmlParser.ParamTab;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace IMVSContourMatchModuControl;

public class HPPyramidFeaturepatMatchDialogUI : HPFeaturepatMatchDialogUI
{
	protected override string FilterName => "xml files(*.contourmxml)|*.contourmxml";

	static HPPyramidFeaturepatMatchDialogUI()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HPPyramidFeaturepatMatchDialogUI), new FrameworkPropertyMetadata(typeof(HPPyramidFeaturepatMatchDialogUI)));
	}

	protected override void CustomInitSign()
	{
	}

	protected override void CustomRefreshByModelData()
	{
	}

	protected override HPFeaturepatMatchDialogData GetFeaturepatMatchDialogData()
	{
		return (HPFeaturepatMatchDialogData)(object)new HPPyramidFeaturepatMatchDialogData();
	}

	protected override SDKFeatureMatchUIData GetUIData()
	{
		return (SDKFeatureMatchUIData)(object)new SDKPyramidFeatureMatchUIData();
	}

	protected override int CreatePatE(HPFeaturepatMatchDialogData featurepatMatchData, dynamic dynamicObject)
	{
		return dynamicObject.CreateDipolePyramid(featurepatMatchData);
	}
}
