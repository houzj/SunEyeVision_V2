using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Apps.Data.ShapeDefine;
using Apps.Interface;
using Apps.Localization;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;

namespace IMVSColorRecognitionModuControl;

public partial class HikHsiList : UserControl, ISpecialParam, IComponentConnector
{
	private AlgorithmTabRoot _Root;

	public ObservableCollection<HsiData> ModuleResultInfoList { get; set; } = new ObservableCollection<HsiData>();

	private AlgorithmTabRoot Root => _Root;

	public string TabItemName => "CurResultTabItem";

	public HikHsiList()
	{
		InitializeComponent();
		InitList();
	}

	public HikHsiList(AlgorithmTabRoot data)
	{
		_Root = data;
		InitializeComponent();
		InitList();
	}

	private void DataGrid_LayoutUpdated(object sender, EventArgs e)
	{
		m_DataGrid.RowHeight = double.NaN;
	}

	private void InitList()
	{
		HsiData hsiData = new HsiData();
		hsiData.Name = AppLocalizationService.GetLang("模型区域");
		HsiData hsiData2 = new HsiData();
		hsiData2.Name = AppLocalizationService.GetLang("当前区域");
		ModuleResultInfoList.Add(hsiData);
		ModuleResultInfoList.Add(hsiData2);
	}

	public void InitFunction()
	{
	}

	public void DisposeFunction()
	{
	}

	public void RenderFunction(Dictionary<string, object> keyValues)
	{
		if (keyValues == null || keyValues["Features"] == null)
		{
			return;
		}
		List<object> list = keyValues["Shapes"] as List<object>;
		if (list.Count == 0)
		{
			return;
		}
		IList<FeatureBase> list2 = keyValues["Features"] as IList<FeatureBase>;
		foreach (FeatureBase item in list2)
		{
			PointCollection pointCollection = new PointCollection();
			int valueCount = item.GetValueCount();
			if (item.Name == "ModelHData")
			{
				for (int i = 0; i < valueCount; i++)
				{
					pointCollection.Add(new System.Windows.Point(i, (list[i] as HsiList).ModelHData));
				}
				ModuleResultInfoList[0].HData = pointCollection;
			}
			else if (item.Name == "ModelSData")
			{
				for (int j = 0; j < valueCount; j++)
				{
					pointCollection.Add(new System.Windows.Point(j, (list[j] as HsiList).ModelSData));
				}
				ModuleResultInfoList[0].SData = pointCollection;
			}
			else if (item.Name == "ModelIData")
			{
				for (int k = 0; k < valueCount; k++)
				{
					pointCollection.Add(new System.Windows.Point(k, (list[k] as HsiList).ModelIData));
				}
				ModuleResultInfoList[0].IData = pointCollection;
			}
			else if (item.Name == "SampleHData")
			{
				for (int l = 0; l < valueCount; l++)
				{
					pointCollection.Add(new System.Windows.Point(l, (list[l] as HsiList).SampleHData));
				}
				ModuleResultInfoList[1].HData = pointCollection;
			}
			else if (item.Name == "SampleSData")
			{
				for (int m = 0; m < valueCount; m++)
				{
					pointCollection.Add(new System.Windows.Point(m, (list[m] as HsiList).SampleSData));
				}
				ModuleResultInfoList[1].SData = pointCollection;
			}
			else if (item.Name == "SampleIData")
			{
				for (int n = 0; n < valueCount; n++)
				{
					pointCollection.Add(new System.Windows.Point(n, (list[n] as HsiList).SampleIData));
				}
				ModuleResultInfoList[1].IData = pointCollection;
			}
		}
	}
}
