using System.Windows;
using Apps.XmlParser.ParamTab;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace IMVSFastFeatureMatchModuControl;

public class FastFeaturepatMatchDialogUI : HPFeaturepatMatchDialogUI
{
	protected override string FilterName => "xml files(*.fmxml)|*.fmxml";

	static FastFeaturepatMatchDialogUI()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(FastFeaturepatMatchDialogUI), new FrameworkPropertyMetadata(typeof(FastFeaturepatMatchDialogUI)));
	}

	protected override void CustomInitSign()
	{
	}

	protected override void CustomRefreshByModelData()
	{
	}

	protected override HPFeaturepatMatchDialogData GetFeaturepatMatchDialogData()
	{
		return (HPFeaturepatMatchDialogData)(object)new FastFeaturepatMatchDialogData();
	}

	protected override SDKFeatureMatchUIData GetUIData()
	{
		return (SDKFeatureMatchUIData)(object)new SDKFastFeatureMatchUIData();
	}

	protected override int CreatePatE(HPFeaturepatMatchDialogData featurepatMatchData, dynamic dynamicObject)
	{
		return dynamicObject.CreateFastPatE(featurepatMatchData);
	}
}
