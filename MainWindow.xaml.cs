// ============================================================================
// 文件：MainWindow.xaml.cs
// 用途：主窗口的后台逻辑，包括无边框样式、窗口控制、动画触发与状态同步
// ============================================================================

using System;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using MingMou.Core;
using MingMou.Services;

namespace MingMou
{
    /// <summary>
    /// 应用主窗口
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly ReminderService _reminderService;
        private readonly ISettingsService _settings;

        /// <summary>
        /// 标记当前关闭动作是否为"真正退出应用"。
        /// false 时点击关闭按钮只隐藏窗口；true 时（由托盘"退出"触发）才允许窗口真正销毁。
        /// </summary>
        private bool _isExiting;

        /// <summary>
        /// 当前暂停时长（分钟），默认 30，可配置并持久化
        /// </summary>
        private int _pauseMinutes;

        /// <summary>
        /// 是否正在通过自定义标题栏手动拖动窗口。
        /// 由 PointerPressed 置 true，PointerReleased 置 false。
        /// </summary>
        private bool _isDragging;

        /// <summary>拖动开始时鼠标光标的屏幕 X 坐标（来自 GetCursorPos）</summary>
        private int _dragStartCursorX;

        /// <summary>拖动开始时鼠标光标的屏幕 Y 坐标</summary>
        private int _dragStartCursorY;

        /// <summary>拖动开始时窗口左上角的屏幕 X 坐标</summary>
        private int _dragStartWindowX;

        /// <summary>拖动开始时窗口左上角的屏幕 Y 坐标</summary>
        private int _dragStartWindowY;

        /// <summary>
        /// 初始化主窗口
        /// </summary>
        /// <param name="reminderService">提醒服务</param>
        public MainWindow(ReminderService? reminderService)
        {
            _reminderService = reminderService ?? throw new ArgumentNullException(nameof(reminderService));
            _settings = ServiceLocator.Current.GetService<ISettingsService>();
            _pauseMinutes = _settings.GetInt(AppConstants.SettingsKeyPauseMinutes, AppConstants.PauseDurationMinutes);

            InitializeComponent();

            InitializeWindow();
            AttachEvents();
            InitializeBindings();
        }

        /// <summary>
        /// 主窗口关闭事件处理
        /// </summary>
        public void OnMainWindowClosed(object sender, WindowEventArgs args)
        {
            // 如果不是真正退出，则取消关闭并隐藏窗口到托盘
            if (!_isExiting)
            {
                args.Handled = true;
                HideWindow();
                Logger.Info("主窗口关闭按钮被拦截，窗口已隐藏到托盘");
            }
            else
            {
                DetachEvents();
            }
        }

        /// <summary>
        /// 标记应用即将真正退出，之后调用 Close() 才会真正销毁窗口。
        /// 由 App.Shutdown()（托盘"退出"菜单）调用。
        /// </summary>
        public void PrepareForExit()
        {
            _isExiting = true;
        }

