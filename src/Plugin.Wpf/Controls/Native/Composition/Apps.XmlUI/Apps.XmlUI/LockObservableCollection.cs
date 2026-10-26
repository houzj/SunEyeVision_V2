using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class LockObservableCollection<T> : ObservableCollection<T>
{
	private readonly ReaderWriterLockSlim lockSlim = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

	public override event NotifyCollectionChangedEventHandler CollectionChanged;

	protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
	{
		NotifyCollectionChangedEventHandler notifyCollectionChangedEventHandler = CollectionChanged;
		if (notifyCollectionChangedEventHandler == null)
		{
			return;
		}
		Delegate[] invocationList = notifyCollectionChangedEventHandler.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			NotifyCollectionChangedEventHandler handler = (NotifyCollectionChangedEventHandler)invocationList[i];
			if (handler.Target is DispatcherObject { Dispatcher: { } dispatcher } && !dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke((Action)(() =>
				{
					handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}), DispatcherPriority.DataBind);
			}
			else
			{
				handler(this, e);
			}
		}
	}

	protected override void SetItem(int index, T item)
	{
		lockSlim.EnterWriteLock();
		try
		{
			base.SetItem(index, item);
		}
		finally
		{
			lockSlim.ExitWriteLock();
		}
	}

	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		lockSlim.EnterUpgradeableReadLock();
		try
		{
			lockSlim.EnterWriteLock();
			try
			{
				base.OnPropertyChanged(e);
			}
			finally
			{
				lockSlim.ExitWriteLock();
			}
		}
		finally
		{
			lockSlim.ExitUpgradeableReadLock();
		}
	}

	protected override void InsertItem(int index, T item)
	{
		lockSlim.EnterWriteLock();
		try
		{
			base.InsertItem(index, item);
		}
		finally
		{
			lockSlim.ExitWriteLock();
		}
	}

	protected override void RemoveItem(int index)
	{
		lockSlim.EnterWriteLock();
		try
		{
			base.RemoveItem(index);
		}
		finally
		{
			lockSlim.ExitWriteLock();
		}
	}

	protected override void MoveItem(int oldIndex, int newIndex)
	{
		lockSlim.EnterWriteLock();
		try
		{
			base.MoveItem(oldIndex, newIndex);
		}
		finally
		{
			lockSlim.ExitWriteLock();
		}
	}

	protected override void ClearItems()
	{
		lockSlim.EnterWriteLock();
		try
		{
			base.ClearItems();
		}
		finally
		{
			lockSlim.ExitWriteLock();
		}
	}

	public override bool Equals(object obj)
	{
		if (!lockSlim.IsWriteLockHeld)
		{
			lockSlim.EnterReadLock();
		}
		try
		{
			return base.Equals(obj);
		}
		finally
		{
			if (!lockSlim.IsWriteLockHeld)
			{
				lockSlim.ExitReadLock();
			}
		}
	}

	public override int GetHashCode()
	{
		if (!lockSlim.IsWriteLockHeld)
		{
			lockSlim.EnterReadLock();
		}
		try
		{
			return base.GetHashCode();
		}
		finally
		{
			if (!lockSlim.IsWriteLockHeld)
			{
				lockSlim.ExitReadLock();
			}
		}
	}
}
