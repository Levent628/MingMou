// ============================================================================
// 文件：Services/ReminderService.cs
// 用途：定时提醒服务，负责管理眨眼提醒的定时触发、暂停/恢复以及计数统计
// ============================================================================

using System;
using Microsoft.UI.Dispatching;
using MingMou.Core;

namespace MingMou.Services
{
    /// <summary>
    /// 提醒服务状态
    /// </summary>
    public enum ReminderState
    {
        /// <summary>正常运行中，正在倒计时</summary>
        Running,

        /// <summary>用户手动暂停</summary>
        Paused,

        /// <summary>因空闲检测自动暂停</summary>
        IdlePaused
    }

    /// <summary>
    /// 定时提醒服务类
    /// 负责管理眨眼提醒的定时触发、暂停/恢复以及计数统计
    /// </summary>
    public sealed class ReminderService : IDisposable
    {
        /// <summary>
        /// 提醒触发事件，订阅者（如 BlinkControl）执行眨眼动画
        /// </summary>
        public event Action? BlinkRequested;

        /// <summary>
        /// 倒计时每秒推进事件，用于界面更新剩余秒数
        /// </summary>
        public event Action? Ticked;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event Action<ReminderState>? StateChanged;

        private readonly DispatcherQueueTimer _timer;
        private readonly ISettingsService _settings;

        private int _intervalSeconds;
        private int _remainingSeconds;
        private int _blinkCount;
        private ReminderState _state = ReminderState.Running;
        private DateTime _manualPauseEndTime = DateTime.MinValue;
        private bool _disposed;

        /// <summary>
        /// 累计活跃用眼秒数（仅在 Running 状态下累计，暂停/空闲不计入），
        /// 作为智能间隔推荐算法的输入
        /// </summary>
        private int _activeElapsedSeconds;

        /// <summary>
        /// 智能间隔模式是否开启：开启后提醒间隔随累计用眼时长动态调整
        /// </summary>
        private bool _enableSmartInterval;

