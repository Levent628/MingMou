using Microsoft.Windows.ApplicationModel.DynamicDependency;
using System;
using WinRT;

namespace MingMou
{
    /// <summary>
    /// 应用程序入口点。
    /// </summary>
    /// <remarks>
    /// 打包模式（MSIX）由包依赖提供 Windows App SDK 运行时，无需手动初始化；
    /// 非打包模式必须在 <see cref="Microsoft.UI.Xaml.Application.Start"/> 之前调用
    /// Bootstrap.Initialize 注册 Microsoft.UI.Xaml 等 WinRT 组件，否则启动时会抛出
    /// REGDB_E_CLASSNOTREG（没有注册类）。
    /// 该文件在 csproj 中通过 DISABLE_XAML_GENERATED_MAIN 取代 XAML 编译器自动生成的 Main。
    /// </remarks>
    public static class Program
    {
        /// <summary>
        /// 应用程序主入口。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
#if UNPACKAGED && !WINDOWSAPPSDK_SELF_CONTAINED
            // 非打包 + 框架依赖模式：初始化 Windows App SDK 运行时。
            // 0x00010006 编码了目标运行时的主/次版本（1.6），详见 Bootstrap.Initialize 文档。
            // 该调用会动态注册 Microsoft.UI.Xaml 等 WinRT 组件，使 COM/WinRT 激活可用。
            // 注意：自包含部署（WINDOWSAPPSDK_SELF_CONTAINED）下运行时 DLL 随应用分发，
            // 应用目录内不含 WindowsAppRuntime.Bootstrap.dll，因此这里必须跳过 Bootstrap API。
            Bootstrap.Initialize(0x00010006);
#endif
            ComWrappersSupport.InitializeComWrappers();

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });

#if UNPACKAGED && !WINDOWSAPPSDK_SELF_CONTAINED
            // 释放对 Windows App SDK 运行时的动态依赖。
            Bootstrap.Shutdown();
#endif
        }
    }
}
