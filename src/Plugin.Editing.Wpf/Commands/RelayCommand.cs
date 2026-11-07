using System;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Editing.Wpf.Commands
{
    /// <summary>
    /// 简单命令实现 - 用于不需要路由的场景
    /// </summary>
    /// <remarks>
    /// 使用场景：
    /// - 简单的命令绑定（不需要路由事件）
    /// - ViewModel 中的命令实现
    /// - 需要动态改变 CanExecute 的场景
    /// 
    /// 示例：
    /// <![CDATA[
    /// public ICommand SaveCommand { get; }
    /// 
    /// public MyViewModel()
    /// {
    ///     SaveCommand = new RelayCommand(
    ///         execute: () => Save(),
    ///         canExecute: () => CanSave()
    ///     );
    /// }
    /// ]]>
    /// </remarks>
    public class RelayCommand : ICommand
    {
        #region 字段

        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建简单命令
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法（可选）</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region ICommand 实现

        /// <summary>
        /// 可执行状态变化事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute();
        }

        #endregion
    }

    /// <summary>
    /// 泛型命令实现 - 支持参数传递
    /// </summary>
    /// <typeparam name="T">命令参数类型</typeparam>
    /// <remarks>
    /// 示例：
    /// <![CDATA[
    /// public ICommand SelectItemCommand { get; }
    /// 
    /// public MyViewModel()
    /// {
    ///     SelectItemCommand = new RelayCommand<Item>(
    ///         execute: item => SelectItem(item),
    ///         canExecute: item => item != null
    ///     );
    /// }
    /// ]]>
    /// </remarks>
    public class RelayCommand<T> : ICommand
    {
        #region 字段

        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建泛型命令
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法（可选）</param>
        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region ICommand 实现

        /// <summary>
        /// 可执行状态变化事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute((T?)parameter);
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
        }

        #endregion
    }
}
