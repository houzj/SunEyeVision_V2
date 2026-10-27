using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Shared input composition. The caller owns binding selection and publication.</summary>
public sealed class DeclaredInputFieldsView : StackPanel
{
    public IReadOnlyList<DeclaredFieldEditor> Fields { get; }
    public DeclaredInputFieldsView(ToolSchema schema, Func<Port,ParameterSetting> read,
        Action<Port,ParameterSetting> write, Func<Port,ReferenceSetting?> select,
        Func<OutputReference,string>? format=null)
    {
        var fields=new List<DeclaredFieldEditor>();
        foreach(var group in (schema.Presentation??throw new ArgumentException("Input presentation is required.")).Fields.GroupBy(f=>f.Layout!.Section))
        {
            Children.Add(new TextBlock{Text=group.Key,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,10)});
            foreach(var field in group)
            {
                var port=schema.Inputs.Single(p=>p.Id==field.FieldId);
                var editor=new DeclaredFieldEditor(port with{Required=port.Required&&schema.WorkflowImageInputId!=port.Id,EditModes=FieldEditModes.Reference},
                    field.DisplayName,read(port),setting=>write(port,setting),select,formatReference:format,
                    presentation:field,allowUnboundReference:!port.Required||schema.WorkflowImageInputId==port.Id);
                fields.Add(editor);Children.Add(editor);
            }
        }
        Fields=fields.AsReadOnly();
    }
}
