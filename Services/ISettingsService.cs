// ============================================================================
// 文件：Services/ISettingsService.cs
// 用途：用户设置服务的抽象接口，便于打包/非打包模式之间的存储方式切换
// ============================================================================

namespace MingMou.Services
{
    /// <summary>
    /// 用户设置服务接口
    /// 提供键值对持久化能力，屏蔽底层是 LocalSettings 还是 JSON 文件的差异
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 保存整数值
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="value">值</param>
        void SetInt(string key, int value);

        /// <summary>
        /// 获取整数值
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="defaultValue">键不存在时返回的默认值</param>
        /// <returns>设置值或默认值</returns>
        int GetInt(string key, int defaultValue = 0);

        /// <summary>
        /// 保存布尔值
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="value">值</param>
        void SetBool(string key, bool value);

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="defaultValue">键不存在时返回的默认值</param>
        /// <returns>设置值或默认值</returns>
        bool GetBool(string key, bool defaultValue = false);

        /// <summary>
        /// 保存字符串值
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="value">值</param>
        void SetString(string key, string value);

        /// <summary>
        /// 获取字符串值
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="defaultValue">键不存在时返回的默认值</param>
        /// <returns>设置值或默认值</returns>
        string GetString(string key, string defaultValue = "");
    }
}
