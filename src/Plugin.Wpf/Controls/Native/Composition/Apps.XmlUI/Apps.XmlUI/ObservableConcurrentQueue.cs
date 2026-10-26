using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class ObservableConcurrentQueue<T> : INotifyCollectionChanged
{
	private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

	public int Count => _queue.Count;

	public event NotifyCollectionChangedEventHandler CollectionChanged;

	protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
	{
		CollectionChanged?.Invoke(this, e);
	}

	public void Enqueue(T item)
	{
		_queue.Enqueue(item);
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
	}

	public bool TryDequeue(out T result)
	{
		result = default;
		if (_queue.TryDequeue(out result))
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, result));
			return true;
		}
		return false;
	}

	public bool TryPeek(out T result)
	{
		return _queue.TryPeek(out result);
	}

	public IEnumerable<T> Where(Func<T, bool> predicate)
	{
		return _queue.AsEnumerable().Where(predicate);
	}

	public ObservableCollection<T> ToObservableCollection()
	{
		return new ObservableCollection<T>(_queue.ToList());
	}
}
