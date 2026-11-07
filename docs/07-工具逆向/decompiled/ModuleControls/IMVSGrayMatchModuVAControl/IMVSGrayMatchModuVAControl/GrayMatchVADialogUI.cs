using System.Windows;
using Apps.XmlParser.ParamTab;
using IMVSHPFeatureMatchModuControl;
using VM.ModuleControl;

namespace IMVSGrayMatchModuVAControl;

public class GrayMatchVADialogUI : HPFeaturepatMatchDialogUI
{
	protected override string FilterName => "xml files(*.gmvaxml)|*.gmvaxml";

	static GrayMatchVADialogUI()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(GrayMatchVADialogUI), new FrameworkPropertyMetadata(typeof(GrayMatchVADialogUI)));
	}

	protected override void CustomInitSign()
	{
	}

	protected override HPFeaturepatMatchDialogData GetFeaturepatMatchDialogData()
	{
		return (HPFeaturepatMatchDialogData)(object)new GrayMatchVADialogData();
	}

	public override void RefreshByModelData()
	{
		((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).NotifyPropertyChanged("ItemsData");
		((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).NotifyPropertyChanged("FixPointX");
		((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).NotifyPropertyChanged("FixPointY");
		((HPFeaturepatMatchDialogUI)this).CurImage = ((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).CurImageDataA;
		((HPFeaturepatMatchDialogUI)this).ShowImage();
		((HPFeaturepatMatchDialogUI)this).FreshOtherMask();
		((HPFeaturepatMatchDialogUI)this).RefreshPoints();
	}

	protected override int CreatePatE(HPFeaturepatMatchDialogData featurepatMatchData, dynamic dynamicObject)
	{
		return dynamicObject.CreateGrayPatEVA(featurepatMatchData);
	}
}
