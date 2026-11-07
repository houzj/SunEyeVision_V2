using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.PcUI.Controls;
using HikPcUI.Commom;
using VM.Controls;
using VM.Controls.Core.Utilities;
using VM.Utility;
using VMControls.WPF;

namespace VisionMasterMain;

public partial class MainWindow : BaseWindow, IComponentConnector
{
	public static readonly DependencyProperty IsShowProgressMaskProperty = DependencyProperty.Register("IsShowProgressMask", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnIsShowProgressMaskChanged));

	public static readonly DependencyProperty MaskMessageProperty = DependencyProperty.Register("MaskMessage", typeof(string), typeof(MainWindow), new PropertyMetadata(null));

	private ProgressMask progressMask;

	public bool IsShowProgressMask
	{
		get
		{
			return (bool)GetValue(IsShowProgressMaskProperty);
		}
		set
		{
			SetValue(IsShowProgressMaskProperty, value);
		}
	}

	public string MaskMessage
	{
		get
		{
			return (string)GetValue(MaskMessageProperty);
		}
		set
		{
			SetValue(MaskMessageProperty, value);
		}
	}

	private static void OnIsShowProgressMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MainWindow mainWindow = (MainWindow)d;
		if ((bool)e.NewValue)
		{
			mainWindow.AddMask();
		}
		else
		{
			mainWindow.RemoveMask();
		}
	}

	private void RemoveMask()
	{
		PART_View.Children.Remove(progressMask);
		progressMask = null;
	}

	private void AddMask()
	{
		progressMask = new ProgressMask();
		progressMask.IsShowProgressBar = false;
		progressMask.SetBinding(ProgressMask.MessageProperty, new Binding("MaskMessage")
		{
			Source = this
		});
		progressMask.SetBinding(ProgressMask.ValueProperty, new Binding("Progress")
		{
			Source = this
		});
		progressMask.SetValue(Grid.ColumnSpanProperty, 2);
		PART_View.Children.Add(progressMask);
	}

	public MainWindow()
	{
		InitializeComponent();
		CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
		{
		});
		Title = ((ConfigurationHelper.GetConfig("ApplicationName") == string.Empty) ? "VisionMaster" : ConfigurationHelper.GetConfig("ApplicationName"));
	}

	public static void SeMenuFocus(UIElement element)
	{
		MainWindow mainWindow = VisualTreeHelperEx.FindAncestorByType<MainWindow>(element);
		if (mainWindow != null)
		{
			ProcedureConfigControl procedureConfigControl = VisualTreeHelperEx.FindDescendantByType<ProcedureConfigControl>(mainWindow);
			if (procedureConfigControl?.curTabControlBase != null)
			{
				FocusManager.SetFocusedElement(procedureConfigControl.curTabControlBase, element);
			}
		}
	}

	private void Window_Closed(object sender, EventArgs e)
	{
	}

	private void BaseWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
	}

	private void BaseWindow_Closing(object sender, CancelEventArgs e)
	{
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