        /// <summary>
        /// 初始化提醒服务，创建定时器但不自动启动
        /// </summary>
        /// <param name="settings">设置服务</param>
        public ReminderService(ISettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // 从持久化设置中读取提醒间隔，若不存在则使用默认值
            _intervalSeconds = Math.Clamp(
                _settings.GetInt(AppConstants.SettingsKeyReminderInterval, AppConstants.ReminderIntervalSeconds),
                AppConstants.MinReminderIntervalSeconds,
                AppConstants.MaxReminderIntervalSeconds);

            // 跨天重置计数：如果上次记录日期不是今天，则清零
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var lastDate = _settings.GetString(AppConstants.SettingsKeyBlinkDate);
            if (lastDate != today)
            {
                _blinkCount = 0;
                _settings.SetInt(AppConstants.SettingsKeyBlinkCount, 0);
                _settings.SetString(AppConstants.SettingsKeyBlinkDate, today);
            }
            else
            {
                _blinkCount = _settings.GetInt(AppConstants.SettingsKeyBlinkCount);
            }

            _remainingSeconds = _intervalSeconds;

            // 读取智能间隔模式状态
            _enableSmartInterval = _settings.GetBool(
                AppConstants.SettingsKeySmartInterval,
                AppConstants.EnableSmartIntervalDefault);

            // 创建 1 秒精度的主线程定时器
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// 当前提醒间隔（秒）
        /// </summary>
        public int IntervalSeconds
        {
            get => _intervalSeconds;
            set
            {
                var clamped = Math.Clamp(value, AppConstants.MinReminderIntervalSeconds, AppConstants.MaxReminderIntervalSeconds);
                if (_intervalSeconds == clamped)
                {
                    return;
                }

                _intervalSeconds = clamped;
                _remainingSeconds = clamped;
                _settings.SetInt(AppConstants.SettingsKeyReminderInterval, clamped);
                Logger.Info($"提醒间隔已调整为 {_intervalSeconds} 秒");
            }
        }

        /// <summary>
        /// 距离下一次提醒的剩余秒数
        /// </summary>
        public int RemainingSeconds => _remainingSeconds;

        /// <summary>
        /// 今日累计眨眼次数
        /// </summary>
        public int BlinkCount => _blinkCount;

        /// <summary>
        /// 当前服务状态
        /// </summary>
        public ReminderState State => _state;

        /// <summary>
        /// 累计活跃用眼秒数（智能间隔算法输入）
        /// </summary>
        public int ActiveElapsedSeconds => _activeElapsedSeconds;

        /// <summary>
        /// 智能间隔模式是否开启。
        /// 开启后提醒间隔随累计用眼时长动态调整（用眼越久间隔越短）；
        /// 关闭则使用用户手动设置的固定间隔。
        /// 状态持久化到设置。
        /// </summary>
        public bool EnableSmartInterval
        {
            get => _enableSmartInterval;
            set
            {
                if (_enableSmartInterval == value)
                {
                    return;
                }

                _enableSmartInterval = value;
                _settings.SetBool(AppConstants.SettingsKeySmartInterval, value);
                Logger.Info($"智能间隔模式已 {(value ? "开启" : "关闭")}");

                // 开启时立即按当前用眼时长应用推荐间隔
                if (value)
                {
                    ApplySmartIntervalIfChanged();
                }
            }
        }

        /// <summary>
        /// 根据累计用眼时长，基于眼科医学依据（20-20-20 法则、视屏终端综合征研究）
        /// 给出推荐的眨眼提醒间隔（秒）。
        /// 阶梯：用眼越久 → 眨眼频率越低 → 提醒间隔越短。
        /// </summary>
        /// <returns>推荐间隔（秒）</returns>
        public int GetSmartRecommendedInterval()
        {
            var elapsedMinutes = _activeElapsedSeconds / 60;

            if (elapsedMinutes < AppConstants.TwentyTwentyTwentyMinutes)
            {
                return AppConstants.SmartIntervalBaselineSeconds;
            }

            if (elapsedMinutes < AppConstants.SmartIntervalLongSessionMinutes)
            {
                return AppConstants.SmartIntervalMediumSeconds;
            }

            if (elapsedMinutes < AppConstants.SmartIntervalExtendedSessionMinutes)
            {
                return AppConstants.SmartIntervalHighSeconds;
            }

            return AppConstants.SmartIntervalMaxSeconds;
        }

        /// <summary>
        /// 智能间隔模式：若当前推荐间隔与当前间隔不同，则动态调整。
        /// 推荐值仅在跨阶梯变化时更新，避免频繁重置倒计时。
        /// </summary>
        private void ApplySmartIntervalIfChanged()
        {
            var recommended = GetSmartRecommendedInterval();
            if (recommended != _intervalSeconds)
            {
                // IntervalSeconds setter 内部会钳制、重置倒计时并持久化
                IntervalSeconds = recommended;
            }
        }

        /// <summary>
        /// 启动提醒服务
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();
            _timer.Start();
            Logger.Info($"提醒服务已启动，间隔 {_intervalSeconds} 秒");
        }

        /// <summary>
        /// 停止提醒服务
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();
            _timer.Stop();
            Logger.Info("提醒服务已停止");
        }

        /// <summary>
        /// 暂停提醒服务
        /// </summary>
        public void Pause()
        {
            ThrowIfDisposed();
            SetState(ReminderState.Paused);
            _manualPauseEndTime = DateTime.MaxValue;
            Logger.Info("提醒服务已暂停");
        }

        /// <summary>
        /// 暂停提醒服务一段固定时长
        /// </summary>
        /// <param name="minutes">暂停时长（分钟）</param>
        public void PauseForMinutes(int minutes)
        {
            ThrowIfDisposed();
            if (minutes <= 0)
            {
                return;
            }

            _manualPauseEndTime = DateTime.Now.AddMinutes(minutes);
            SetState(ReminderState.Paused);
            Logger.Info($"提醒服务已暂停 {minutes} 分钟，预计 {_manualPauseEndTime:HH:mm:ss} 恢复");
        }

