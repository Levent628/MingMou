// ============================================================================
// 文件：Core/NativeMethods.cs
// 用途：封装 Win32 API 与 DWM 相关的 P/Invoke 调用，用于窗口拖动、圆角与空闲检测
// ============================================================================

using System;
using System.Runtime.InteropServices;

namespace MingMou.Core
{
    /// <summary>
    /// DWM 窗口圆角偏好枚举
    /// </summary>
    public enum DwmWindowCornerPreference
    {
        /// <summary>使用系统默认圆角策略</summary>
        Default = 0,

        /// <summary>不应用圆角</summary>
        DoNotRound = 1,

        /// <summary>应用圆角</summary>
        Round = 2,

        /// <summary>应用较小的圆角</summary>
        RoundSmall = 3
    }

    /// <summary>
    /// Win32 互操作帮助类
    /// </summary>
    public static class NativeMethods
    {
        // ------------------------------------------------------------------
        // 窗口拖动相关 API（已废弃：主窗口现用手动指针拖动方案，见 MainWindow 标题栏事件；
        // 保留 SendMessage/ReleaseCapture 作为通用 Win32 工具，未来如需系统级拖动可复用）
        // ------------------------------------------------------------------

        /// <summary>
        /// 向指定窗口发送消息
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 释放当前线程的鼠标捕获
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        // ------------------------------------------------------------------
        // DWM 圆角 API
        // ------------------------------------------------------------------
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        /// <summary>
        /// 设置指定窗口的 DWM 属性
        /// </summary>
        [DllImport("dwmapi.dll", CharSet = CharSet.Auto, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int pvAttribute,
            uint cbAttribute);

        // ------------------------------------------------------------------
        // 鼠标点击穿透 API（提醒小窗不接收任何鼠标输入，避免与游戏/应用抢鼠标）
        // ------------------------------------------------------------------
        internal const int GWL_EXSTYLE = -20;

        /// <summary>
        /// 扩展样式：窗口不参与鼠标点击（点击穿透到下层窗口）
        /// </summary>
        internal const int WS_EX_TRANSPARENT = 0x00000020;

        /// <summary>
        /// 获取窗口扩展样式
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        /// <summary>
        /// 设置窗口扩展样式
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// 让窗口鼠标点击穿透（纯展示窗口专用，如提醒小窗——不打断用户正在进行的
        /// 游戏/应用操作，鼠标事件直接落到下层窗口）。
        /// </summary>
        /// <param name="windowHandle">窗口句柄 HWND</param>
        public static void MakeClickThrough(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
        }

        // ------------------------------------------------------------------
        // 透明窗口 API（WinUI 3 自定义透明 SystemBackdrop 方案）
        // ------------------------------------------------------------------
        internal const int GWL_STYLE = -16;

        /// <summary>标题栏样式位</summary>
        internal const int WS_CAPTION = 0x00C00000;

        /// <summary>可调整大小边框样式位</summary>
        internal const int WS_THICKFRAME = 0x00040000;

        /// <summary>最小化按钮样式位</summary>
        internal const int WS_MINIMIZEBOX = 0x00020000;

        /// <summary>最大化按钮样式位</summary>
        internal const int WS_MAXIMIZEBOX = 0x00010000;

        /// <summary>系统菜单样式位</summary>
        internal const int WS_SYSMENU = 0x00080000;

        /// <summary>
        /// 彻底移除标准窗口边框相关样式位（配合 SWP_FRAMECHANGED 刷新生效），
        /// 用于消除无边框透明窗口残留的 1px 系统白边
        /// </summary>
        /// <param name="windowHandle">窗口句柄 HWND</param>
        public static void RemoveStandardWindowFrame(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            const int borderlessMask = WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
            var style = GetWindowLong(windowHandle, GWL_STYLE);
            SetWindowLong(windowHandle, GWL_STYLE, style & ~borderlessMask);
        }

        /// <summary>
        /// 工具窗口扩展样式：不参与 Alt+Tab 切换器展示（提醒小窗不需要）
        /// </summary>
        internal const int WS_EX_TOOLWINDOW = 0x00000080;

        /// <summary>
        /// SetWindowPos 标志：触发窗口样式刷新（样式修改后必须调用）
        /// </summary>
        internal const uint SWP_FRAMECHANGED = 0x0020;

        /// <summary>
        /// 窗口角偏好值：关闭系统圆角
        /// </summary>
        internal const int DWMWCP_DONOTROUND = 1;

        /// <summary>
        /// 窗口描边颜色值：无描边
        /// </summary>
        internal const int DWMWA_COLOR_NONE = -2;

        /// <summary>
        /// DWM 边框颜色属性 ID
        /// </summary>
        internal const int DWMWA_BORDER_COLOR = 34;

        /// <summary>
        /// 获取窗口客户区 DC（用于立即擦除背景）
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        /// <summary>
        /// 释放 DC
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        /// <summary>
        /// 背景擦除消息：子类化时填充黑色（配合透明画刷避免默认底色）
        /// </summary>
        internal const uint WM_ERASEBKGND = 0x0014;

        /// <summary>
        /// 窗口子类化回调委托（必须持有强引用防止被 GC 回收）
        /// </summary>
        internal delegate int SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData);

