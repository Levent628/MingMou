// ============================================================================
// 文件：Controls/BlinkControl.xaml.cs
// 用途：眨眼动画控件的后台代码，负责启动 Storyboard 动画并对外暴露播放接口
// ============================================================================

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using MingMou.Core;

namespace MingMou.Controls
{
    /// <summary>
    /// 眨眼动画控件
    /// 展示一对抽象眼睛，并通过 Storyboard 驱动眨眼动画
    /// </summary>
    public sealed partial class BlinkControl : UserControl
    {
        /// <summary>
        /// 初始化眨眼控件
        /// </summary>
        public BlinkControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 播放一次眨眼动画
        /// </summary>
        public void PlayBlinkAnimation()
        {
            try
            {
                // 如果动画正在播放，先停止并回到初始状态，避免重叠播放导致异常
                if (BlinkStoryboard.GetCurrentState() == ClockState.Active)
                {
                    BlinkStoryboard.Stop();
                }

                BlinkStoryboard.Begin();
                Logger.Debug("眨眼动画开始播放");
            }
            catch (System.Exception ex)
            {
                Logger.Exception(ex, "播放眨眼动画失败");
            }
        }

        /// <summary>
        /// Storyboard 动画完成回调
        /// </summary>
        private void OnBlinkAnimationCompleted(object sender, object e)
        {
            Logger.Debug("眨眼动画播放完成");
        }
    }
}
