using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Extern;

/// <summary>
/// 把行为触发器的事件转成命令调用，支持显式命令参数，缺省时使用事件参数。
/// </summary>
public class ParamsEventCommand : TriggerAction<DependencyObject>
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(ParamsEventCommand),
        new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(ParamsEventCommand),
        new PropertyMetadata(null));

    /// <summary>事件触发时要执行的命令。</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>命令参数，为空时使用触发器传入的事件参数。</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <inheritdoc />
    protected override void Invoke(object parameter)
    {
        ICommand? command = Command;
        if (command is null)
        {
            return;
        }

        object? commandParameter = CommandParameter ?? parameter;
        if (command.CanExecute(commandParameter))
        {
            command.Execute(commandParameter);
        }
    }
}
