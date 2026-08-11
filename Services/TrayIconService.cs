// ============================================================================
// 文件：Services/TrayIconService.cs
// 用途：管理系统托盘图标、右键菜单与托盘点击事件，保持 TaskbarIcon 不被 GC 回收
// ============================================================================

using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MingMou.Core;

namespace MingMou.Services
{
    /// <summary>
    /// 系统托盘图标服务
    /// </summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly ReminderService _reminderService;
        private readonly ISettingsService _settings;
        private readonly H.NotifyIcon.TaskbarIcon? _taskbarIcon;
        private bool _disposed;

        /// <summary>
        /// 用户请求显示主窗口时触发
        /// </summary>
        public event Action? ShowWindowRequested;

        /// <summary>
        /// 用户请求退出应用时触发
        /// </summary>
        public event Action? ExitRequested;

        /// <summary>
        /// 用户请求打开设置时触发
        /// </summary>
        public event Action? SettingsRequested;

        /// <summary>
        /// 初始化托盘图标服务
        /// </summary>
        /// <param name="reminderService">提醒服务，用于"暂停"等菜单项状态同步</param>
        public TrayIconService(ReminderService reminderService)
        {
            _reminderService = reminderService ?? throw new ArgumentNullException(nameof(reminderService));
            _settings = ServiceLocator.Current.GetService<ISettingsService>();

            try
            {
                _taskbarIcon = new H.NotifyIcon.TaskbarIcon
                {
                    ToolTipText = AppConstants.AppDisplayName,
                    ContextFlyout = BuildContextMenu(),
                    IconSource = LoadIconSource()
                };

                _taskbarIcon.LeftClickCommand = new RelayCommand(_ => ShowWindowRequested?.Invoke());
                _taskbarIcon.DoubleClickCommand = new RelayCommand(_ => ShowWindowRequested?.Invoke());

                // 强制创建托盘图标；必须保留对实例的强引用，否则可能被 GC 回收导致异常
                _taskbarIcon.ForceCreate();
                Logger.Info("系统托盘图标已创建");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "创建系统托盘图标失败");
                _taskbarIcon = null;
            }
        }

        /// <summary>
        /// 刷新托盘菜单的状态（例如暂停/恢复后的菜单文案）
        /// </summary>
        public void RefreshMenu()
        {
            if (_taskbarIcon == null)
            {
                return;
            }

            try
            {
                _taskbarIcon.ContextFlyout = BuildContextMenu();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "刷新托盘菜单失败");
            }
        }

        /// <summary>
        /// 释放托盘图标资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _taskbarIcon?.Dispose();
                Logger.Info("系统托盘图标已释放");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "释放系统托盘图标失败");
            }

            _disposed = true;
        }

        /// <summary>
        /// 加载托盘图标资源
        /// </summary>
        /// <returns>图标图像源</returns>
        private static ImageSource? LoadIconSource()
        {
            try
            {
                // ms-appx:/// 引用打包后的 Assets/icon.ico
                var bitmap = new BitmapImage(new Uri("ms-appx:///Assets/icon.ico"));
                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "加载托盘图标资源失败");
                return null;
            }
        }

        /// <summary>
        /// 构造托盘右键菜单。
        /// 注意：H.NotifyIcon 在 WinUI 模式下会把 MenuFlyout 转成 Win32 PopupMenu，
        /// 该转换**只调用 MenuFlyoutItem 绑定的 Command（ICommand），不触发 Click 事件**。
        /// 因此所有菜单项必须用 Command = new RelayCommand(...) 绑定，否则点击无反应。
        /// </summary>
        /// <returns>MenuFlyout 实例</returns>
        private MenuFlyout BuildContextMenu()
        {
            var flyout = new MenuFlyout();

            // 1) 显示主窗口
            var showItem = new MenuFlyoutItem
            {
                Text = "显示主窗口",
                Icon = new FontIcon { Glyph = "\uE7C4" }, // 屏幕图标
                Command = new RelayCommand(_ => ShowWindowRequested?.Invoke())
            };
            flyout.Items.Add(showItem);

            // 2) 暂停/恢复提醒（时长来自用户配置，默认 30 分钟）
            var pauseMinutes = _settings.GetInt(AppConstants.SettingsKeyPauseMinutes, AppConstants.PauseDurationMinutes);
            var pauseText = _reminderService.State == ReminderState.Running
                ? $"暂停提醒 {pauseMinutes} 分钟"
                : "恢复提醒";
            var pauseItem = new MenuFlyoutItem
            {
                Text = pauseText,
                Icon = new FontIcon { Glyph = "\uE769" }, // 暂停/播放图标
                Command = new RelayCommand(_ => OnPauseClicked())
            };
            flyout.Items.Add(pauseItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            // 3) 设置：打开独立设置窗口
            var settingsItem = new MenuFlyoutItem
            {
                Text = "设置",
                Icon = new FontIcon { Glyph = "\uE713" }, // 齿轮图标
                Command = new RelayCommand(_ =>
                {
                    Logger.Info("用户点击托盘菜单：设置");
                    SettingsRequested?.Invoke();
                })
            };
            flyout.Items.Add(settingsItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            // 4) 退出
            var exitItem = new MenuFlyoutItem
            {
                Text = "退出",
                Icon = new FontIcon { Glyph = "\uE711" }, // 关闭图标
                Command = new RelayCommand(_ => ExitRequested?.Invoke())
            };
            flyout.Items.Add(exitItem);

            return flyout;
        }

        /// <summary>
        /// 处理托盘菜单中的暂停/恢复点击（使用用户配置的暂停时长）
        /// </summary>
        private void OnPauseClicked()
        {
            try
            {
                var pauseMinutes = _settings.GetInt(AppConstants.SettingsKeyPauseMinutes, AppConstants.PauseDurationMinutes);

                if (_reminderService.State == ReminderState.Running)
                {
                    _reminderService.PauseForMinutes(pauseMinutes);
                }
                else
                {
                    _reminderService.Resume();
                }

                RefreshMenu();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "处理托盘菜单暂停/恢复命令失败");
            }
        }
    }
}
