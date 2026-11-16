using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>UI-stage switch and dependent field using the original field presenters.</summary>
public sealed class NativeUiConditionalFields : Decorator,IDisposable
{
    internal DeclaredFieldRow Switch {get;}
    internal DeclaredFieldRow Dependent {get;}
    private bool _disposed;
    public NativeUiConditionalFields(DeclaredFieldRow toggle,DeclaredFieldRow dependent)
    {
        Switch=toggle;Dependent=dependent;
        var panel=new StackPanel();panel.Children.Add(toggle);panel.Children.Add(dependent);Child=panel;
        toggle.Changed+=Project;Project();
    }
    private void Project()=>Dependent.Visibility=Switch.Setting is ConstantSetting{Value:true}?Visibility.Visible:Visibility.Collapsed;
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Switch.Changed-=Project;Child=null;Switch.Dispose();Dependent.Dispose();
    }
}
