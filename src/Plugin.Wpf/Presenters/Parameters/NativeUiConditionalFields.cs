using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Window-local switch and dependent field using the original field presenters.</summary>
public sealed class NativeUiConditionalFields : Decorator,IDisposable,IParameterVisibility
{
    internal DeclaredFieldEditor Switch {get;}
    internal DeclaredFieldEditor Dependent {get;}
    private readonly ParameterSetting _switchBaseline,_dependentBaseline;
    private bool _disposed;
    public event Action? Changed;
    public bool HasPendingEdits=>!_disposed&&(Switch.HasPendingEdits||Dependent.HasPendingEdits);
    public bool HasInvalidInput=>!_disposed&&(Switch.HasInvalidText||Dependent.HasInvalidText);
    public bool HasUiChanges=>!_disposed&&(HasPendingEdits||HasInvalidInput||!DeclaredFieldState.SameSetting(Switch.Setting,_switchBaseline)||!DeclaredFieldState.SameSetting(Dependent.Setting,_dependentBaseline));
    public NativeUiConditionalFields(DeclaredFieldEditor toggle,DeclaredFieldEditor dependent)
    {
        Switch=toggle;Dependent=dependent;_switchBaseline=toggle.Setting;_dependentBaseline=dependent.Setting;
        var panel=new StackPanel();panel.Children.Add(toggle);panel.Children.Add(dependent);Child=panel;
        toggle.Changed+=Project;dependent.Changed+=OnChanged;
        AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));
        IsVisibleChanged+=OnVisible;Project();
    }
    private void OnRawChanged(object sender,TextChangedEventArgs args)=>OnChanged();
    private void OnChanged(){if(!_disposed)Changed?.Invoke();}
    private void Project()
    {
        if(_disposed)return;
        var visible=Switch.Setting is ConstantSetting{Value:true};
        ((IParameterVisibility)Dependent).PrepareForVisibility(visible&&IsVisible);
        Dependent.Visibility=visible?Visibility.Visible:Visibility.Collapsed;OnChanged();
    }
    private void OnVisible(object sender,DependencyPropertyChangedEventArgs args)=>((IParameterVisibility)this).PrepareForVisibility(IsVisible);
    void IParameterVisibility.PrepareForVisibility(bool visible)
    {ParameterCleanup.Run(new Action[]{()=>((IParameterVisibility)Switch).PrepareForVisibility(visible),()=>((IParameterVisibility)Dependent).PrepareForVisibility(visible&&Dependent.Visibility==Visibility.Visible)});}
    public void RetireInteractions()
    {if(_disposed)return;((IParameterVisibility)this).PrepareForVisibility(false);if(IsVisible)((IParameterVisibility)this).PrepareForVisibility(true);}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;Switch.Changed-=Project;Dependent.Changed-=OnChanged;
        IsVisibleChanged-=OnVisible;RemoveHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));
        ParameterCleanup.Run(new Action[]{()=>Child=null,Switch.Dispose,Dependent.Dispose});
    }
}