        /// <summary>
        /// 显示主窗口并居中（用户主动打开时调用：正常激活，抢占焦点符合预期）
        /// </summary>
        public void ShowWindow()
        {
            try
            {
                var appWindow = AppWindow;
                if (appWindow == null)
                {
                    return;
                }

                // 每次打开都从当前设置刷新滑块/开关，避免与设置窗口的修改不同步
                SyncSettingsControls();

                // 先居中再显示，避免窗口闪烁
                CenterOnScreen();
                appWindow.Show();
                this.Activate();

                // 前台锁兜底：用户刚操作其他软件时（如双击托盘），Windows 会拒绝普通 Activate，
                // 导致窗口显示在别的程序后面；用 AttachThreadInput 技巧强制置前
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                NativeMethods.ForceForeground(hWnd);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "显示主窗口失败");
            }
        }

        /// <summary>
        /// 从当前设置与提醒服务刷新设置视图内的控件（滑块、开关、智能信息），
        /// 与运行状态保持同步
        /// </summary>
        private void SyncSettingsControls()
        {
            IntervalSlider.Value = _reminderService.IntervalSeconds;
            IntervalValueText.Text = $"{_reminderService.IntervalSeconds} 秒";

            SmartIntervalToggle.IsOn = _reminderService.EnableSmartInterval;
            UpdateSmartInfo();

            // 同步开机自启动状态（注册表查询，每次进入设置视图刷新）
            AutoStartToggle.IsOn = StartupManager.IsAutoStartEnabled();

            // 同步暂停时长候选框（可能被托盘/其他入口修改）
            SyncPauseComboSelection();
        }

        /// <summary>
        /// 隐藏主窗口到托盘
        /// </summary>
        public void HideWindow()
        {
            try
            {
                AppWindow?.Hide();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "隐藏主窗口失败");
            }
        }

        /// <summary>
        /// 判断主窗口当前是否可见。
        /// WinUI 3 的 AppWindow 未公开 Visible 属性，因此借助 Win32 IsWindowVisible 判断，
        /// 用于区分"窗口处于隐藏状态"与"用户已手动打开窗口"。
        /// </summary>
        /// <returns>窗口可见返回 true；判断失败时返回 false</returns>
        public bool IsWindowVisible()
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                return NativeMethods.IsWindowVisible(hWnd);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "判断窗口可见性失败");
                return false;
            }
        }

        /// <summary>
        /// 初始化窗口样式：无边框、圆角、固定尺寸、居中
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                var appWindow = AppWindow;
                if (appWindow == null)
                {
                    Logger.Error("AppWindow 为空，无法初始化窗口样式");
                    return;
                }

                // 让 XAML 内容延伸到标题栏区域。
                // 注意：该属性只能在代码里设置，写进 XAML 会导致 SDK 1.6 的 XAML 编译器崩溃。
                ExtendsContentIntoTitleBar = true;

                // 设置亚克力（Acrylic）背景材质：相比 Mica 半透明毛玻璃感更明显。
                // 注意：窗口级 Acrylic backdrop 的类名是 DesktopAcrylicBackdrop
                //（AcrylicBackdrop 并不存在，误用会导致 XAML 编译器静默崩溃），
                // 且它也只能在代码里设置——SDK 1.6 的 XAML 编译器遇到 XAML 中的
                // <DesktopAcrylicBackdrop /> 同样会静默退出（<MicaBackdrop /> 则没问题）。
                // 另外需系统开启"设置→个性化→颜色→透明效果"，否则 Acrylic 会退化为纯色。
                SystemBackdrop = new DesktopAcrylicBackdrop();

                // 标题栏由自定义 XAML 内容承担，这里把系统按钮背景设为透明以透出云母
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                // 通过 OverlappedPresenter 去掉系统标题栏并保留边框
                // SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)：保留边框与圆角，隐藏标题栏
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(true, false);
                    // 响应式：允许用户自由调整窗口大小（保留边框即可拖拽边缘缩放）。
                    // 注：SDK 1.6 无内置最小尺寸 API（PreferredMinimum* 是 1.7+），
                    // 布局靠 Grid 自适应 + 设置视图 ScrollViewer 兜底，暂不做最小尺寸限制。
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                }

                // 初始窗口大小（用户可自由调整）
                appWindow.Resize(new SizeInt32(AppConstants.MainWindowWidth, AppConstants.MainWindowHeight));

                // 显式设置圆角偏好，确保在移除标题栏后仍保持圆角
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                NativeMethods.SetWindowCornerPreference(hWnd, DwmWindowCornerPreference.Round);

                CenterOnScreen();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "初始化窗口样式失败");
            }
        }

        /// <summary>
        /// 将窗口居中到当前屏幕工作区
        /// </summary>
        private void CenterOnScreen()
        {
            try
            {
                var appWindow = AppWindow;
                if (appWindow == null)
                {
                    return;
                }

                var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                var x = (workArea.Width - AppConstants.MainWindowWidth) / 2;
                var y = (workArea.Height - AppConstants.MainWindowHeight) / 2;

                appWindow.Move(new PointInt32(workArea.X + x, workArea.Y + y));
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "主窗口居中失败");
            }
        }

        /// <summary>
        /// 绑定事件：提醒触发、倒计时推进、窗口关闭、标题栏拖动等
        /// </summary>
        private void AttachEvents()
        {
            Closed += OnMainWindowClosed;

            _reminderService.BlinkRequested += OnBlinkRequested;
            _reminderService.Ticked += OnReminderTicked;
            _reminderService.StateChanged += OnReminderStateChanged;

            // 自定义标题栏手动拖动：按下时记录起点，移动时通过 AppWindow.Move 平移窗口，释放时清理
            AppTitleBar.PointerPressed += OnTitleBarPointerPressed;
            AppTitleBar.PointerMoved += OnTitleBarPointerMoved;
            AppTitleBar.PointerReleased += OnTitleBarPointerReleased;
        }

        /// <summary>
        /// 取消事件订阅，防止窗口反复隐藏/显示时内存泄漏
        /// </summary>
        private void DetachEvents()
        {
            Closed -= OnMainWindowClosed;

            _reminderService.BlinkRequested -= OnBlinkRequested;
            _reminderService.Ticked -= OnReminderTicked;
            _reminderService.StateChanged -= OnReminderStateChanged;

            // 自定义标题栏手动拖动事件（详见 OnTitleBarPointerPressed 等方法）
            AppTitleBar.PointerPressed -= OnTitleBarPointerPressed;
            AppTitleBar.PointerMoved -= OnTitleBarPointerMoved;
            AppTitleBar.PointerReleased -= OnTitleBarPointerReleased;
        }

        /// <summary>
        /// 初始化控件绑定与初始状态
        /// </summary>
        private void InitializeBindings()
        {
            // 提醒间隔滑块范围与初始值（XAML 中无法用 {x:Static} 引用 const，故在代码中设置；
            // 赋值会触发一次 ValueChanged，设置相同值时 setter 幂等，无副作用）
            IntervalSlider.Minimum = AppConstants.MinReminderIntervalSeconds;
            IntervalSlider.Maximum = AppConstants.MaxReminderIntervalSeconds;
            IntervalSlider.Value = _reminderService.IntervalSeconds;
            IntervalValueText.Text = $"{_reminderService.IntervalSeconds} 秒";

            SmartIntervalToggle.IsOn = _reminderService.EnableSmartInterval;

            // 初始化暂停时长候选框（预设 + 自定义）
            InitPauseCombo();

            // 应用主题设置（跟随系统/浅色/深色）
            ApplyTheme();

            // 开机自启动状态同步（注册表查询）
            AutoStartToggle.IsOn = StartupManager.IsAutoStartEnabled();

            UpdateStatusText();
            UpdateBlinkCount(_reminderService.BlinkCount);
            UpdateSmartInfo();
        }

        /// <summary>
        /// 应用当前主题模式到窗口根容器。
        /// ElementTheme.Default 即跟随系统，系统切换时自动响应。
        /// </summary>
        private void ApplyTheme()
        {
            var mode = _settings.GetInt(AppConstants.SettingsKeyAppTheme, AppConstants.ThemeDefault);
            RootLayout.RequestedTheme = MapTheme(mode);
            SyncThemeRadios(mode);
        }

        /// <summary>
        /// 将主题模式数值映射为 WinUI 的 ElementTheme
        /// </summary>
        private static ElementTheme MapTheme(int mode)
        {
            switch (mode)
            {
                case AppConstants.ThemeLight:
                    return ElementTheme.Light;
                case AppConstants.ThemeDark:
                    return ElementTheme.Dark;
                default:
                    return ElementTheme.Default;
            }
        }

        /// <summary>
        /// 同步外观单选按钮的选中状态
        /// </summary>
        /// <param name="mode">主题模式数值</param>
        private void SyncThemeRadios(int mode)
        {
            ThemeSystemRadio.IsChecked = mode == AppConstants.ThemeFollowSystem;
            ThemeLightRadio.IsChecked = mode == AppConstants.ThemeLight;
            ThemeDarkRadio.IsChecked = mode == AppConstants.ThemeDark;
        }

        /// <summary>
        /// 外观单选按钮切换：应用主题并持久化
        /// </summary>
        private void OnThemeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radio || radio.IsChecked != true)
            {
                return;
            }

            int mode;
            if (radio == ThemeSystemRadio)
            {
                mode = AppConstants.ThemeFollowSystem;
            }
            else if (radio == ThemeLightRadio)
            {
                mode = AppConstants.ThemeLight;
            }
            else
            {
                mode = AppConstants.ThemeDark;
            }

            _settings.SetInt(AppConstants.SettingsKeyAppTheme, mode);
            RootLayout.RequestedTheme = MapTheme(mode);
            Logger.Info($"主题已切换为模式 {mode}");
        }

        /// <summary>
        /// 开机自启动开关切换：写入/删除注册表 Run 键
        /// </summary>
        private void OnAutoStartToggled(object sender, RoutedEventArgs e)
        {
            StartupManager.SetAutoStart(AutoStartToggle.IsOn);
        }

        /// <summary>
        /// 初始化暂停时长候选框：预设项（3/5/10/…/60 分钟）+ "自定义…"项，并同步当前值
        /// </summary>
        private void InitPauseCombo()
        {
            PauseCombo.Items.Clear();
            foreach (var minutes in AppConstants.PauseDurationOptions)
            {
                PauseCombo.Items.Add(new ComboBoxItem { Content = $"{minutes} 分钟", Tag = minutes });
            }

            PauseCombo.Items.Add(new ComboBoxItem
            {
                Content = "自定义…",
                Tag = AppConstants.MinCustomPauseMinutes - 1
            });

            SyncPauseComboSelection();
        }

        /// <summary>
        /// 将当前暂停时长同步到候选框的选中状态。
        /// 若当前值在预设中则选中对应项；否则选中"自定义…"并显示输入框。
        /// </summary>
        private void SyncPauseComboSelection()
        {
            foreach (ComboBoxItem item in PauseCombo.Items)
            {
                if (item.Tag is int minutes && minutes == _pauseMinutes)
                {
                    PauseCombo.SelectedItem = item;
                    PauseCustomBox.Visibility = Visibility.Collapsed;
                    return;
                }
            }

            // 当前值不在预设中：选中"自定义…"并回填输入框
            PauseCombo.SelectedItem = PauseCombo.Items[^1];
            PauseCustomBox.Visibility = Visibility.Visible;
            PauseCustomBox.Value = _pauseMinutes;
        }

        /// <summary>
        /// 暂停时长候选框选择变化
        /// </summary>
        private void OnPauseSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PauseCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int minutes)
            {
                return;
            }

            if (minutes >= AppConstants.MinCustomPauseMinutes)
            {
                // 选中的是预设值
                PauseCustomBox.Visibility = Visibility.Collapsed;
                SetPauseMinutes(minutes);
            }
            else
            {
                // 选中的是"自定义…"：显示输入框并回填当前值
                PauseCustomBox.Visibility = Visibility.Visible;
                PauseCustomBox.Value = _pauseMinutes;
            }
        }

        /// <summary>
        /// 自定义暂停分钟输入变化
        /// </summary>
        private void OnPauseCustomChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // 输入框隐藏时（未选中"自定义…"）忽略；空值/非法值忽略
            if (sender.Visibility != Visibility.Visible || double.IsNaN(args.NewValue) || args.NewValue < 1)
            {
                return;
            }

            SetPauseMinutes((int)Math.Round(args.NewValue));
        }

        /// <summary>
        /// 应用暂停时长：钳制范围、持久化并刷新按钮文案
        /// </summary>
        /// <param name="minutes">暂停分钟数</param>
        private void SetPauseMinutes(int minutes)
        {
            _pauseMinutes = Math.Clamp(
                minutes,
                AppConstants.MinCustomPauseMinutes,
                AppConstants.MaxCustomPauseMinutes);
            _settings.SetInt(AppConstants.SettingsKeyPauseMinutes, _pauseMinutes);
            UpdatePauseButton();
        }

        /// <summary>
        /// 标题栏鼠标按下：记录拖动起点（鼠标屏幕位置 + 窗口当前屏幕位置），捕获指针。
        /// 之前用 WM_NCLBUTTONDOWN + HTCAPTION 让系统进入拖动循环，但 WinUI 3 的 PointerReleased
        /// 不会发出对应的 WM_LBUTTONUP，导致系统认为鼠标一直按着不放（光标卡在"按压"状态）。
        /// 改为手动追踪 PointerMoved / PointerReleased + AppWindow.Move，最稳定。
        /// </summary>
        private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 仅响应左键按下；右键/中键不触发拖动
            var pointerPoint = e.GetCurrentPoint(AppTitleBar);
            if (!pointerPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var appWindow = AppWindow;
            if (appWindow == null)
            {
                return;
            }

            _isDragging = true;
            _dragStartCursorX = cursor.X;
            _dragStartCursorY = cursor.Y;
            _dragStartWindowX = appWindow.Position.X;
            _dragStartWindowY = appWindow.Position.Y;

            // 捕获指针，确保鼠标移出标题栏区域仍能持续收到 PointerMoved / PointerReleased
            AppTitleBar.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        /// <summary>
        /// 标题栏鼠标移动：根据光标位移平移窗口
        /// </summary>
        private void OnTitleBarPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var newX = _dragStartWindowX + (cursor.X - _dragStartCursorX);
            var newY = _dragStartWindowY + (cursor.Y - _dragStartCursorY);
            AppWindow?.Move(new PointInt32(newX, newY));
            e.Handled = true;
        }

        /// <summary>
        /// 标题栏鼠标释放：结束拖动，释放指针捕获
        /// </summary>
        private void OnTitleBarPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            AppTitleBar.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        /// <summary>
        /// 最小化按钮：隐藏到托盘
        /// </summary>
        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            HideWindow();
            Logger.Info("用户点击最小化按钮，窗口已隐藏到托盘");
        }

        /// <summary>
        /// 关闭按钮：隐藏到托盘（不退出应用）
        /// </summary>
        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            HideWindow();
            Logger.Info("用户点击关闭按钮，窗口已隐藏到托盘");
        }

        /// <summary>
        /// 立即眨眼按钮：手动触发一次提醒
        /// </summary>
        private void OnBlinkNowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _reminderService.TriggerNow();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "手动触发眨眼失败");
            }
        }

        /// <summary>
        /// 暂停/恢复按钮
        /// </summary>
        private void OnPauseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_reminderService.State == ReminderState.Running)
                {
                    _reminderService.PauseForMinutes(_pauseMinutes);
                }
                else
                {
                    _reminderService.Resume();
                }

                UpdatePauseButton();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "暂停/恢复提醒失败");
            }
        }

        /// <summary>
        /// 提醒间隔滑块变化：实时更新数值文本并应用到提醒服务（setter 内部钳制并持久化）
        /// </summary>
        private void OnIntervalChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var seconds = (int)Math.Round(e.NewValue);
            IntervalValueText.Text = $"{seconds} 秒";
            _reminderService.IntervalSeconds = seconds;
        }

        /// <summary>
        /// 智能间隔开关切换：同步到提醒服务（开启后间隔随用眼时长动态调整）
        /// </summary>
        private void OnSmartIntervalToggled(object sender, RoutedEventArgs e)
        {
            _reminderService.EnableSmartInterval = SmartIntervalToggle.IsOn;
            UpdateSmartInfo();
        }

        /// <summary>
        /// 刷新智能间隔信息文本（当前用眼时长、当前间隔与推荐值），并同步滑块：
        /// 智能模式开启时禁用滑块并让滑块跟随实际动态间隔，避免"滑块显示 600s 实际 25s"的误导。
        /// </summary>
        private void UpdateSmartInfo()
        {
            var minutes = _reminderService.ActiveElapsedSeconds / 60;
            var smartEnabled = _reminderService.EnableSmartInterval;
            var currentInterval = _reminderService.IntervalSeconds;

            // 智能模式开启：锁定滑块（禁用）并把滑块值同步到实际间隔；
            // 关闭：恢复可调，值同步为当前间隔
            IntervalSlider.IsEnabled = !smartEnabled;
            if (Math.Abs(IntervalSlider.Value - currentInterval) > 0.01)
            {
                IntervalSlider.Value = currentInterval;
            }
            IntervalValueText.Text = $"{currentInterval} 秒";

            if (smartEnabled)
            {
                var recommended = _reminderService.GetSmartRecommendedInterval();
                SmartInfoText.Text = $"已用眼约 {minutes} 分钟；智能模式已开启，"
                    + $"当前间隔 {currentInterval} 秒（推荐 {recommended} 秒，随用眼时长自动调整）";
            }
            else
            {
                SmartInfoText.Text = $"已用眼约 {minutes} 分钟，手动间隔 {currentInterval} 秒。"
                    + "开启「智能间隔」可随用眼时长自动加密提醒（用眼越久提醒越频繁）。";
            }
        }

        /// <summary>
        /// 视图切换动画（滑入 + 淡入）持续时长（毫秒）
        /// </summary>
        private const int ViewSwitchAnimationMs = 280;

        /// <summary>
        /// 上一次视图切换动画，切换前先停止，避免快速来回切换时残留
        /// </summary>
        private Storyboard? _viewSwitchStoryboard;

        /// <summary>
        /// 切换到设置视图（层进式导航，带滑入动画）。
        /// 标题栏左侧"明眸"标题切换为返回按钮（常驻，不随设置内容滚动消失）。
        /// </summary>
        public void ShowSettingsView()
        {
            MainView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            TitleMainState.Visibility = Visibility.Collapsed;
            TitleBackButton.Visibility = Visibility.Visible;
            AnimateViewSwitch(SettingsView, fromLeft: false);
            SyncSettingsControls();
            Logger.Info("已进入设置视图");
        }

        /// <summary>
        /// 返回到主视图（带滑入动画），标题栏恢复"明眸"标题
        /// </summary>
        private void ShowMainView()
        {
            SettingsView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
            TitleBackButton.Visibility = Visibility.Collapsed;
            TitleMainState.Visibility = Visibility.Visible;
            AnimateViewSwitch(MainView, fromLeft: true);
            Logger.Info("已返回主视图");
        }

        /// <summary>
        /// 播放视图切换动画：目标视图从侧面滑入并淡入。
        /// 进入设置时从右向左滑入；返回主视图时从左向右滑入，符合层进导航观感。
        /// </summary>
        /// <param name="view">要显示的目标视图</param>
        /// <param name="fromLeft">是否从左侧滑入（返回主视图时为 true）</param>
        private void AnimateViewSwitch(UIElement view, bool fromLeft)
        {
            // 停止上一次未完成的切换动画，防止 Opacity/位移残留
            _viewSwitchStoryboard?.Stop();

            var startX = fromLeft ? -24 : 24;
            var translate = new TranslateTransform { X = startX };
            view.RenderTransform = translate;
            view.Opacity = 0;

            var storyboard = new Storyboard();

            var xAnimation = new DoubleAnimation
            {
                From = startX,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(ViewSwitchAnimationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(xAnimation, translate);
            Storyboard.SetTargetProperty(xAnimation, "X");

            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(ViewSwitchAnimationMs)
            };
            Storyboard.SetTarget(opacityAnimation, view);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            storyboard.Children.Add(xAnimation);
            storyboard.Children.Add(opacityAnimation);

            _viewSwitchStoryboard = storyboard;
            storyboard.Begin();
        }

        /// <summary>
        /// 主界面"设置"按钮点击：进入设置视图
        /// </summary>
        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            ShowSettingsView();
        }

        /// <summary>
        /// 设置视图"返回"按钮点击：回到主视图
        /// </summary>
        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            ShowMainView();
        }

        /// <summary>
        /// 提醒触发：仅当主窗口可见时在窗口内播放眨眼动画并更新统计。
        /// 主窗口隐藏时，提醒由 App 层统一以透明小窗呈现（见 App.OnBlinkRequestedForReminder），
        /// 主窗口不再被提醒流程拉出或自动隐藏，保持其独立形态。
        /// </summary>
        private void OnBlinkRequested()
        {
            try
            {
                // 主窗口不可见时由透明提醒小窗负责，这里不处理
                if (!IsWindowVisible())
                {
                    return;
                }

                // 在主线程执行 UI 动画
                DispatcherQueue.TryEnqueue(() =>
                {
                    BlinkAnimation.PlayBlinkAnimation();
                    UpdateBlinkCount(_reminderService.BlinkCount);
                    ShowHintText();
                });
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "处理眨眼提醒失败");
            }
        }

        /// <summary>
        /// 倒计时推进：刷新状态文本与智能间隔信息
        /// </summary>
        private void OnReminderTicked()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStatusText();
                UpdateSmartInfo();
            });
        }

        /// <summary>
        /// 提醒服务状态变化时刷新界面
        /// </summary>
        /// <param name="state">新状态</param>
        private void OnReminderStateChanged(ReminderState state)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStatusText();
                UpdatePauseButton();
            });
        }

        /// <summary>
        /// 更新底部眨眼次数显示
        /// </summary>
        /// <param name="count">当前次数</param>
        private void UpdateBlinkCount(int count)
        {
            BlinkCountTextBlock.Text = $"{count} 次";
        }

        /// <summary>
        /// 更新状态文本（倒计时/暂停/空闲）
        /// </summary>
        private void UpdateStatusText()
        {
            switch (_reminderService.State)
            {
                case ReminderState.Paused:
                    StatusTextBlock.Text = AppConstants.StatusPaused;
                    break;
                case ReminderState.IdlePaused:
                    StatusTextBlock.Text = AppConstants.StatusIdlePaused;
                    break;
                default:
                    StatusTextBlock.Text = string.Format(AppConstants.StatusRunningTemplate, _reminderService.RemainingSeconds);
                    break;
            }
        }

        /// <summary>
        /// 更新暂停按钮文案（使用当前配置的暂停时长）
        /// </summary>
        private void UpdatePauseButton()
        {
            PauseButton.Content = _reminderService.State == ReminderState.Running
                ? $"暂停 {_pauseMinutes} 分钟"
                : "恢复提醒";
        }

        /// <summary>
        /// 提示文本淡入显示持续时间（毫秒）
        /// </summary>
        private const int HintDisplayDurationMs = 2000;

        /// <summary>
        /// 显示眨眼完成后的提示文本，并在 2 秒后淡出
        /// </summary>
        private void ShowHintText()
        {
            HintTextBlock.Text = AppConstants.BlinkHintText;
            HintTextBlock.Opacity = 1;

            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(HintDisplayDurationMs);
                HintTextBlock.Opacity = 0;
            });
        }
    }
}
