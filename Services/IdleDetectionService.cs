// ============================================================================
// 文件：Services/IdleDetectionService.cs
// 用途：基于 Win32 GetLastInputInfo 的空闲检测服务
// ============================================================================

using System;
using Microsoft.UI.Dispatching;
using MingMou.Core;

namespace MingMou.Services
{
    /// <summary>
    /// 空闲检测服务
    /// 每隔一段时间检查用户是否持续无键盘/鼠标操作，并触发状态变更事件
    /// </summary>
    public sealed class IdleDetectionService : IDisposable
    {
        /// <summary>
        /// 用户从非空闲进入空闲状态时触发
        /// </summary>
        public event Action? IdleStarted;

        /// <summary>
        /// 用户从空闲恢复为活跃状态时触发
        /// </summary>
        public event Action? IdleEnded;

        private readonly DispatcherQueueTimer _timer;
        private bool _isIdle;
        private bool _disposed;

        /// <summary>
        /// 初始化空闲检测服务
        /// </summary>
        public IdleDetectionService()
        {
            // 获取主线程调度队列，确保定时回调在 UI 线程执行
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(AppConstants.IdleCheckIntervalMs);
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// 当前是否处于空闲状态
        /// </summary>
        public bool IsIdle => _isIdle;

        /// <summary>
        /// 启动空闲检测
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();
            _timer.Start();
            Logger.Info("空闲检测服务已启动");
        }

        /// <summary>
        /// 停止空闲检测
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();
            _timer.Stop();
            Logger.Info("空闲检测服务已停止");
        }

        /// <summary>
        /// 释放服务占用的资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _timer.Tick -= OnTimerTick;
            _timer.Stop();
            _disposed = true;

            Logger.Info("空闲检测服务已释放");
        }

        /// <summary>
        /// 定时器回调：轮询用户空闲秒数并触发状态切换
        /// </summary>
        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                var idleSeconds = NativeMethods.GetIdleSeconds();
                var shouldBeIdle = idleSeconds >= AppConstants.IdleThresholdSeconds;

                if (shouldBeIdle && !_isIdle)
                {
                    _isIdle = true;
                    Logger.Info($"检测到用户空闲，已连续 {idleSeconds} 秒无操作");
                    IdleStarted?.Invoke();
                }
                else if (!shouldBeIdle && _isIdle)
                {
                    _isIdle = false;
                    Logger.Info("用户恢复操作，退出空闲状态");
                    IdleEnded?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "空闲检测定时器回调异常");
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IdleDetectionService));
            }
        }
    }
}
