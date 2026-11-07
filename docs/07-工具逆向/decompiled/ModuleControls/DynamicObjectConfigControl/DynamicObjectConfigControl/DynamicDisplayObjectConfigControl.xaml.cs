using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DynamicObjectConfigControl;

public partial class DynamicDisplayObjectConfigControl : Control, IComponentConnector, IStyleConnector
{
	public DynamicDisplayObjectConfigControl()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
