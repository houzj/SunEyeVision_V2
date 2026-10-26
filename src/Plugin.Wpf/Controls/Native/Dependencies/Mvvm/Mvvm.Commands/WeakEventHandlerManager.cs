using System;
using System.Collections.Generic;
using System.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;

/// <summary>
///     Handles management and dispatching of EventHandlers in a weak way.
/// </summary>
public static class WeakEventHandlerManager
{
	private static readonly SynchronizationContext SyncContext = SynchronizationContext.Current;

	/// <summary>
	///     Invokes the handlers
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="handlers"></param>
	public static void CallWeakReferenceHandlers(object sender, List<WeakReference> handlers)
	{
		if (handlers != null)
		{
			EventHandler[] array = new EventHandler[handlers.Count];
			int count = 0;
			count = CleanupOldHandlers(handlers, array, count);
			for (int i = 0; i < count; i++)
			{
				CallHandler(sender, array[i]);
			}
		}
	}

	private static void CallHandler(object sender, EventHandler eventHandler)
	{
		if (eventHandler == null)
		{
			return;
		}
		if (SyncContext != null)
		{
			SyncContext.Post((object o) =>
			{
				eventHandler(sender, EventArgs.Empty);
			}, null);
		}
		else
		{
			eventHandler(sender, EventArgs.Empty);
		}
	}

	private static int CleanupOldHandlers(IList<WeakReference> handlers, IList<EventHandler> callees, int count)
	{
		for (int num = handlers.Count - 1; num >= 0; num--)
		{
			if (!(handlers[num].Target is EventHandler value))
			{
				handlers.RemoveAt(num);
			}
			else
			{
				callees[count] = value;
				count++;
			}
		}
		return count;
	}

	/// <summary>
	///     Adds a handler to the supplied list in a weak way.
	/// </summary>
	/// <param name="handlers">Existing handler list.  It will be created if null.</param>
	/// <param name="handler">Handler to add.</param>
	/// <param name="defaultListSize">Default list size.</param>
	public static void AddWeakReferenceHandler(ref List<WeakReference> handlers, EventHandler handler, int defaultListSize)
	{
		if (handlers == null)
		{
			handlers = ((defaultListSize > 0) ? new List<WeakReference>(defaultListSize) : new List<WeakReference>());
		}
		handlers.Add(new WeakReference(handler));
	}

	/// <summary>
	///     Removes an event handler from the reference list.
	/// </summary>
	/// <param name="handlers">Handler list to remove reference from.</param>
	/// <param name="handler">Handler to remove.</param>
	public static void RemoveWeakReferenceHandler(List<WeakReference> handlers, EventHandler handler)
	{
		if (handlers == null)
		{
			return;
		}
		for (int num = handlers.Count - 1; num >= 0; num--)
		{
			if (!(handlers[num].Target is EventHandler eventHandler) || eventHandler == handler)
			{
				handlers.RemoveAt(num);
			}
		}
	}
}
