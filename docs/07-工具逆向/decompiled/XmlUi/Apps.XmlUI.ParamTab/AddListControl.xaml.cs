using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows.Markup;

namespace Apps.XmlUI.ParamTab;

public partial class AddListControl : AddListControlBase, IComponentConnector
{
	public AddListControl()
	{
		InitializeComponent();
		DG = dg;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
