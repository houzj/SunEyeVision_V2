using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Apps.ErrorCode;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using VM.Controls;
using VMControls.BaseInterface;

namespace IMVSBlobFindLabelsModuControl;

public class ThresholdAreaItem : BaseNode, INotifyPropertyChanged
{
	private int index;

	private Integer _grayValue;

	private IntegerBettween _areaRange;

	private bool _isAdd = true;

	private bool _isInverse;

	public int Index
	{
		get
		{
			return index;
		}
		set
		{
			if (index != value)
			{
				index = value;
				OnPropertyChanged("Index");
			}
		}
	}

	public Integer GrayValue
	{
		get
		{
			if (_grayValue == null)
			{
				_grayValue = new Integer();
				_grayValue.SetParentNode(this);
				_grayValue.SubscribeVisibleA = false;
				_grayValue.MinValueA = 1;
				_grayValue.MaxValueA = 255;
				_grayValue.OnCurValueChanged += CurValueChanged;
			}
			return _grayValue;
		}
	}

	public IntegerBettween AreaRange
	{
		get
		{
			if (_areaRange == null)
			{
				_areaRange = new IntegerBettween();
				_areaRange.SetParentNode(this);
				_areaRange.SubscribeVisibleA = false;
				if (_areaRange.IntegersA == null)
				{
					_areaRange.MaxValueA = 999999999;
					_areaRange.MinValueA = 1;
					Integer integer = new Integer();
					integer.MaxValueA = 999999999;
					integer.MinValueA = 1;
					integer.CurValueA = 10;
					Integer integer2 = new Integer();
					integer2.MaxValueA = 999999999;
					integer2.MinValueA = 1;
					integer2.CurValueA = 999999999;
					integer.SetParentNode(_areaRange);
					integer2.SetParentNode(_areaRange);
					integer.OnCurValueChanged += CurValueChanged;
					integer2.OnCurValueChanged += CurValueChanged;
					_areaRange.IntegersA = new List<object>();
					_areaRange.IntegersA.Add(integer);
					_areaRange.IntegersA.Add(integer2);
				}
			}
			return _areaRange;
		}
	}

	public bool IsInverse
	{
		get
		{
			return _isInverse;
		}
		set
		{
			if (_isInverse != value)
			{
				_isInverse = value;
				OnPropertyChanged("IsInverse");
			}
		}
	}

	public bool IsAdd
	{
		get
		{
			return _isAdd;
		}
		set
		{
			if (_isAdd != value)
			{
				_isAdd = value;
				OnPropertyChanged("IsAdd");
			}
		}
	}

	public string ThresholdAreaData => Index.ToString() + "#" + Convert.ToInt32(IsAdd) + "#" + GrayValue.Value + "#" + (AreaRange.IntegersA[0] as Integer).Value + "#" + (AreaRange.IntegersA[1] as Integer).Value + "#" + Convert.ToInt32(IsInverse);

	public IParamsConfig VmParam { get; set; }

	public ThresholdAreaPanel Owner { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private void CurValueChanged(object sender, EventArgs e)
	{
		if (VmParam == null)
		{
			return;
		}
		Integer integer;
		if (Owner != null && (integer = sender as Integer) != null)
		{
			if (Enumerable.Count(Owner.Items, (ThresholdAreaItem p) => p.GrayValue.CurValueA == integer.CurValueA) > 1)
			{
				HikMessageBox.Show("Lang_DuplicateGrayValue", MessageIcons.Warning, isTranslate: true);
				integer.Value = integer.OldValue;
				return;
			}
			integer.OldValue = integer.Value;
		}
		try
		{
			((IUserStringData)VmParam)["ThresholdArea"] = ThresholdAreaData;
		}
		catch (Exception ex)
		{
			int result = 0;
			string text = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
			LogHelper.moduleLog.Error("Set threshold area error: " + text);
		}
	}
}
