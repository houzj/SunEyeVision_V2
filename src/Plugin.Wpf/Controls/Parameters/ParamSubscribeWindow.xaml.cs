using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core;
using SunEyeVision.Plugin.Wpf.Controls.Parameters;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;


namespace SunEyeVision.Plugin.Wpf.Controls.Parameters;

public partial class ParamSubscribeWindow : BaseWindow, IComponentConnector
{
	public static readonly DependencyProperty IsSubscribeIndexProperty = DependencyProperty.Register("IsSubscribeIndex", typeof(bool), typeof(ParamSubscribeWindow), new PropertyMetadata(false));

	public static readonly DependencyProperty MyWindowStateProperty = DependencyProperty.Register("MyWindowState", typeof(WindowState), typeof(ParamSubscribeWindow), new PropertyMetadata(WindowState.Normal, OnMyWindowStateChanged));

	private bool isMovingByOwner = false;
    private Window? attachedOwner;

	private bool isMovingBySelf = false;

	public bool IsSubscribeIndex
	{
		get
		{
			return (bool)GetValue(IsSubscribeIndexProperty);
		}
		set
		{
			SetValue(IsSubscribeIndexProperty, value);
		}
	}

	public WindowState MyWindowState
	{
		get
		{
			return (WindowState)GetValue(MyWindowStateProperty);
		}
		set
		{
			SetValue(MyWindowStateProperty, value);
		}
	}

	public ParamSubscribeWindow()
	{
		InitializeComponent();
        if(TryFindResource("Language") is string language&&language=="English")Width=320;
        SetBinding(IsSubscribeIndexProperty, new Binding("IsIndex"));
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        Loaded += ParamSubscribeWindow_Loaded;
        Closed += ParamSubscribeWindow_Closed;
	}

	private static void OnMyWindowStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ParamSubscribeWindow { MyWindowState: not WindowState.Maximized } paramSubscribeWindow)
		{
			paramSubscribeWindow.WindowState = paramSubscribeWindow.MyWindowState;
		}
	}

    public ParamSubscribeWindow(Window owner) : this()
    {
        ArgumentNullException.ThrowIfNull(owner);
        try
        {
            Owner=owner;attachedOwner=owner;WindowStartupLocation=WindowStartupLocation.Manual;
            owner.LocationChanged+=Owner_LocationOrSizeChanged;owner.SizeChanged+=Owner_LocationOrSizeChanged;
            owner.Closed+=Owner_Closed;
            SetBinding(MyWindowStateProperty,new Binding(nameof(WindowState)){Source=owner});
        }
        catch{DetachOwner();throw;}
    }
    private void Owner_Closed(object? sender,EventArgs args)=>Close();

    private void ParamSubscribeWindow_Closed(object? sender,EventArgs e)=>DetachOwner();

    internal void DetachOwner()
    {
        if(attachedOwner is {} owner)
        {
            owner.LocationChanged-=Owner_LocationOrSizeChanged;owner.SizeChanged-=Owner_LocationOrSizeChanged;
            owner.Closed-=Owner_Closed;attachedOwner=null;
        }
        LocationChanged-=ParamSubscribeWindow_LocationChanged;Loaded-=ParamSubscribeWindow_Loaded;
        Closed-=ParamSubscribeWindow_Closed;BindingOperations.ClearBinding(this,MyWindowStateProperty);
        BindingOperations.ClearBinding(this,IsSubscribeIndexProperty);
    }

	private void ParamSubscribeWindow_Loaded(object sender, RoutedEventArgs e)
	{
		if (Owner != null)
		{
			ChangeLocationAndSizeByOwner();
            LocationChanged -= ParamSubscribeWindow_LocationChanged;
            LocationChanged += ParamSubscribeWindow_LocationChanged;
		}
	}

	private void Owner_LocationOrSizeChanged(object sender, EventArgs e)
	{
		if (!isMovingBySelf)
		{
			isMovingByOwner = true;
            try{ChangeLocationAndSizeByOwner();}
            finally{isMovingByOwner=false;}
		}
	}

	private void ParamSubscribeWindow_LocationChanged(object sender, EventArgs e)
	{
		if (!isMovingByOwner)
		{
			isMovingBySelf = true;
            try{ChangeOwnerLocation();}
            finally{isMovingBySelf=false;}
		}
	}

	private void ChangeLocationAndSizeByOwner()
	{
		if (Owner != null)
		{
			Top = Owner.Top;
			Left = Owner.Left + Owner.ActualWidth;
			Height = Owner.ActualHeight;
		}
	}

	private void ChangeOwnerLocation()
	{
		if (Owner != null)
		{
			Owner.Top = Top;
			Owner.Left = Left - Owner.ActualWidth;
		}
	}

	private void Grid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != null)
		{
			Focus();
		}
	}

}
