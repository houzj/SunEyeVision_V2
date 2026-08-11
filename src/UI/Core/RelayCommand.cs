using System;
using System.Windows.Input;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 简洁统一的 RelayCommand 实现
    /// 
    /// 设计原则：
    /// 1. 单一职责：只负责命令的执行和状态管理
    /// 2. 最小化：只保留原型期必需的功能
    /// 3. 解耦：不依赖日志系统，通过事件通知异常
    /// 4. 标准化：符合 Microsoft Prism 设计模式
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private readonly string _commandName;

        /// <summary>
        /// 构造函数（有参数版本）
        /// </summary>
        /// <param name="execute">执行委托</param>
        /// <param name="canExecute">可执行判断委托（可选）</param>
        /// <param name="commandName">命令名称（可选，用于调试）</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null, string? commandName = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _commandName = commandName ?? "UnnamedCommand";
        }

        /// <summary>
        /// 构造函数（无参数版本）- 避免构造函数重载歧义
        /// </summary>
        /// <param name="execute">执行委托</param>
        /// <param name="canExecute">可执行判断委托（可选）</param>
        /// <param name="commandName">命令名称（可选，用于调试）</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null, string? commandName = null)
        {
            _execute = _ => execute();
            _canExecute = canExecute == null ? null : _ => canExecute();
            _commandName = commandName ?? "UnnamedCommand";
        }

        /// <summary>
        /// 命令执行异常事件（用于外部日志记录）
        /// </summary>
        public event EventHandler<CommandExceptionEventArgs>? ExecutionFailed;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// 手动触发 CanExecuteChanged 事件
        /// 
        /// 使用场景：
        /// - 撤销/重做状态变化
        /// - 选区状态变化
        /// - 其他需要手动刷新命令状态的情况
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void Execute(object? parameter)
        {
            try
            {
                _execute(parameter);
            }
            catch (Exception ex)
            {
                // 通过事件通知异常，不直接记录日志（解耦设计）
                OnExecutionFailed(ex, parameter);
                
                // 重新抛出异常，让调用者处理
                throw;
            }
        }

        private void OnExecutionFailed(Exception exception, object? parameter)
        {
            ExecutionFailed?.Invoke(this, new CommandExceptionEventArgs
            {
                CommandName = _commandName,
                Exception = exception,
                Parameter = parameter
            });
        }
    }

    /// <summary>
    /// 命令异常事件参数
    /// </summary>
    public class CommandExceptionEventArgs : EventArgs
    {
        public string CommandName { get; set; } = string.Empty;
        public Exception Exception { get; set; } = null!;
        public object? Parameter { get; set; }
    }

    /// <summary>
    /// 泛型命令（用于有明确类型的参数）
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute((T?)parameter);
        }

        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
        }

        /// <summary>
        /// 手动触发 CanExecuteChanged 事件
        /// 
        /// 使用场景：
        /// - 撤销/重做状态变化
        /// - 选区状态变化
        /// - 其他需要手动刷新命令状态的情况
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