        /// <summary>
        /// 注册窗口子类回调（拦截 WM_ERASEBKGND 等消息）
        /// </summary>
        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        internal static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, uint dwRefData);

        /// <summary>
        /// 调用默认子类处理（返回值与 SUBCLASSPROC 回调一致）
        /// </summary>
        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        internal static extern int DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// DWM 扩展帧边距结构（-1 表示扩展到整个客户区）
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        /// <summary>
        /// 将窗口客户区扩展为无边距（消除边缘色带）
        /// </summary>
        [DllImport("dwmapi.dll", CharSet = CharSet.Auto)]
        internal static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        /// <summary>
        /// 创建纯色 GDI 画刷（用于擦背景时填充洋红色）
        /// </summary>
        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr CreateSolidBrush(int crColor);

        /// <summary>
        /// 释放 GDI 对象
        /// </summary>
        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        internal static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// 用画刷填充矩形区域（注意：FillRect 属于 user32.dll，不在 gdi32！）
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool FillRect(IntPtr hdc, ref RECT lprc, IntPtr hbr);

        /// <summary>
        /// 获取客户区矩形
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// 矩形结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // ------------------------------------------------------------------
        // 空闲检测 API：GetLastInputInfo
        // ------------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        internal struct LASTINPUTINFO
        {
            /// <summary>结构体大小（字节）</summary>
            public uint cbSize;

            /// <summary>上次输入事件发生时系统的运行时间（毫秒）</summary>
            public uint dwTime;
        }

        /// <summary>
        /// 获取上次输入事件的时间信息
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>
        /// 获取系统启动以来的运行时间（毫秒）
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern uint GetTickCount();

        // ------------------------------------------------------------------
        // 窗口可见性检测 API
        // ------------------------------------------------------------------

        /// <summary>
        /// 判断指定窗口当前是否可见
        /// WinUI 3 的 AppWindow 没有公开的 Visible 属性，改用 Win32 判断最可靠
        /// </summary>
        /// <param name="hWnd">窗口句柄 HWND</param>
        /// <returns>可见返回 true</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        // ------------------------------------------------------------------
        // 鼠标光标位置 API（用于自定义标题栏手动拖动窗口）
        // ------------------------------------------------------------------

        /// <summary>
        /// Win32 POINT 结构（屏幕坐标）
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            /// <summary>横坐标（屏幕像素）</summary>
            public int X;

