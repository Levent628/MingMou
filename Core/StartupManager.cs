// ============================================================================
// 文件：Core/StartupManager.cs
// 用途：开机自启动管理（注册表 Run 键实现，非打包模式的标准简单方案）
// ============================================================================

using System;
using Microsoft.Win32;
using MingMou.Core;

namespace MingMou.Core
{
    /// <summary>
    /// 开机自启动管理器。
    /// 通过写入/删除当前用户的注册表 Run 键实现，无需管理员权限，
    /// 与系统"启动应用"列表中显示的项目等效。
    /// </summary>
    public static class StartupManager
    {
        /// <summary>
        /// 查询当前是否已启用开机自启动
        /// </summary>
        /// <returns>已启用返回 true；查询失败时返回 false</returns>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(AppConstants.AutoStartRunKeyPath);
                return key?.GetValue(AppConstants.AutoStartValueName) != null;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "查询开机自启动状态失败");
                return false;
            }
        }

        /// <summary>
        /// 设置或取消开机自启动
        /// </summary>
        /// <param name="enable">true 写入 Run 键；false 删除</param>
        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(AppConstants.AutoStartRunKeyPath, writable: true);
                if (key == null)
                {
                    Logger.Error("无法打开注册表 Run 键，设置开机自启动失败");
                    return;
                }

                if (enable)
                {
                    // 记录当前可执行文件路径（非打包模式 exe 直接自启动）
                    var exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        Logger.Error("无法获取程序路径，设置开机自启动失败");
                        return;
                    }

                    key.SetValue(AppConstants.AutoStartValueName, exePath, RegistryValueKind.String);
                    Logger.Info($"开机自启动已开启：{exePath}");
                }
                else
                {
                    key.DeleteValue(AppConstants.AutoStartValueName, throwOnMissingValue: false);
                    Logger.Info("开机自启动已关闭");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "设置开机自启动失败");
            }
        }
    }
}
