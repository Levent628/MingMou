// ============================================================================
// 文件：ReminderPopupWindow.xaml.cs
// 用途：透明提醒小窗：右下角定位、置顶不抢焦点、点击穿透、自动隐藏。
//       背景使用自定义 TransparentBackdrop——真正完全透明（alpha=0 画刷），
//       只有眨眼动画可见，参考 WinUIEx 的 TransparentTintBackdrop（SDK 1.6 可用）。
// ============================================================================

using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MingMou.Core;

namespace MingMou
{
    /// <summary>
    /// 透明提醒小窗：提醒触发时在屏幕右下角浮出，背景完全透明（只有眨眼动画可见），
    /// 不抢焦点、鼠标点击穿透、短暂展示后自动隐藏。主界面保持原样。
    /// </summary>
    public sealed partial class ReminderPopupWindow : Window
    {
        /// <summary>
        /// 自动隐藏用的延迟计时器
        /// </summary>
        private DispatcherQueueTimer? _hideTimer;

        /// <summary>
        /// 提醒小窗在屏幕上的展示时长（秒），之后自动隐藏
        /// </summary>
        private const double ShowDurationSeconds = 2.5;

        /// <summary>
        /// 初始化提醒小窗并设置完全透明背景、无边框、固定尺寸
        /// </summary>
        public ReminderPopupWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }

        /// <summary>
        /// 显示提醒小窗：按 DPI 校准尺寸 → 定位右下角 → 显示 → 置顶不抢焦点 →
        /// 播放眨眼动画 → 定时自动隐藏
        /// </summary>
        public void ShowReminder()
        {
            try
            {
                var appWindow = AppWindow;
                if (appWindow == null)
                {
                    Logger.Error("透明提醒小窗：AppWindow 为空");
                    return;
                }

                ApplyDpiAwareSize(appWindow);
                PositionAtBottomRight(appWindow);
                appWindow.Show();

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                NativeMethods.BringToFrontNoActivate(hWnd);

                BlinkAnimation.PlayBlinkAnimation();
                StartHideTimer();

                Logger.Info("透明提醒小窗已弹出");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "显示提醒小窗失败");
            }
        }

        /// <summary>
        /// 设置窗口样式：无边框、完全透明背景（TransparentBackdrop）、
        /// 固定尺寸、点击穿透、不出现在任务栏/Alt+Tab
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                var appWindow = AppWindow;
                if (appWindow == null) return;

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                }

                // 真·全透明窗口：自定义 TransparentBackdrop（输出 alpha=0 透明画刷），
                // 参考 WinUIEx 的 TransparentTintBackdrop——SDK 1.6 即可，无需升级
                SystemBackdrop = new TransparentBackdrop(
                    WinRT.Interop.WindowNative.GetWindowHandle(this));

                appWindow.Resize(new Windows.Graphics.SizeInt32(
                    AppConstants.ReminderPopupWidth, AppConstants.ReminderPopupHeight));

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                NativeMethods.MakeClickThrough(hWnd);

                // 工具窗口样式：不在任务栏/Alt+Tab 显示（纯展示小窗）
                var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "初始化提醒小窗失败");
            }
        }

        /// <summary>
        /// 按当前显示缩放（DPI）把"设计像素"尺寸换算为物理像素并应用到窗口。
        /// WinUI 布局使用有效像素（epx），AppWindow.Resize 使用物理像素，
        /// 高 DPI 下不换算会导致内容超出窗口被裁剪（表现为"眼睛显示不全"）。
        /// </summary>
        /// <param name="appWindow">窗口对象</param>
        private void ApplyDpiAwareSize(Microsoft.UI.Windowing.AppWindow appWindow)
        {
            var scale = GetRasterizationScale();
            appWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)(AppConstants.ReminderPopupWidth * scale),
                (int)(AppConstants.ReminderPopupHeight * scale)));
        }

        /// <summary>
        /// 获取当前显示缩放比例（1.0 = 100% DPI；窗口未显示时 XamlRoot 可能为空，回退 1.0）
        /// </summary>
        private double GetRasterizationScale()
        {
            return Content?.XamlRoot?.RasterizationScale ?? 1.0;
        }

        /// <summary>
        /// 将提醒小窗定位到工作区右下角（贴近托盘通知区），尺寸按 DPI 换算
        /// </summary>
        /// <param name="appWindow">窗口对象</param>
        private void PositionAtBottomRight(Microsoft.UI.Windowing.AppWindow appWindow)
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            var scale = GetRasterizationScale();

            var width = (int)(AppConstants.ReminderPopupWidth * scale);
            var height = (int)(AppConstants.ReminderPopupHeight * scale);
            var margin = (int)(AppConstants.ReminderWindowMargin * scale);

            var x = workArea.X + workArea.Width - width - margin;
            var y = workArea.Y + workArea.Height - height - margin;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        /// <summary>
        /// 启动展示时长计时，到时自动隐藏小窗
        /// </summary>
        private void StartHideTimer()
        {
            if (_hideTimer == null)
            {
                _hideTimer = DispatcherQueue.CreateTimer();
                _hideTimer.Interval = TimeSpan.FromSeconds(ShowDurationSeconds);
                _hideTimer.Tick += (s, e) =>
                {
                    _hideTimer?.Stop();
                    try { AppWindow?.Hide(); }
                    catch (Exception ex) { Logger.Exception(ex, "隐藏小窗失败"); }
                };
            }
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }
}
