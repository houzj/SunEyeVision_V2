using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;
public partial class ParamUserControl:ContentControl
{
    public static readonly DependencyProperty ExternDisplayTypeProperty=DependencyProperty.Register(nameof(ExternDisplayType),typeof(string),typeof(ParamUserControl),new PropertyMetadata(null,(d,_)=>((ParamUserControl)d).SelectTemplate()));
    public string? ExternDisplayType {get=>(string?)GetValue(ExternDisplayTypeProperty);set=>SetCurrentValue(ExternDisplayTypeProperty,value);}
    public ParamUserControl()
    {
        InitializeComponent();PreviewMouseDown+=(_,args)=>
        {if(Content is NativeParameterRow row&&row.ParamItemSelectedCommand.CanExecute(args))row.ParamItemSelectedCommand.Execute(args);};
    }
    protected override void OnContentChanged(object oldContent,object newContent){base.OnContentChanged(oldContent,newContent);SelectTemplate();}
    private void SelectTemplate()
    {
        var key=ExternDisplayType??(Content as NativeParameterRow)?.DisplayType;
        if(key is null){SetCurrentValue(ContentTemplateProperty,null);return;}
        if(TryFindResource(key) is not DataTemplate template)throw new ArgumentException("未声明的原参数行模板："+key);
        SetCurrentValue(ContentTemplateProperty,template);
    }
}
