using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace IMVSMarkInspModuVAControl;

public class UndoEngine : INotifyPropertyChanged
{
	public class UndoUnit
	{
		public byte Type { get; set; }

		public byte[] BoxData { get; set; }

		public byte[] MaskData { get; set; }
	}

	private int _step = -1;

	private readonly List<UndoUnit> _units = new List<UndoUnit>();

	public EventHandler<UndoUnit> ReDo;

	public EventHandler<UndoUnit> UnDo;

	public bool EnableUndo => _step > 0;

	public bool EnableRedo => _step < _units.Count - 1;

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public void ClearUp()
	{
		_units.Clear();
		_units.Add(new UndoUnit
		{
			Type = 1
		});
		_step = 0;
		OnPropertyChanged("EnableUndo");
		OnPropertyChanged("EnableRedo");
	}

	public void Undo()
	{
		if (!EnableUndo)
		{
			return;
		}
		try
		{
			_step--;
			UndoUnit e = _units[_step];
			if (UnDo != null)
			{
				UnDo(this, e);
			}
			OnPropertyChanged("EnableUndo");
			OnPropertyChanged("EnableRedo");
		}
		catch (Exception)
		{
		}
	}

	public void Redo()
	{
		if (!EnableRedo)
		{
			return;
		}
		try
		{
			_step++;
			UndoUnit e = _units[_step];
			if (ReDo != null)
			{
				ReDo(this, e);
			}
			OnPropertyChanged("EnableUndo");
			OnPropertyChanged("EnableRedo");
		}
		catch (Exception)
		{
		}
	}

	public void AddUndoUnit(UndoUnit unit)
	{
		_step++;
		if (_step < _units.Count)
		{
			_units.RemoveRange(_step, _units.Count - _step);
		}
		if (_units.Count > 10)
		{
			_units.RemoveAt(0);
			_step--;
		}
		_units.Add(unit);
		OnPropertyChanged("EnableUndo");
		OnPropertyChanged("EnableRedo");
	}
}
