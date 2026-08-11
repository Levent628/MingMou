// ============================================================================
// 文件：Core/ObservableObject.cs
// 用途：提供 MVVM 中属性变更通知的基础实现
// ============================================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MingMou.Core
{
    /// <summary>
    /// 可观察对象基类
    /// 实现 INotifyPropertyChanged 接口，简化 ViewModel 与 UserControl 的属性绑定
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">变更的属性名；调用处可省略</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 通用属性设置辅助方法：仅在值发生变化时赋值并触发通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">属性 backing field 的引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名；调用处可省略</param>
        /// <returns>是否实际发生了变更</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
