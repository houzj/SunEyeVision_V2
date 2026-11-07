using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Interface;
using Apps.XmlParser.ParamTab;
using VM.PlatformSDKCS;

namespace GlobalCameraModuleControl;

public partial class FrequentlyParamListView : UserControl, ISpecialParam, IComponentConnector
{
	public delegate void SelectItemChaneged(NameValueItem info);

	private static object synParamInfoListObj = new object();

	public SelectItemChaneged OnSelectedItemChanged;

	private AlgorithmTabRoot _Root;

	private object _CameraName;

	private object _CameraWidth;

	private object _CameraHeight;

	private object _AcquisitionFrameRate;

	private object _ResultingFrameRate;

	private object _ExposureTime;

	private object _TriggerSource;

	private object _PixelFormat;

	private AlgorithmTabRoot Root => _Root;

	public object CameraName
	{
		get
		{
			if (_CameraName == null && Root != null)
			{
				_CameraName = Root.FindBaseNodeByName("CameraName");
			}
			return _CameraName;
		}
	}

	public object CameraWidth
	{
		get
		{
			if (_CameraWidth == null && Root != null)
			{
				_CameraWidth = Root.FindBaseNodeByName("Width");
			}
			return _CameraWidth;
		}
	}

	public object CameraHeight
	{
		get
		{
			if (_CameraHeight == null && Root != null)
			{
				_CameraHeight = Root.FindBaseNodeByName("Height");
			}
			return _CameraHeight;
		}
	}

	public object AcquisitionFrameRate
	{
		get
		{
			if (_AcquisitionFrameRate == null && Root != null)
			{
				_AcquisitionFrameRate = Root.FindBaseNodeByName("AcquisitionFrameRate");
			}
			return _AcquisitionFrameRate;
		}
	}

	public object ResultingFrameRate
	{
		get
		{
			if (_ResultingFrameRate == null && Root != null)
			{
				_ResultingFrameRate = Root.FindBaseNodeByName("ResultingFrameRate");
			}
			return _ResultingFrameRate;
		}
	}

	public object ExposureTime
	{
		get
		{
			if (_ExposureTime == null && Root != null)
			{
				_ExposureTime = Root.FindBaseNodeByName("ExposureTime");
			}
			return _ExposureTime;
		}
	}

	public object TriggerSource
	{
		get
		{
			if (_TriggerSource == null && Root != null)
			{
				_TriggerSource = Root.FindBaseNodeByName("TriggerSource");
			}
			return _TriggerSource;
		}
	}

	public object PixelFormat
	{
		get
		{
			if (_PixelFormat == null && Root != null)
			{
				_PixelFormat = Root.FindBaseNodeByName("PixelFormat");
			}
			return _PixelFormat;
		}
	}

	public string TabItemName => "CommonParamTabItem";

	public FrequentlyParamListView()
	{
		InitializeComponent();
	}

	public FrequentlyParamListView(AlgorithmTabRoot data)
	{
		_Root = data;
		InitializeComponent();
		UserControl.Loaded += FrequentlyParamListView_Loaded;
	}

	private void FrequentlyParamListView_Loaded(object sender, RoutedEventArgs e)
	{
	}

	public void ClearParamListView()
	{
	}

	private void listViewUserDefinedKeyParam_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
	}

	public void InitFunction()
	{
		if (CameraName is EnumerationLx)
		{
			ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM += (CameraName as EnumerationLx).ServerManager_OnReportDataToVM;
		}
	}

	public void DisposeFunction()
	{
		if (CameraName is EnumerationLx)
		{
			ServerSDKManager.serverSDKManager.mSolutionManager.OnReportDataToVM -= (CameraName as EnumerationLx).ServerManager_OnReportDataToVM;
		}
	}

	public void RenderFunction(Dictionary<string, object> keyValues)
	{
	}

	private void FrequentlyParamListView_UnLoaded(object sender, RoutedEventArgs e)
	{
	}
}
