// ============================================================================
// 文件：Services/SettingsService.cs
// 用途：用户设置的持久化实现；打包模式下使用 LocalSettings，非打包模式下使用 JSON 文件
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MingMou.Core;

namespace MingMou.Services
{
    /// <summary>
    /// 设置服务实现类
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private readonly Dictionary<string, object> _cache;

        /// <summary>
        /// JSON 设置文件的完整路径。仅在非打包模式下赋值，打包模式下为空字符串。
        /// </summary>
        private readonly string _jsonFilePath = string.Empty;
        private readonly bool _useLocalSettings;
        private readonly object _lock = new object();

        /// <summary>
        /// 初始化设置服务，根据当前运行模式自动选择存储后端
        /// </summary>
        public SettingsService()
        {
            // 通过是否拥有 MSIX 包身份判断当前是否为打包模式
            _useLocalSettings = IsPackaged;
            _cache = new Dictionary<string, object>();

            if (_useLocalSettings)
            {
                // 打包模式：使用 Windows Runtime 提供的 LocalSettings
                Logger.Info("设置服务使用 LocalSettings 存储");
            }
            else
            {
                // 非打包模式：使用 JSON 文件存储
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = Path.Combine(localAppData, AppConstants.AppDataFolderName);
                _jsonFilePath = Path.Combine(appFolder, AppConstants.SettingsFileName);

                try
                {
                    Directory.CreateDirectory(appFolder);
                    LoadFromJson();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "非打包模式下加载 JSON 设置文件失败");
                }

                Logger.Info($"设置服务使用 JSON 文件存储: {_jsonFilePath}");
            }
        }

        /// <summary>
        /// 判断当前进程是否运行在 MSIX 打包环境中
        /// </summary>
        private static bool IsPackaged
        {
            get
            {
                try
                {
                    // 在打包应用外访问 Current 会抛出 InvalidOperationException
                    var _ = Windows.ApplicationModel.Package.Current;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 从 JSON 文件加载已有设置
        /// </summary>
        private void LoadFromJson()
        {
            if (!File.Exists(_jsonFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_jsonFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null)
            {
                return;
            }

            foreach (var pair in dict)
            {
                _cache[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// 将内存中的设置写回 JSON 文件
        /// </summary>
        private void SaveToJson()
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var json = JsonSerializer.Serialize(_cache, options);
                    File.WriteAllText(_jsonFilePath, json);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "保存 JSON 设置文件失败");
                }
            }
        }

        /// <inheritdoc cref="ISettingsService.SetInt" />
        public void SetInt(string key, int value)
        {
            SetValue(key, value);
        }

        /// <inheritdoc cref="ISettingsService.GetInt" />
        public int GetInt(string key, int defaultValue = 0)
        {
            return GetValue(key, defaultValue, element => element.GetInt32());
        }

        /// <inheritdoc cref="ISettingsService.SetBool" />
        public void SetBool(string key, bool value)
        {
            SetValue(key, value);
        }

        /// <inheritdoc cref="ISettingsService.GetBool" />
        public bool GetBool(string key, bool defaultValue = false)
        {
            return GetValue(key, defaultValue, element => element.GetBoolean());
        }

        /// <inheritdoc cref="ISettingsService.SetString" />
        public void SetString(string key, string value)
        {
            SetValue(key, value);
        }

        /// <inheritdoc cref="ISettingsService.GetString" />
        public string GetString(string key, string defaultValue = "")
        {
            return GetValue(key, defaultValue, element => element.GetString() ?? defaultValue);
        }

        /// <summary>
        /// 泛型写入值
        /// </summary>
        private void SetValue<T>(string key, T value)
        {
            if (_useLocalSettings)
            {
                Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] = value;
            }
            else
            {
                lock (_lock)
                {
                    _cache[key] = value!;
                    SaveToJson();
                }
            }
        }

        /// <summary>
        /// 泛型读取值
        /// </summary>
        private T GetValue<T>(string key, T defaultValue, Func<JsonElement, T> elementReader)
        {
            if (_useLocalSettings)
            {
                var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                if (values.TryGetValue(key, out var raw) && raw is T typed)
                {
                    return typed;
                }
                return defaultValue;
            }

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var raw))
                {
                    if (raw is T typed)
                    {
                        return typed;
                    }

                    if (raw is JsonElement element)
                    {
                        try
                        {
                            return elementReader(element);
                        }
                        catch
                        {
                            return defaultValue;
                        }
                    }
                }
            }

            return defaultValue;
        }
    }
}
