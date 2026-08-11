// ============================================================================
// 文件：Core/RelayCommand.cs
// 用途：MVVM 通用的 ICommand 实现
// ============================================================================

using System;
using System.Windows.Input;

namespace MingMou.Core
{
    /// <summary>
    /// 通用命令实现类
    /// 用于把 ViewModel 中的业务操作绑定到 XAML 按钮等控件
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <summary>
        /// 初始化命令
        /// </summary>
        /// <param name="execute">命令执行逻辑</param>
        /// <param name="canExecute">命令是否可执行的判断逻辑，可选</param>
        /// <exception cref="ArgumentNullException">execute 为 null 时抛出</exception>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 命令是否可执行的状态发生变化时触发
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// 判断当前命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>若未提供 canExecute 委托则默认返回 true</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        /// <summary>
        /// 执行命令逻辑
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// 手动触发 CanExecuteChanged 事件，用于刷新按钮可用状态
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