        /// <summary>
        /// 恢复提醒服务
        /// </summary>
        public void Resume()
        {
            ThrowIfDisposed();
            if (_state != ReminderState.Running)
            {
                _manualPauseEndTime = DateTime.MinValue;
                _remainingSeconds = _intervalSeconds;
                SetState(ReminderState.Running);
                Logger.Info("提醒服务已恢复");
            }
        }

        /// <summary>
        /// 手动触发一次眨眼提醒
        /// </summary>
        public void TriggerNow()
        {
            ThrowIfDisposed();
            OnBlink();
        }

        /// <summary>
        /// 进入空闲暂停状态
        /// </summary>
        public void EnterIdlePause()
        {
            ThrowIfDisposed();
            if (_state == ReminderState.Running)
            {
                SetState(ReminderState.IdlePaused);
                Logger.Info("用户空闲，提醒服务自动暂停");
            }
        }

        /// <summary>
        /// 退出空闲暂停状态
        /// </summary>
        public void LeaveIdlePause()
        {
            ThrowIfDisposed();
            if (_state == ReminderState.IdlePaused)
            {
                _remainingSeconds = _intervalSeconds;
                SetState(ReminderState.Running);
                Logger.Info("用户恢复操作，提醒服务自动恢复");
            }
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

            Logger.Info("提醒服务已释放");
        }

        /// <summary>
        /// 定时器每秒回调：处理倒计时、手动暂停到期恢复与眨眼触发
        /// </summary>
        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                // 检查用户手动暂停是否已到期
                if (_state == ReminderState.Paused && _manualPauseEndTime != DateTime.MaxValue)
                {
                    if (DateTime.Now >= _manualPauseEndTime)
                    {
                        Resume();
                    }
                }

                // 非运行状态不计时
                if (_state != ReminderState.Running)
                {
                    Ticked?.Invoke();
                    return;
                }

                // 累计活跃用眼时长（智能间隔算法输入）
                _activeElapsedSeconds++;

                // 智能间隔模式：按当前用眼时长动态调整提醒间隔（推荐值变化时才生效）
                if (_enableSmartInterval)
                {
                    ApplySmartIntervalIfChanged();
                }

                _remainingSeconds--;
                if (_remainingSeconds <= 0)
                {
                    // 先重置倒计时再触发提醒：即使 OnBlink 内部发生意外，
                    // 也不会让 _remainingSeconds 卡在 <=0 导致每秒重复触发
                    _remainingSeconds = _intervalSeconds;
                    OnBlink();
                }

                Ticked?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "提醒服务定时器回调异常");
            }
        }

        /// <summary>
        /// 触发一次眨眼，累加计数并持久化。
        /// 持久化与日志均为"尽力而为"：任何失败只记录，绝不阻断 BlinkRequested，
        /// 确保提醒事件永远能送达订阅者（防止偶发静默吞提醒）。
        /// </summary>
        private void OnBlink()
        {
            _blinkCount++;

            try
            {
                _settings.SetInt(AppConstants.SettingsKeyBlinkCount, _blinkCount);

                // 同时刷新日期字段，保证跨天时数据不混淆
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                _settings.SetString(AppConstants.SettingsKeyBlinkDate, today);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "持久化眨眼计数失败（不影响本次提醒）");
            }

            try
            {
                Logger.Info($"触发第 {_blinkCount} 次眨眼提醒");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "记录眨眼提醒日志失败（不影响本次提醒）");
            }

            BlinkRequested?.Invoke();
        }

        /// <summary>
        /// 修改当前状态并触发变更事件
        /// </summary>
        /// <param name="newState">新状态</param>
        private void SetState(ReminderState newState)
        {
            if (_state == newState)
            {
                return;
            }

            _state = newState;
            Ticked?.Invoke();
            StateChanged?.Invoke(_state);
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReminderService));
            }
        }
    }
}
