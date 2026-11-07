using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using Apps.PcUI.Common;

namespace DynamicObjectConfigControl;

public partial class DynamicRunParamRelationModulesConfigWin : ConfirmBaseWindow, IComponentConnector
{
	public static readonly DependencyProperty CheckClosingMethodProperty = DependencyProperty.Register("CheckClosingMethod", typeof(Func<object, bool>), typeof(DynamicRunParamRelationModulesConfigWin), new PropertyMetadata(null));

	public Func<object, bool> CheckClosingMethod
	{
		get
		{
			return (Func<object, bool>)GetValue(CheckClosingMethodProperty);
		}
		set
		{
			SetValue(CheckClosingMethodProperty, value);
		}
	}

	public DynamicRunParamRelationModulesConfigWin()
	{
		InitializeComponent();
		Closing += DynamicRunParamRelationModulesConfigWin_Closing;
	}

	private void DynamicRunParamRelationModulesConfigWin_Closing(object sender, CancelEventArgs e)
	{
		if (DialogResult.HasValue && DialogResult.Value)
		{
			e.Cancel = CheckClosingMethod != null && !CheckClosingMethod(DataContext);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
