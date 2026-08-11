// ============================================================================
// 文件：App.xaml.cs
// 用途：应用启动逻辑、依赖注入配置与全局异常处理
// ============================================================================

using System;
using Microsoft.UI.Xaml;
using MingMou.Core;
using MingMou.Services;

namespace MingMou
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的 Application 类。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 当前应用实例，便于从其他位置访问
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// 应用主窗口实例
        /// </summary>
        public MainWindow? MainWindow { get; private set; }

        /// <summary>
        /// 托盘图标服务
        /// </summary>
        public TrayIconService? TrayIconService { get; private set; }

        /// <summary>
        /// 提醒服务
        /// </summary>
        public ReminderService? ReminderService { get; private set; }

        /// <summary>
        /// 空闲检测服务
        /// </summary>
        public IdleDetectionService? IdleDetectionService { get; private set; }

        /// <summary>
        /// 透明提醒小窗实例（懒创建、复用）
        /// </summary>
        private ReminderPopupWindow? _reminderPopup;

        private bool _subscriptionsInitialized;

        /// <summary>
        /// 初始化单例应用程序对象。
        /// 这是执行的创作代码的第一行，逻辑上等同于 main() 或 WinMain()
        /// </summary>
        public App()
        {
            InitializeComponent();

            // 订阅全局未处理异常，记录日志并给出中文提示
            UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// 在启动应用程序时调用
        /// </summary>
        /// <param name="args">有关启动请求的详细数据</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                Logger.Initialize();
                Logger.Info("================ 明眸应用启动 ================");

                // 注册服务到依赖注入容器
                ConfigureServices();

                // 获取服务实例
                ReminderService = ServiceLocator.Current.GetService<ReminderService>();
                IdleDetectionService = ServiceLocator.Current.GetService<IdleDetectionService>();

                // 创建主窗口但不立即显示
                MainWindow = new MainWindow(ReminderService);

                // 初始化托盘图标服务，必须持有强引用防止被 GC 回收
                TrayIconService = new TrayIconService(ReminderService);
                TrayIconService.ShowWindowRequested += OnShowWindowRequested;
                TrayIconService.ExitRequested += OnExitRequested;
                TrayIconService.SettingsRequested += OnSettingsRequested;

                // 绑定空闲检测与提醒服务
                IdleDetectionService.IdleStarted += OnIdleStarted;
                IdleDetectionService.IdleEnded += OnIdleEnded;
                ReminderService.StateChanged += OnReminderStateChanged;

                // 提醒触发：主窗口隐藏时由透明小窗呈现（不打断用户，不占系统通知区）
                ReminderService.BlinkRequested += OnBlinkRequestedForReminder;

                // 记录应用级事件订阅，Shutdown 时会统一取消
                _subscriptionsInitialized = true;

                // 启动核心服务
                IdleDetectionService.Start();
                ReminderService.Start();

                // 根据配置决定是否显示主窗口
                var showOnStartup = AppConstants.ShowMainWindowOnStartup;
                if (showOnStartup && MainWindow != null)
                {
                    MainWindow.ShowWindow();
                }

                Logger.Info("明眸应用启动完成");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "应用启动失败");
                ShowFatalError(ex);
                throw;
            }
        }

        /// <summary>
        /// 注册全局服务
        /// </summary>
        private static void ConfigureServices()
        {
            var settings = new SettingsService();
            ServiceLocator.Current.Register<ISettingsService>(settings);

            var reminder = new ReminderService(settings);
            ServiceLocator.Current.Register(reminder);

            var idle = new IdleDetectionService();
            ServiceLocator.Current.Register(idle);
        }

        /// <summary>
        /// 托盘菜单"显示主窗口"或双击托盘时触发
        /// </summary>
        private void OnShowWindowRequested()
        {
            try
            {
                if (MainWindow == null)
                {
                    MainWindow = new MainWindow(ReminderService!);
                }

                MainWindow.ShowWindow();
                Logger.Info("显示主窗口");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "显示主窗口失败");
            }
        }

        /// <summary>
        /// 托盘菜单"设置"时触发：打开主窗口并切换到设置视图（层进式导航）
        /// </summary>
        private void OnSettingsRequested()
        {
            try
            {
                if (MainWindow == null)
                {
                    MainWindow = new MainWindow(ReminderService!);
                }

                MainWindow.ShowWindow();
                MainWindow.ShowSettingsView();
                Logger.Info("托盘请求打开设置");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "打开设置失败");
            }
        }

        /// <summary>
        /// 托盘菜单"退出"时触发
        /// </summary>
        private void OnExitRequested()
        {
            Logger.Info("用户从托盘菜单请求退出应用");
            Shutdown();
        }

        /// <summary>
        /// 用户进入空闲状态时自动暂停提醒
        /// </summary>
        private void OnIdleStarted()
        {
            try
            {
                ReminderService?.EnterIdlePause();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "进入空闲暂停状态失败");
            }
        }

        /// <summary>
        /// 用户恢复操作时自动恢复提醒
        /// </summary>
        private void OnIdleEnded()
        {
            try
            {
                ReminderService?.LeaveIdlePause();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "退出空闲暂停状态失败");
            }
        }

        /// <summary>
        /// 提醒服务状态变化时刷新托盘菜单
        /// </summary>
        /// <param name="state">新状态</param>
        private void OnReminderStateChanged(ReminderState state)
        {
            try
            {
                TrayIconService?.RefreshMenu();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "状态变化刷新托盘菜单失败");
            }
        }

        /// <summary>
        /// 提醒触发分发：主窗口可见时由主窗口播放动画；
        /// 主窗口隐藏时统一弹出透明提醒小窗（唯一提醒方式，不抢焦点、不打扰）。
        /// 日志记录分发状态，便于排查"到点无提示"。
        /// </summary>
        private void OnBlinkRequestedForReminder()
        {
            try
            {
                var mainWindowVisible = MainWindow?.IsWindowVisible() == true;

                Logger.Info($"提醒分发：主窗口可见={mainWindowVisible}");

                // 主窗口可见：动画由主窗口内部播放，这里不重复处理
                if (mainWindowVisible)
                {
                    return;
                }

                ShowReminderPopup();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "处理提醒分发失败");
            }
        }

        /// <summary>
        /// 显示透明提醒小窗（懒创建并复用实例）
        /// </summary>
        private void ShowReminderPopup()
        {
            try
            {
                _reminderPopup ??= new ReminderPopupWindow();
                _reminderPopup.ShowReminder();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "弹出透明提醒小窗失败");
            }
        }

        /// <summary>
        /// 全局未处理异常处理
        /// </summary>
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.Exception(e.Exception, "全局未处理异常");
            e.Handled = true;
            ShowFatalError(e.Exception);
        }

        /// <summary>
        /// 显示致命错误提示
        /// </summary>
        /// <param name="ex">异常对象</param>
        private static void ShowFatalError(Exception ex)
        {
            try
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "明眸 - 发生错误",
                    Content = $"应用遇到了一个无法恢复的问题。\n\n若问题持续出现，请查看以下位置的日志：\n{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\{AppConstants.AppDataFolderName}\\{AppConstants.LogsSubFolderName}",
                    CloseButtonText = "确定"
                };

                // 需要设置 XamlRoot，否则在 WinUI 3 中无法显示
                if (Current.MainWindow?.Content is FrameworkElement root)
                {
                    dialog.XamlRoot = root.XamlRoot;
                    _ = dialog.ShowAsync();
                }
            }
            catch
            {
                // 如果连弹窗都失败了，只能依赖日志
            }
        }

        /// <summary>
        /// 安全地关闭应用，释放托盘与后台服务
        /// </summary>
        public void Shutdown()
        {
            Logger.Info("================ 明眸应用关闭 ================");

            try
            {
                // 取消应用级事件订阅，避免服务 Dispose 后触发空引用或已释放异常
                if (_subscriptionsInitialized)
                {
                    IdleDetectionService!.IdleStarted -= OnIdleStarted;
                    IdleDetectionService!.IdleEnded -= OnIdleEnded;
                    ReminderService!.StateChanged -= OnReminderStateChanged;
                    ReminderService!.BlinkRequested -= OnBlinkRequestedForReminder;
                    TrayIconService!.SettingsRequested -= OnSettingsRequested;
                    _subscriptionsInitialized = false;
                }

                ReminderService?.Dispose();
                IdleDetectionService?.Dispose();
                TrayIconService?.Dispose();

                // 关闭可能存在的提醒小窗
                _reminderPopup?.Close();

                if (MainWindow != null)
                {
                    // 先置退出标记，Close() 才不会被"隐藏到托盘"逻辑拦截；
                    // 保留 Closed 订阅，让窗口在关闭回调里完成自身的事件解绑
                    MainWindow.PrepareForExit();
                    MainWindow.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "应用关闭过程中发生异常");
            }

            Exit();
        }
    }
}
