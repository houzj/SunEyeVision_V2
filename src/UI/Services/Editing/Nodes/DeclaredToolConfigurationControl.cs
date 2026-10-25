using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Host-owned basic configuration when an optional specialized editor is absent.
/// Uses the existing draft/execute/commit path. Full layout, binding and ROI gestures belong to S4.</summary>
public sealed class DeclaredToolConfigurationControl : ToolDebugControlBase, IToolEditorCommitParticipant
{
    private readonly IToolPlugin _tool;
    private ToolParameters? _parameters;
    private readonly List<(FrameworkElement Editor,DependencyProperty Target,string Label)> _editors=new();
    public DeclaredToolConfigurationControl(IToolPlugin tool){_tool=tool;}
    public override void SetParameters(ToolParameters parameters)
    {
        base.SetParameters(parameters);_parameters=parameters;
        _editors.Clear();
        var panel=new StackPanel{Margin=new Thickness(12)};
        foreach(var field in (parameters as IRuntimeParameterContract)?.GetRuntimeParameters()
            ??Array.Empty<RuntimeParameterDescriptor>())
        {
            var property=parameters.GetType().GetProperty(field.PropertyName);
            if(property is not {CanRead:true,CanWrite:true})continue;
            var type=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
            FrameworkElement editor;DependencyProperty target;
            if(type==typeof(bool)){editor=new CheckBox();target=ToggleButton.IsCheckedProperty;}
            else if(type.IsEnum){editor=new ComboBox{ItemsSource=Enum.GetValues(type)};target=Selector.SelectedItemProperty;}
            else if(type==typeof(int)||type==typeof(long)||type==typeof(double)||type==typeof(float)||type==typeof(string))
            {editor=new TextBox();target=TextBox.TextProperty;}
            else continue;
            editor.Name=field.PropertyName;
            editor.SetBinding(target,new Binding(field.PropertyName){Source=parameters,Mode=BindingMode.TwoWay,
                UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnExceptions=true,NotifyOnValidationError=true});
            _editors.Add((editor,target,field.DisplayName));
            var row=new Grid{Margin=new Thickness(0,3,0,3)};
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(150)});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var label=new TextBlock{Text=field.DisplayName,VerticalAlignment=VerticalAlignment.Center};
            row.Children.Add(label);Grid.SetColumn(editor,1);row.Children.Add(editor);panel.Children.Add(row);
        }
        Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    public bool TryValidateEditor(out string error)
    {
        foreach(var field in _editors)
        {
            field.Editor.GetBindingExpression(field.Target)?.UpdateSource();
            if(Validation.GetHasError(field.Editor)){error=field.Label+"输入无效。";return false;}
        }
        var validation=_parameters?.Validate();
        error=validation is null?"配置尚未初始化。":validation.IsValid?"":string.Join("；",validation.Errors);
        return error.Length==0;
    }
    public bool TryCommitEditor(out string error)=>TryValidateEditor(out error);
    public override bool ValidateParameters()=>TryValidateEditor(out _);
    protected override bool CanExecuteTool()=>_parameters is not null;
    protected override object ExecuteTool()=>ExecuteWithLiveContext(_tool,_parameters??throw new InvalidOperationException("配置尚未初始化。"));
}

public static class ToolConfigurationEditors
{
    public static FrameworkElement? CreateControl(IToolPlugin tool)
    {
        if(ToolRegistry.CreateEditorFactory(tool) is { } factory)return factory.CreateDebugControl(tool);
        var id=tool.GetType().GetCustomAttributes(typeof(ToolAttribute),false).OfType<ToolAttribute>().SingleOrDefault()?.Id;
        return ToolRegistry.GetComponentMetadata().Any(c=>c.ToolId==id&&c.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime)
            ?new DeclaredToolConfigurationControl(tool):null;
    }
}
