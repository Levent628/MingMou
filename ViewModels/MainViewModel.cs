// ============================================================================
// 文件：ViewModels/MainViewModel.cs
// 用途：主窗口的数据绑定层，当前为预留 ViewModel，便于后续扩展
// ============================================================================

using MingMou.Core;

namespace MingMou.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// 目前主要承担状态属性的集中管理，UI 控件可直接绑定这些属性
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        private int _blinkCount;
        private int _remainingSeconds;
        private string _statusText = string.Empty;
        private string _hintText = string.Empty;
        private bool _isPaused;

        /// <summary>
        /// 今日累计眨眼次数
        /// </summary>
        public int BlinkCount
        {
            get => _blinkCount;
            set => SetProperty(ref _blinkCount, value);
        }

        /// <summary>
        /// 距离下一次提醒的剩余秒数
        /// </summary>
        public int RemainingSeconds
        {
            get => _remainingSeconds;
            set => SetProperty(ref _remainingSeconds, value);
        }

        /// <summary>
        /// 状态文本（倒计时/暂停/空闲）
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// 眨眼完成后的提示文本
        /// </summary>
        public string HintText
        {
            get => _hintText;
            set => SetProperty(ref _hintText, value);
        }

        /// <summary>
        /// 当前是否处于暂停状态
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }
    }
}
