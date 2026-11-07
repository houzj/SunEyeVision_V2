using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Xml;
using Apps.Data;
using Hik.Xml;
using HikPcUI.ImageView;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class PathGuidData : BaseNode, INotifyPropertyChanged
{
	public Tab ParamTab;

	public string ParamValues;

	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

	public ImageData CurImageData;

	public ImageData ModelImageData;

	public bool HasModel;

	public byte[] FixInitBuffer;

	public ObservableCollection<string> StringPointsData = new ObservableCollection<string>();

	public ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> _PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();

	public ObservableCollection<object> _ShapesData = new ObservableCollection<object>();

	public SDKPathData SDKImportData;

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
		string text = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser.ParamTab";
		base.Init(node);
		BaseNode.defaultNameSpace = text;
		ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> observableCollection = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
		foreach (string stringPointsDatum in StringPointsData)
		{
			string[] array = stringPointsDatum.Split('#');
			if (array.Length == 3)
			{
				MVBPATMATCH_EDGEPOINT_OUT item = default;
				float result = 0f;
				float result2 = 0f;
				int result3 = 0;
				item.x = (float.TryParse(array[0], out result) ? result : 0f);
				item.y = (float.TryParse(array[1], out result2) ? result2 : 0f);
				item.score_flag = (int.TryParse(array[2], out result3) ? result3 : 0);
				observableCollection.Add(item);
			}
		}
		PointsData = observableCollection;
		SDKImportData = null;
		return this;
	}

	protected override void BeforSaveBaseNode()
	{
		base.BeforSaveBaseNode();
		StringPointsData.Clear();
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in PointsData)
		{
			string item = pointsDatum.x + "#" + pointsDatum.y + "#" + pointsDatum.score_flag;
			StringPointsData.Add(item);
		}
	}

	private void OnPointsDataCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyPropertyChanged("EditOutlinePointsEnable");
	}

	public void PrepareSDKImportData(IParamsConfig paramsConfig)
	{
		SDKImportData = new SDKPathData();
		SDKImportData.ModuleModelData = new SDKPathModelData
		{
			ModEdgePointFindMode = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgePointFindMode") as NameValueItem)?.Value,
			ModEdgePointPolarity = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgePointPolarity") as NameValueItem)?.Value,
			ModEdgeStrength = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModEdgeStrength") as NameValueItem)?.Value,
			ModCaliperHeight = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModCaliperHeight") as NameValueItem)?.Value,
			ModCaliperWidth = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModCaliperWidth") as NameValueItem)?.Value,
			ModHalfKernelSize = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModHalfKernelSize") as NameValueItem)?.Value,
			ModMaxPointNum = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModMaxPointNum") as NameValueItem)?.Value,
			ModPointOffsetLen = (ParamTab.Collection.FirstOrDefault((object p) => p is NameValueItem nameValueItem && nameValueItem.Name == "ModPointOffsetLen") as NameValueItem)?.Value,
			ModelImageWidth = (CurImageData?.Width.ToString() ?? "0"),
			ModelImageHeight = (CurImageData?.Height.ToString() ?? "0"),
			ModelImageData_ByteArray = CurImageData?.ImageBuffer
		};
		SDKImportData.ModuleModelData.ModelTranj_ByteArray = ((IUserBytesData)paramsConfig)["ModelTranj"];
		SDKImportData.ModuleModelData.ModelProcessResult_ByteArray = ((IUserBytesData)paramsConfig)["ModelProcessResult"];
	}

	protected override void AfterSaveBaseNode()
	{
		base.AfterSaveBaseNode();
		SDKImportData = null;
	}
}
