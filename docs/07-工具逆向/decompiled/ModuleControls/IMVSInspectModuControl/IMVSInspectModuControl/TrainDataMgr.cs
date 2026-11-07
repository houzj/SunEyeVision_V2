using System;
using System.Collections.Generic;
using Apps.Data;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace IMVSInspectModuControl;

internal class TrainDataMgr
{
	public delegate void DeleteImageDelegate(object obj);

	public delegate void ClearAllModelDelegate();

	public delegate void SetWindowShadowDelegate(bool bVisible);

	private static readonly TrainDataMgr instance;

	public const int DISPLAY_IMAGE_COUNT = 5;

	public const int IMAGE_ADDRESS_HALF_OFFSET = 4;

	public const int MAX_MARK_IMAGE_NUM = 20;

	public const int FreshCount = 5;

	private ImageData _curImageData;

	public static TrainDataMgr Instance => instance;

	public ImageData CurImageData
	{
		get
		{
			return _curImageData;
		}
		set
		{
			_curImageData = value;
		}
	}

	public IParamsConfig Param { get; set; }

	public IVmModule Module { get; set; }

	public ModelTrainDialog Owner { get; set; }

	public bool IsStartTrain { get; set; }

	public bool IsHasModel { get; set; }

	public ModuleTrainData TrainData { get; set; }

	public bool IsEnglish { get; set; }

	public event DeleteImageDelegate DeleteImageEvent;

	public event ClearAllModelDelegate ClearAllModelEvent;

	public event SetWindowShadowDelegate SetWindowShadowEvent;

	static TrainDataMgr()
	{
		instance = new TrainDataMgr();
	}

	private TrainDataMgr()
	{
	}

	public void OnDeleteImageEvent(object obj)
	{
		if (DeleteImageEvent != null)
		{
			DeleteImageEvent(obj);
		}
	}

	public void OnClearAllModelEvent()
	{
		if (ClearAllModelEvent != null)
		{
			ClearAllModelEvent();
		}
	}

	public void OnSetWindowShadowEvent(bool bVisible)
	{
		if (SetWindowShadowEvent != null)
		{
			SetWindowShadowEvent(bVisible);
		}
	}

	public void ClearTempData()
	{
		CurImageData = null;
		Module = null;
		Param = null;
		Owner = null;
		TrainData = null;
	}

	public byte[] GetPointBuffer(float pointX, float pointY)
	{
		List<byte> list = new List<byte>();
		list.AddRange(BitConverter.GetBytes(pointX));
		list.AddRange(BitConverter.GetBytes(pointY));
		return list.ToArray();
	}
}
