// ============================================================================
// 文件：Core/TransparentBackdrop.cs
// 用途：真正透明的窗口背景（自定义 SystemBackdrop）。
//       参考 WinUIEx 的 TransparentTintBackdrop（github.com/dotMorten/WinUIEx）。
// 机制：
//   1) OnTargetConnected：把 connectedTarget.SystemBackdrop 设为透明画刷——
//      注意该属性类型是 Windows.UI.Composition.CompositionBrush（非 Microsoft.UI 类型），
//      必须用 new Windows.UI.Composition.Compositor() 创建画刷（1.6 起可用）；
//   2) DWM：DwmExtendFrameIntoClientArea + 去圆角/去描边 + 移除 Win32 边框样式位；
//   3) WM_ERASEBKGND：用黑色画刷填充背景（防止默认白/灰底，配合 alpha=0 画刷实现透明）。
// 适用：WinUI 3 / Windows App SDK 1.6+（无需升级 SDK）。
// ============================================================================

using System;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MingMou.Core;

namespace MingMou
{
    /// <summary>
    /// 让窗口背景完全透明的 SystemBackdrop 实现。
    /// 用法：window.SystemBackdrop = new TransparentBackdrop(hwnd);
    /// </summary>
    public sealed class TransparentBackdrop : SystemBackdrop
    {
        /// <summary>
        /// 共享的 Windows.UI.Composition 合成器（WinUI 3 桌面环境下可直接 new）
        /// </summary>
        private static readonly Windows.UI.Composition.Compositor SharedCompositor = new();

        private readonly IntPtr _hwnd;

        /// <summary>
        /// 透明画刷（alpha=0，窗口背景完全透明）
        /// </summary>
        private Windows.UI.Composition.CompositionColorBrush? _brush;

        /// <summary>
        /// 子类化回调委托引用（防 GC）
        /// </summary>
        private NativeMethods.SUBCLASSPROC? _subclassProc;

        /// <summary>
        /// GDI 黑色背景画刷（WM_ERASEBKGND 时填充）
        /// </summary>
        private IntPtr _backgroundBrush = IntPtr.Zero;

        /// <summary>
        /// 创建透明背景
        /// </summary>
        /// <param name="hwnd">窗口句柄（用于 DWM 配置与消息子类化）</param>
        public TransparentBackdrop(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        /// <inheritdoc />
        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            // 顺序注意：先设置透明画刷，再调基类（基类会初始化默认配置，不会覆盖画刷）
            _brush = SharedCompositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
            connectedTarget.SystemBackdrop = _brush;

            base.OnTargetConnected(connectedTarget, xamlRoot);

            // DWM 扩展帧到整个客户区（消除边缘色带；注意：不用 DwmEnableBlurBehindWindow——
            // 它会把客户区变成模糊背景层并吞掉 XAML 内容，且用户要的是"透明不模糊"）
            var margins = new NativeMethods.MARGINS { cxLeftWidth = -1 };
            NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // 去掉 Win11 的圆角轮廓与系统描边（透明窗口残留"奇怪边框"的来源）
            try
            {
                int cornerPref = NativeMethods.DWMWCP_DONOTROUND;
                NativeMethods.DwmSetWindowAttribute(_hwnd, 33, ref cornerPref, 4);
                int borderColor = NativeMethods.DWMWA_COLOR_NONE;
                NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref borderColor, 4);
            }
            catch
            {
                // 旧系统不支持这些 DWM 属性时忽略（尽力而为）
            }

            // 彻底移除 Win32 层边框样式位并刷新（消除 1px 系统白边）
            NativeMethods.RemoveStandardWindowFrame(_hwnd);
            NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_FRAMECHANGED | 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0040);

            // 子类化：WM_ERASEBKGND 时用黑色填充（防止默认白/灰底）
            _subclassProc = OnWindowSubclass;
            NativeMethods.SetWindowSubclass(_hwnd, _subclassProc, 0, 0);

            // 立即擦除一次初始背景（黑色），确保无残留底色
            var hdc = NativeMethods.GetDC(_hwnd);
            if (hdc != IntPtr.Zero)
            {
                var rect = new NativeMethods.RECT();
                NativeMethods.GetClientRect(_hwnd, out rect);
                EnsureBackgroundBrush();
                NativeMethods.FillRect(hdc, ref rect, _backgroundBrush);
                NativeMethods.ReleaseDC(_hwnd, hdc);
            }
        }

        /// <inheritdoc />
        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            disconnectedTarget.SystemBackdrop = null;
            _brush?.Dispose();
            _brush = null;

            if (_backgroundBrush != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(_backgroundBrush);
                _backgroundBrush = IntPtr.Zero;
            }

            _subclassProc = null;
            base.OnTargetDisconnected(disconnectedTarget);
        }

        /// <summary>
        /// 窗口消息回调：拦截背景擦除，用黑色画刷填充（配合透明画刷实现透明背景）
        /// </summary>
        private int OnWindowSubclass(
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData)
        {
            if (uMsg == NativeMethods.WM_ERASEBKGND)
            {
                var rect = new NativeMethods.RECT();
                NativeMethods.GetClientRect(hWnd, out rect);
                EnsureBackgroundBrush();
                NativeMethods.FillRect(wParam, ref rect, _backgroundBrush);
                return 1; // 已处理
            }

            return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        /// <summary>
        /// 确保黑色背景画刷已创建（懒创建）
        /// </summary>
        private void EnsureBackgroundBrush()
        {
            if (_backgroundBrush == IntPtr.Zero)
            {
                _backgroundBrush = NativeMethods.CreateSolidBrush(0); // 黑色
            }
        }
    }
}
