// ============================================================================
// 文件：Core/AppConstants.cs
// 用途：集中管理明眸应用的所有可配置参数与固定字符串，避免代码中散落"魔法数字"
// ============================================================================

namespace MingMou.Core
{
    /// <summary>
    /// 应用常量定义类
    /// 所有可在运行时被用户配置或业务逻辑引用的常量都应集中在此
    /// </summary>
    public static class AppConstants
    {
        #region 窗口尺寸与样式

        /// <summary>
        /// 主窗口宽度（像素）
        /// </summary>
        public const int MainWindowWidth = 400;

        /// <summary>
        /// 主窗口高度（像素）
        /// </summary>
        public const int MainWindowHeight = 560;

        /// <summary>
        /// 主窗口最小宽度（像素）：响应式布局的下限，防止内容过度挤压
        /// </summary>
        public const int MainWindowMinWidth = 320;

        /// <summary>
        /// 主窗口最小高度（像素）
        /// </summary>
        public const int MainWindowMinHeight = 440;

        /// <summary>
        /// 透明提醒小窗宽度（设计像素 epx）
        /// 仅容纳眨眼动画（168 宽）+ 呼吸缩放余量，最小化对屏幕内容的遮挡；
        /// 运行时由 ReminderPopupWindow 按显示缩放（DPI）换算为物理像素
        /// </summary>
        public const int ReminderPopupWidth = 200;

        /// <summary>
        /// 透明提醒小窗高度（设计像素 epx）
        /// 内容只有眨眼动画（高 100），无多余文字
        /// </summary>
        public const int ReminderPopupHeight = 120;

        /// <summary>
        /// 提醒弹窗与屏幕边缘的间距（像素）
        /// </summary>
        public const int ReminderWindowMargin = 16;

        /// <summary>
        /// 窗口圆角半径
        /// </summary>
        public const int WindowCornerRadius = 12;

        /// <summary>
        /// 控件通用圆角半径（单位：像素）
        /// </summary>
        public const int ControlCornerRadius = 8;

        #endregion

        #region 眨眼提醒间隔与动画

        /// <summary>
        /// 默认眨眼提醒间隔（秒），符合医学建议的每 25 秒眨眼一次
        /// </summary>
        public const int ReminderIntervalSeconds = 25;

        /// <summary>
        /// 眨眼提醒间隔的最小值（秒）
        /// </summary>
        public const int MinReminderIntervalSeconds = 10;

        /// <summary>
        /// 眨眼提醒间隔的最大值（秒）
        /// </summary>
        public const int MaxReminderIntervalSeconds = 600;

        #endregion

        #region 智能间隔（基于眼科医学依据）

        /// <summary>
        /// 智能间隔基线：开始用眼阶段的推荐提醒间隔（秒）。
        /// 依据：视屏终端（VDT）研究显示用屏时眨眼频率显著下降，
        /// 保持有意识的规律眨眼有助于泪膜稳定、缓解视疲劳与干眼。
        /// </summary>
        public const int SmartIntervalBaselineSeconds = 25;

        /// <summary>
        /// 连续用眼 20 分钟（20-20-20 法则阈值）后的推荐提醒间隔（秒）。
        /// 20-20-20 法则（美国眼科学会 AAO）：每用眼 20 分钟，远眺 20 英尺（约 6 米）外 20 秒。
        /// 用眼越久眨眼频率越低，提醒适当加密。
        /// </summary>
        public const int SmartIntervalMediumSeconds = 20;

        /// <summary>
        /// 连续用眼 40 分钟后的推荐提醒间隔（秒）
        /// </summary>
        public const int SmartIntervalHighSeconds = 15;

        /// <summary>
        /// 连续用眼 60 分钟及以上（长时用眼上限）的推荐提醒间隔（秒）
        /// </summary>
        public const int SmartIntervalMaxSeconds = 10;

        /// <summary>
        /// 20-20-20 法则的用眼阈值（分钟）
        /// </summary>
        public const int TwentyTwentyTwentyMinutes = 20;

        /// <summary>
        /// 智能间隔阶梯阈值：连续用眼超过该分钟数后间隔再次加密
        /// </summary>
        public const int SmartIntervalLongSessionMinutes = 40;

        /// <summary>
        /// 智能间隔阶梯阈值：连续用眼超过该分钟数后进入最长提醒强度
        /// </summary>
        public const int SmartIntervalExtendedSessionMinutes = 60;

        /// <summary>
        /// 智能间隔模式的默认状态（默认开启：随用眼时长动态推荐更贴合护眼场景）
        /// </summary>
        public const bool EnableSmartIntervalDefault = true;

        #endregion

        #region 眨眼动画参数

        /// <summary>
        /// 眨眼动画总时长（毫秒）
        /// </summary>
        public const double BlinkAnimationDurationMs = 600.0;

        /// <summary>
        /// 眨眼动画中眼睑闭合到位的关键时间点（毫秒）
        /// </summary>
        public const double BlinkCloseKeyTimeMs = 300.0;

        /// <summary>
        /// 呼吸效果最大缩放比例（1.0 -> 1.06）
        /// </summary>
        public const double BreathMaxScale = 1.06;

        #endregion

        #region 空闲检测

