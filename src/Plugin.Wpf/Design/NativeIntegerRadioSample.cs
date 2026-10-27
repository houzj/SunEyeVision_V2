using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeIntegerRadioPreview : UserControl,IDisposable
{
    public NativeIntegerRadioPreview()=>InitializeComponent();
    public void Dispose(){var content=Content;Content=null;(content as IDisposable)?.Dispose();}
}

/// <summary>Independent presentation-only examples. Uses the production field and template chain.</summary>
public sealed class NativeIntegerRadioSample : Decorator,IDisposable
{
    public DeclaredFieldsView Fields {get;}
    public NativeIntegerRadioSample()
    {
        var options=new Dictionary<string,NativeIntegerRadioOptions>
        {
            ["two"]=new(new[]{new NativeIntegerRadioChoice(10,"绘制"),new NativeIntegerRadioChoice(30,"继承")}),
            ["five"]=new(new[]{new NativeIntegerRadioChoice(-1,"本地"),new NativeIntegerRadioChoice(10,"整体"),new NativeIntegerRadioChoice(30,"参数")},true),
            ["invalid"]=new(new[]{new NativeIntegerRadioChoice(0,"零"),new NativeIntegerRadioChoice(1,"一")})
        };
        Fields=new(new[]{("two","两项选择",10L),("five","五槽选择",-1L),("invalid","待修复值",99L)}
            .Select((item,i)=>(new Port(item.Item1,DataType.Integer,EditModes:FieldEditModes.Constant),
                new FieldPresentation(item.Item1,item.Item2,"原单选示例",i,FieldEditorKind.Scalar),
                (ParameterSetting)new ConstantSetting(item.Item3)))
            .Append((new Port("ordinary",DataType.Boolean),new FieldPresentation("ordinary","普通参数","原单选示例",3,FieldEditorKind.Scalar),
                (ParameterSetting)new ConstantSetting(false))),_=>{},integerRadios:options);
        Child=Fields;
    }
    public void Dispose(){Fields.Dispose();Child=null;}
}