            /// <summary>纵坐标（屏幕像素）</summary>
            public int Y;
        }

        /// <summary>
        /// 获取鼠标光标在屏幕坐标系下的位置
        /// </summary>
        /// <param name="lpPoint">输出的屏幕坐标</param>
        /// <returns>成功返回 true</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        // ------------------------------------------------------------------
        // 前台窗口与焦点 API（托盘打开窗口时绕过 Windows 前台锁）
        // ------------------------------------------------------------------

        /// <summary>
        /// 获取当前前台窗口句柄
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// 获取指定窗口所属线程与进程 ID
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// 获取当前线程 ID
        /// </summary>
        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        /// <summary>
        /// 将线程输入队列附加到另一线程，使本线程可以设置前台窗口（前台锁绕过的基础）
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(
            uint idAttach,
            uint idAttachTo,
            [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        /// <summary>
        /// 将窗口设为前台窗口
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// 强制将窗口带到前台。
        /// 用户刚操作其他软件时，Windows 的前台窗口锁会拒绝普通 SetForegroundWindow；
        /// 通过 AttachThreadInput 将本线程输入队列附加到前台线程后再设置，可可靠绕开限制。
        /// </summary>
        /// <param name="windowHandle">窗口句柄 HWND</param>
        public static void ForceForeground(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var foreground = GetForegroundWindow();
                if (foreground == windowHandle)
                {
                    return;
                }

                var foregroundThread = GetWindowThreadProcessId(foreground, out _);
                var currentThread = GetCurrentThreadId();
                var attached = foregroundThread != currentThread
                    && AttachThreadInput(currentThread, foregroundThread, true);

                try
                {
                    _ = SetForegroundWindow(windowHandle);
                }
                finally
                {
                    if (attached)
                    {
                        _ = AttachThreadInput(currentThread, foregroundThread, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "强制窗口置前失败");
            }
        }

        // ------------------------------------------------------------------
        // 窗口置顶 API（用于提醒弹窗"浮到最前但不抢焦点"）
        // ------------------------------------------------------------------

        /// <summary>置顶句柄常量：置于所有窗口之上</summary>
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        /// <summary>取消置顶句柄常量：恢复正常 z 序</summary>
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        /// <summary>不移动窗口位置</summary>
        private const uint SWP_NOMOVE = 0x0001;

        /// <summary>不改变窗口大小</summary>
        private const uint SWP_NOSIZE = 0x0002;

        /// <summary>不激活窗口（关键：置顶但不抢键盘/鼠标焦点）</summary>
        private const uint SWP_NOACTIVATE = 0x0010;

        /// <summary>
        /// 改变窗口的大小、位置和 Z 序
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        /// <summary>
        /// 将窗口浮到最顶层且不抢焦点。
        /// 用于提醒弹窗：用户正在操作其他软件时，Windows 的前台窗口锁会拒绝
        /// Activate 抢占前台，导致提醒窗口被盖住；置顶（TOPMOST）+ 不激活
        /// （NOACTIVATE）则能让提醒浮到最上层展示，同时不打断用户当前输入。
        /// </summary>
        /// <param name="windowHandle">窗口句柄 HWND</param>
        public static void BringToFrontNoActivate(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            _ = SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// 取消窗口置顶，恢复正常 z 序
        /// </summary>
        /// <param name="windowHandle">窗口句柄 HWND</param>
        public static void RemoveTopmost(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            _ = SetWindowPos(windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// 强制设置窗口圆角偏好
        /// </summary>
        /// <param name="windowHandle">窗口句柄 HWND</param>
        /// <param name="preference">圆角偏好</param>
        public static void SetWindowCornerPreference(IntPtr windowHandle, DwmWindowCornerPreference preference)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var value = (int)preference;
                DwmSetWindowAttribute(windowHandle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "设置窗口圆角偏好失败");
            }
        }

        /// <summary>
        /// 获取用户上次键盘或鼠标输入至今已过去的秒数
        /// </summary>
        /// <returns>空闲秒数；调用失败时返回 0</returns>
        public static uint GetIdleSeconds()
        {
            var lastInputInfo = new LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
            };

            if (!GetLastInputInfo(ref lastInputInfo))
            {
                Logger.Error("GetLastInputInfo 调用失败，无法检测空闲状态");
                return 0;
            }

            var tickCount = GetTickCount();
            var idleMilliseconds = tickCount - lastInputInfo.dwTime;

            // 防止系统运行时间过长导致的溢出
            if (idleMilliseconds < 0)
            {
                idleMilliseconds = 0;
            }

            return idleMilliseconds / 1000;
        }
    }
}