        /// <summary>
        /// 判定用户为"空闲"的连续无操作时间阈值（秒）
        /// 达到该阈值后自动暂停提醒，恢复操作后继续计时
        /// 取值说明：300 秒（5 分钟）——护眼场景下用户经常"盯着屏幕不动"
        ///（看视频、阅读、思考），阈值过短会把正常用机误判为空闲并冻结提醒，
        /// 表现为"偶发不弹提醒"；5 分钟接近 Windows 默认锁屏待机时间，
        /// 只有"真正离开电脑"才会暂停。
        /// </summary>
        public const int IdleThresholdSeconds = 300;

        /// <summary>
        /// 空闲检测的心跳间隔（毫秒）
        /// </summary>
        public const int IdleCheckIntervalMs = 1000;

        #endregion

        #region 暂停与托盘

        /// <summary>
        /// 暂停时长的默认值（分钟）
        /// </summary>
        public const int PauseDurationMinutes = 30;

        /// <summary>
        /// 暂停时长的预设候选（分钟），另支持"自定义"输入
        /// </summary>
        public static readonly int[] PauseDurationOptions = { 3, 5, 10, 15, 20, 30, 45, 60 };

        /// <summary>
        /// 自定义暂停时长的最小值（分钟）
        /// </summary>
        public const int MinCustomPauseMinutes = 1;

        /// <summary>
        /// 自定义暂停时长的最大值（分钟）
        /// </summary>
        public const int MaxCustomPauseMinutes = 600;

        /// <summary>
        /// LocalSettings 中存储"暂停时长（分钟）"的键名
        /// </summary>
        public const string SettingsKeyPauseMinutes = "PauseDurationMinutes";

        /// <summary>
        /// 应用启动后是否自动显示主窗口
        /// </summary>
        public const bool ShowMainWindowOnStartup = false;

        /// <summary>
        /// 提醒触发时是否自动弹出主窗口
        /// </summary>
        public const bool AutoShowWindowOnReminder = true;

        /// <summary>
        /// 提醒触发后自动隐藏主窗口的延迟时间（秒）
        /// </summary>
        public const int AutoHideWindowDelaySeconds = 4;

        #endregion

        #region 存储与日志

        /// <summary>
        /// 应用本地数据文件夹名称
        /// </summary>
        public const string AppDataFolderName = "MingMou";

        /// <summary>
        /// 日志文件存放子目录名称
        /// </summary>
        public const string LogsSubFolderName = "Logs";

        /// <summary>
        /// 日志文件命名格式前缀
        /// </summary>
        public const string LogFileNamePrefix = "log_";

        /// <summary>
        /// 日志文件扩展名
        /// </summary>
        public const string LogFileExtension = ".txt";

        /// <summary>
        /// 非打包模式下 JSON 设置文件的名称
        /// </summary>
        public const string SettingsFileName = "settings.json";

        #endregion

        #region 设置键名

        /// <summary>
        /// LocalSettings 中存储提醒间隔的键名
        /// </summary>
        public const string SettingsKeyReminderInterval = "ReminderIntervalSeconds";

        /// <summary>
        /// LocalSettings 中存储今日眨眼次数的键名
        /// </summary>
        public const string SettingsKeyBlinkCount = "BlinkCount";

        /// <summary>
        /// LocalSettings 中存储最后一次眨眼日期的键名（用于跨天重置计数）
        /// </summary>
        public const string SettingsKeyBlinkDate = "BlinkCountDate";

        /// <summary>
        /// LocalSettings 中存储"提醒时自动弹出窗口"开关的键名
        /// </summary>
        public const string SettingsKeyAutoShowOnReminder = "AutoShowOnReminder";

        /// <summary>
        /// LocalSettings 中存储"智能间隔模式"开关的键名
        /// </summary>
        public const string SettingsKeySmartInterval = "EnableSmartInterval";

        /// <summary>
        /// LocalSettings 中存储"应用主题"的键名（值见 ThemeFollowSystem / ThemeLight / ThemeDark）
        /// </summary>
        public const string SettingsKeyAppTheme = "AppTheme";

        #endregion

        #region 主题

        /// <summary>主题模式：跟随系统</summary>
        public const int ThemeFollowSystem = 0;

        /// <summary>主题模式：强制浅色</summary>
        public const int ThemeLight = 1;

        /// <summary>主题模式：强制深色</summary>
        public const int ThemeDark = 2;

        /// <summary>
        /// 默认主题模式（跟随系统）
        /// </summary>
        public const int ThemeDefault = ThemeFollowSystem;

        #endregion

        #region 开机自启动

        /// <summary>
        /// 注册表"运行"键路径（当前用户，无需管理员权限）
        /// </summary>
        public const string AutoStartRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// 注册表"运行"键下的值名称
        /// </summary>
        public const string AutoStartValueName = "MingMou";

        #endregion

        #region 界面文案

        /// <summary>
        /// 应用显示名称
        /// </summary>
        public const string AppDisplayName = "明眸";

        /// <summary>
        /// 眨眼动画结束后显示的提示语
        /// </summary>
        public const string BlinkHintText = "已眨眼，继续加油 👀";

        /// <summary>
        /// 服务暂停时的状态文本
        /// </summary>
        public const string StatusPaused = "已暂停";

        /// <summary>
        /// 服务空闲暂停时的状态文本
        /// </summary>
        public const string StatusIdlePaused = "空闲中，已自动暂停";

        /// <summary>
        /// 服务运行中的状态文本模板
        /// </summary>
        public const string StatusRunningTemplate = "下次提醒：{0} 秒";

        #endregion
    }
}
