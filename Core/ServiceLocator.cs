// ============================================================================
// 文件：Core/ServiceLocator.cs
// 用途：轻量级依赖注入容器，管理应用级单例服务的生命周期
// ============================================================================

using System;
using System.Collections.Generic;

namespace MingMou.Core
{
    /// <summary>
    /// 服务定位器
    /// 提供构造函数注入的简化实现，避免引入第三方 IoC 框架
    /// </summary>
    public sealed class ServiceLocator
    {
        /// <summary>
        /// 当前进程内的默认服务定位器实例
        /// </summary>
        public static ServiceLocator Current { get; } = new ServiceLocator();

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly object _lock = new object();

        /// <summary>
        /// 注册一个已实例化的服务
        /// </summary>
        /// <typeparam name="TService">服务接口或实现类型</typeparam>
        /// <param name="instance">服务实例</param>
        public void Register<TService>(TService instance) where TService : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            lock (_lock)
            {
                _services[typeof(TService)] = instance;
            }
        }

        /// <summary>
        /// 获取指定类型的服务实例
        /// </summary>
        /// <typeparam name="TService">服务类型</typeparam>
        /// <returns>已注册的服务实例</returns>
        /// <exception cref="InvalidOperationException">服务尚未注册时抛出</exception>
        public TService GetService<TService>() where TService : class
        {
            lock (_lock)
            {
                if (_services.TryGetValue(typeof(TService), out var instance))
                {
                    return (TService)instance;
                }
            }

            throw new InvalidOperationException($"服务未注册: {typeof(TService).FullName}");
        }

        /// <summary>
        /// 尝试获取指定类型的服务实例
        /// </summary>
        /// <typeparam name="TService">服务类型</typeparam>
        /// <param name="instance">输出参数，返回获取到的实例；未注册时为 null</param>
        /// <returns>是否成功获取到服务</returns>
        public bool TryGetService<TService>(out TService? instance) where TService : class
        {
            lock (_lock)
            {
                if (_services.TryGetValue(typeof(TService), out var value))
                {
                    instance = (TService)value;
                    return true;
                }
            }

            instance = null;
            return false;
        }
    }
}
