using System;
using HikPcUI.ImageView;

namespace IMVSOcrModuControl;

public class HikPasterParameterExtern : HikPasterParameter
{
	private bool _IsChosen;

	private bool _IsFocusble;

	public bool IsChosen
	{
		get
		{
			return _IsChosen;
		}
		set
		{
			_IsChosen = value;
			NotifyPropertyChanged("IsChosen");
		}
	}

	public override string SubContent
	{
		get
		{
			return _SubContent;
		}
		set
		{
			if (_SubContent != value)
			{
				if (!string.IsNullOrEmpty(value))
				{
					_SubContent = value;
				}
				NotifyPropertyChanged("SubContent");
			}
		}
	}

	public bool IsFocusble
	{
		get
		{
			return _IsFocusble;
		}
		set
		{
			if (_IsFocusble != value)
			{
				_IsFocusble = value;
				NotifyPropertyChanged("IsFocusble");
			}
		}
	}

	public IntPtr CharIntPtr { get; set; } = IntPtr.Zero;
}
