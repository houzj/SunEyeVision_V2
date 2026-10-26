using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Specialized;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

internal class WeakCollectionChangedWrapper : IList, ICollection, IEnumerable, INotifyCollectionChanged
{
	private WeakEventListener<NotifyCollectionChangedEventArgs> _innerListListener;

	private IList _innerList;

	bool IList.IsFixedSize => _innerList.IsFixedSize;

	bool IList.IsReadOnly => _innerList.IsReadOnly;

	object IList.this[int index]
	{
		get
		{
			return _innerList[index];
		}
		set
		{
			_innerList[index] = value;
		}
	}

	int ICollection.Count => _innerList.Count;

	bool ICollection.IsSynchronized => _innerList.IsSynchronized;

	object ICollection.SyncRoot => _innerList.SyncRoot;

	public event NotifyCollectionChangedEventHandler CollectionChanged;

	public WeakCollectionChangedWrapper(IList sourceList)
	{
		_innerList = sourceList;
		if (_innerList is INotifyCollectionChanged source)
		{
			_innerListListener = new WeakEventListener<NotifyCollectionChangedEventArgs>(OnInnerCollectionChanged);
			CollectionChangedEventManager.AddListener(source, _innerListListener);
		}
	}

	private void OnInnerCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
	{
		if (CollectionChanged != null)
		{
			CollectionChanged(this, args);
		}
	}

	internal void ReleaseEvents()
	{
		if (_innerListListener != null)
		{
			CollectionChangedEventManager.RemoveListener((INotifyCollectionChanged)_innerList, _innerListListener);
			_innerListListener = null;
		}
	}

	int IList.Add(object value)
	{
		return _innerList.Add(value);
	}

	void IList.Clear()
	{
		_innerList.Clear();
	}

	bool IList.Contains(object value)
	{
		return _innerList.Contains(value);
	}

	int IList.IndexOf(object value)
	{
		return _innerList.IndexOf(value);
	}

	void IList.Insert(int index, object value)
	{
		_innerList.Insert(index, value);
	}

	void IList.Remove(object value)
	{
		_innerList.Remove(value);
	}

	void IList.RemoveAt(int index)
	{
		_innerList.RemoveAt(index);
	}

	void ICollection.CopyTo(Array array, int index)
	{
		_innerList.CopyTo(array, index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return _innerList.GetEnumerator();
	}
}
