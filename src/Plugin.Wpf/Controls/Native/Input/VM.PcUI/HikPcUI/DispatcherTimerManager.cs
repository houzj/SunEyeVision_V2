using System;
using System.Windows;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI;

public class DispatcherTimerManager : WeakEventManager
{
	private static DispatcherTimerManager current;

	public static void Create(TimeSpan interval, EventHandler<EventArgs> handler)
	{
		DispatcherTimer dispatcherTimer = new DispatcherTimer
		{
			Interval = interval
		};
		AddHandler(dispatcherTimer, handler);
		dispatcherTimer.Start();
	}

	public static void AddHandler(DispatcherTimer source, EventHandler<EventArgs> handler)
	{
		current.ProtectedAddHandler(source, handler);
	}

	public static void RemoveHandler(DispatcherTimer source, EventHandler<EventArgs> handler)
	{
		current.ProtectedRemoveHandler(source, handler);
	}

	static DispatcherTimerManager()
	{
		current = new DispatcherTimerManager();
		WeakEventManager.SetCurrentManager(typeof(DispatcherTimerManager), current);
	}

	protected override ListenerList NewListenerList()
	{
		return new ListenerList<EventArgs>();
	}

	protected override void StartListening(object source)
	{
		DispatcherTimer dispatcherTimer = (DispatcherTimer)source;
		dispatcherTimer.Tick += OnSomeEvent;
	}

	protected override void StopListening(object source)
	{
		DispatcherTimer dispatcherTimer = (DispatcherTimer)source;
		dispatcherTimer.Tick -= OnSomeEvent;
		dispatcherTimer.Stop();
	}

	private void OnSomeEvent(object sender, EventArgs e)
	{
		DeliverEvent(sender, e);
	}
}
