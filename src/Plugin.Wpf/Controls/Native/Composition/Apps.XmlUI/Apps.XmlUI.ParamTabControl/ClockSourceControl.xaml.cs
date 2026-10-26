using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public partial class ClockSourceControl : Control, IComponentConnector
{
	public static readonly DependencyProperty TickCommandProperty = DependencyProperty.Register("TickCommand", typeof(ICommand), typeof(ClockSourceControl), new PropertyMetadata(null));

	public static readonly DependencyProperty MillisecondsProperty = DependencyProperty.Register("Milliseconds", typeof(int), typeof(ClockSourceControl), new PropertyMetadata(-1, OnMillisecondsChanged));

	public static readonly DependencyProperty ClockIsEnabledProperty = DependencyProperty.Register("ClockIsEnabled", typeof(bool), typeof(ClockSourceControl), new PropertyMetadata(true, OnClockIsEnabledChanged));

	private DispatcherTimer dispatcherTimer = new DispatcherTimer();

	public ICommand TickCommand
	{
		get
		{
			return (ICommand)GetValue(TickCommandProperty);
		}
		set
		{
			SetValue(TickCommandProperty, value);
		}
	}

	public int Milliseconds
	{
		get
		{
			return (int)GetValue(MillisecondsProperty);
		}
		set
		{
			SetValue(MillisecondsProperty, value);
		}
	}

	public bool ClockIsEnabled
	{
		get
		{
			return (bool)GetValue(ClockIsEnabledProperty);
		}
		set
		{
			SetValue(ClockIsEnabledProperty, value);
		}
	}

	private static void OnMillisecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ClockSourceControl clockSourceControl = d as ClockSourceControl;
		int num = (int)e.NewValue;
		if (num != -1)
		{
			if (num < 0)
			{
				MessageBox.Show($"timer Interval {num}");
				return;
			}
			clockSourceControl.dispatcherTimer.Interval = TimeSpan.FromMilliseconds(num);
			clockSourceControl.dispatcherTimer.Start();
		}
	}

	private static void OnClockIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ClockSourceControl clockSourceControl = d as ClockSourceControl;
		bool flag = (bool)e.NewValue;
		clockSourceControl.dispatcherTimer.IsEnabled = flag && clockSourceControl.IsVisible;
	}

	public ClockSourceControl()
	{
		dispatcherTimer.Tick += DispatcherTimer_Tick;
	}

	private void DispatcherTimer_Tick(object sender, EventArgs e)
	{
		TickCommand?.Execute(null);
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property != UIElement.IsVisibleProperty)
		{
			return;
		}
		if ((bool)e.NewValue)
		{
			if (Milliseconds > -1 && ClockIsEnabled)
			{
				dispatcherTimer.Start();
			}
		}
		else
		{
			dispatcherTimer.Stop();
		}
	}
}
