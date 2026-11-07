using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Util;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace DataQueueModuleControl;

public partial class DataQueueDataGrid : UserControl, ISpecialParam, IComponentConnector
{
	private AlgorithmTabRoot rootData;

	private Task _feedTask;

	private bool _dispose = true;

	private IMVS_DATAQUEUE_LIST? _dataqueueList;

	private int _rowCount = 256;

	private bool _bTaskFlag = true;

	private DataGrid _dataGrid;

	private IVmModule _module;

	private string _numStr => string.Format("{0}", AppLocalizationService.GetLang("Number"));

	public IVmModule Module
	{
		get
		{
			if (rootData != null)
			{
				_module = rootData.Owner as IVmModule;
			}
			return _module;
		}
	}

	public string TabItemName => "DataqueueTabItem";

	public DataQueueDataGrid()
	{
		InitializeComponent();
	}

	~DataQueueDataGrid()
	{
		_dispose = true;
		_bTaskFlag = false;
	}

	public DataQueueDataGrid(AlgorithmTabRoot root)
	{
		InitializeComponent();
		rootData = root;
		_dispose = false;
		_dataGrid = dataGridQueue;
	}

	private void startRefreshTask()
	{
		_feedTask = new Task(() =>
		{
			while (_bTaskFlag)
			{
				if (!_dispose)
				{
					Dispatcher.Invoke(RefreshDataGrid);
				}
				Thread.Sleep(500);
			}
		}, TaskCreationOptions.LongRunning);
		_feedTask.Start();
	}

	private void RefreshDataGrid()
	{
		try
		{
			if (Module == null || _dataGrid == null)
			{
				LogHelper.objLog.Error("模块为空或非数据队列模块");
				return;
			}
			IntPtr intPtr = IntPtr.Zero;
			byte[] array = ((IUserBytesData)Module.Params)["DataQueueList"];
			if (array != null && array.Length != 0)
			{
				intPtr = Marshal.AllocHGlobal(array.Length);
				Marshal.Copy(array, 0, intPtr, array.Length);
				IMVS_DATAQUEUE_LIST iMVS_DATAQUEUE_LIST = Marshal.PtrToStructure<IMVS_DATAQUEUE_LIST>(intPtr);
				int result = 256;
				_ = string.Empty;
				int.TryParse(((IUserStringData)Module.Params)["MaxRowNum"], out result);
				string s = ((IUserStringData)Module.Params)["ImageCacheNum"];
				int result2 = 10;
				int.TryParse(s, out result2);
				if (IsAllImageQueue(iMVS_DATAQUEUE_LIST))
				{
					result = result2;
				}
				else if (!IsNoImageQueue(iMVS_DATAQUEUE_LIST) && result2 > result)
				{
					result = result2;
				}
				if (_rowCount != result || !_dataqueueList.HasValue || !_dataqueueList.Value.Equals(iMVS_DATAQUEUE_LIST))
				{
					LogHelper.objLog.Info("Add datagrid data, the rowCount of data is " + result);
					if (!_dataqueueList.HasValue || _dataqueueList.Value.nCount != iMVS_DATAQUEUE_LIST.nCount || DataQueueUtil.bNameChanged || (_dataqueueList.Value.stDataQueue != null && _dataqueueList.Value.stDataQueue.Select((IMVS_DATAQUEUE p) => p.chszQueueName).Aggregate(string.Empty, (string text2, byte[] array2) => text2 + array2) != iMVS_DATAQUEUE_LIST.stDataQueue.Select((IMVS_DATAQUEUE p) => p.chszQueueName).Aggregate(string.Empty, (string text2, byte[] array2) => text2 + array2)))
					{
						LogHelper.objLog.Info("Create new datagrid, the num of queue is " + iMVS_DATAQUEUE_LIST.nCount);
						DataQueueUtil.bNameChanged = false;
						_dataGrid.Columns.Clear();
						_dataGrid.Columns.Add(new DataGridTextColumn
						{
							Header = _numStr,
							Binding = new Binding(_numStr)
						});
						for (int num = 0; num < iMVS_DATAQUEUE_LIST.nCount; num++)
						{
							string text = StringUtil.UTF8GetString(iMVS_DATAQUEUE_LIST.stDataQueue[num].chszQueueName);
							if (Regex.IsMatch(text, "^[\\u4e00-\\u9fa5_a-zA-Z0-9]+$"))
							{
								_dataGrid.Columns.Add(new DataGridTextColumn
								{
									Width = 150.0,
									Header = text,
									Binding = new Binding(text)
								});
							}
						}
					}
					_dataGrid.Items.Clear();
					Dictionary<int, string[]> dictionary = new Dictionary<int, string[]>();
					for (int num2 = 0; num2 < result; num2++)
					{
						QueueScheduleStatisticsModel queueScheduleStatisticsModel = new QueueScheduleStatisticsModel();
						queueScheduleStatisticsModel.SetPropertyValue(_numStr, num2);
						for (int num3 = 0; num3 < iMVS_DATAQUEUE_LIST.nCount; num3++)
						{
							IMVS_DATAQUEUE value = iMVS_DATAQUEUE_LIST.stDataQueue[num3];
							string propertyName = StringUtil.UTF8GetString(value.chszQueueName);
							string value2 = string.Empty;
							if (!dictionary.TryGetValue(num3, out var value3))
							{
								value3 = DataQueueUtil.GetData(value);
								dictionary.Add(num3, value3);
							}
							if (num2 < value.nCount)
							{
								value2 = value3[num2];
							}
							queueScheduleStatisticsModel.SetPropertyValue(propertyName, value2);
						}
						_dataGrid.Items.Add(queueScheduleStatisticsModel);
					}
					_dataqueueList = iMVS_DATAQUEUE_LIST;
					_rowCount = result;
				}
			}
			else
			{
				LogHelper.objLog.Info("the length of DataQueueList is " + array?.Length.ToString());
			}
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
				intPtr = IntPtr.Zero;
			}
		}
		catch
		{
		}
	}

	private bool IsAllImageQueue(IMVS_DATAQUEUE_LIST queueList)
	{
		if (queueList.nCount > 0)
		{
			for (int i = 0; i < queueList.nCount; i++)
			{
				if (!queueList.stDataQueue[i].chszDataType.Equals("IMAGE"))
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}

	private bool IsNoImageQueue(IMVS_DATAQUEUE_LIST queueList)
	{
		for (int i = 0; i < queueList.nCount; i++)
		{
			if (queueList.stDataQueue[i].chszDataType.Equals("IMAGE"))
			{
				return false;
			}
		}
		return true;
	}

	public void InitFunction()
	{
		startRefreshTask();
	}

	public void DisposeFunction()
	{
		try
		{
			_bTaskFlag = false;
			_feedTask.Dispose();
		}
		catch
		{
		}
	}

	public void RenderFunction(Dictionary<string, object> keyValues)
	{
	}
}
