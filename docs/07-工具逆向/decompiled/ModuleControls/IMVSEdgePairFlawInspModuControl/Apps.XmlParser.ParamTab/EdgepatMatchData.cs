using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Xml;
using Apps.Data;
using Hik.Xml;
using HikPcUI.ImageView;
using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class EdgepatMatchData : CompressMaskData, INotifyPropertyChanged
{
	public Tab ParamTab;

	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

	public ImageData CurImageData;

	public ImageData ModelImageData;

	public byte[] ModelData;

	public byte[] otherMask;

	public byte[] FixInitBuffer;

	public ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> _PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();

	public ObservableCollection<object> _ShapesData = new ObservableCollection<object>();

	public SDKEdgePairpatMatchData SDKImportData;

	public ObservableCollection<object> ItemsData
	{
		get
		{
			return _ItemsData;
		}
		set
		{
			if (_ItemsData != value)
			{
				_ItemsData = value;
				NotifyPropertyChanged("ItemsData");
			}
		}
	}

	public ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> PointsData
	{
		get
		{
			return _PointsData;
		}
		set
		{
			if (_PointsData != value)
			{
				WeakEventManager<ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>, NotifyCollectionChangedEventArgs>.RemoveHandler(PointsData, "CollectionChanged", OnPointsDataCollectionChanged);
				_PointsData = value;
				NotifyPropertyChanged("PointsData");
				NotifyPropertyChanged("EditOutlinePointsEnable");
				WeakEventManager<ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>, NotifyCollectionChangedEventArgs>.AddHandler(PointsData, "CollectionChanged", OnPointsDataCollectionChanged);
			}
		}
	}

	public bool EditOutlinePointsEnable => PointsData != null && PointsData.Count > 0;

	public ObservableCollection<object> ShapesData
	{
		get
		{
			return _ShapesData;
		}
		set
		{
			if (_ShapesData != value)
			{
				_ShapesData = value;
				NotifyPropertyChanged("ShapesData");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		string defaultNameSpace = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser.ParamTab";
		((BaseNode)this).Init(node);
		BaseNode.defaultNameSpace = defaultNameSpace;
		SDKImportData = null;
		return (BaseNode)(object)this;
	}

	private void OnPointsDataCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyPropertyChanged("EditOutlinePointsEnable");
	}

	public void PrepareSDKImportData()
	{
		SDKImportData = new SDKEdgePairpatMatchData();
		SDKImportData.ModuleModelData = new SDKEdgePairpatMatchModelData
		{
			StartIndicate = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "StartIndicate") as NameValueItem)?.Value,
			AutoTrajAutoCfg = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajAutoCfg") as NameValueItem)?.Value,
			AutoTrajStartIdeaWidth = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajStartIdeaWidth") as NameValueItem)?.Value,
			AutoTrajStartEdgeStrength = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajStartEdgeStrength") as NameValueItem)?.Value,
			AutoTrajStartEdge0Polarity = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajStartEdge0Polarity") as NameValueItem)?.Value,
			AutoTrajStartEdge1Polarity = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajStartEdge1Polarity") as NameValueItem)?.Value,
			AutoTrajMaxWidthBias = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajMaxWidthBias") as NameValueItem)?.Value,
			AutoTrajSmoCoef = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "AutoTrajSmoCoef") as NameValueItem)?.Value,
			ModEdgePairFindMode = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgePairFindMode") as NameValueItem)?.Value,
			ModCaliperDistCourse = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModCaliperDistCourse") as NameValueItem)?.Value,
			ModCaliperHeight = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModCaliperHeight") as NameValueItem)?.Value,
			ModCaliperWidth = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModCaliperWidth") as NameValueItem)?.Value,
			ModEdgePairIdealWidth = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgePairIdealWidth") as NameValueItem)?.Value,
			ModEdgeStrength = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgeStrength") as NameValueItem)?.Value,
			ModEdgePairEdge0Polarity = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgePairEdge0Polarity") as NameValueItem)?.Value,
			ModEdgePairEdge1Polarity = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgePairEdge1Polarity") as NameValueItem)?.Value,
			ModFineEdgeEnable = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModFineEdgeEnable") as NameValueItem)?.Value,
			ModHalfKernelSize = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModHalfKernelSize") as NameValueItem)?.Value,
			ModelImageWidth = (CurImageData?.Width.ToString() ?? "0"),
			ModelImageHeight = (CurImageData?.Height.ToString() ?? "0"),
			ModelImageData_ByteArray = CurImageData?.ImageBuffer
		};
		EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST structure = Panel_EdgePairpatMatch.ConvertToAlgoStruct(ItemsData);
		int num = Marshal.SizeOf(structure);
		byte[] array = new byte[num];
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			intPtr = Marshal.AllocHGlobal(num);
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
			Marshal.Copy(intPtr, array, 0, num);
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
		SDKImportData.ModuleModelData.ModelTranj_ByteArray = array;
		if (otherMask == null)
		{
			SDKImportData.ModuleModelData.ClearModelImageMask = "";
			return;
		}
		SDKImportData.ModuleModelData.ModelImageMask_ByteArray = otherMask;
		SDKImportData.ModuleModelData.ClearModelImageMask = null;
	}

	protected override void AfterSaveBaseNode()
	{
		((BaseNode)this).AfterSaveBaseNode();
		SDKImportData = null;
	}
}
