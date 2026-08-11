// ============================================================================
// 文件：Core/Logger.cs
// 用途：提供线程安全的简单日志写入功能，按天分割日志文件
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MingMou.Core
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试信息</summary>
        Debug,

        /// <summary>常规信息</summary>
        Info,

        /// <summary>警告</summary>
        Warning,

        /// <summary>错误</summary>
        Error
    }

    /// <summary>
    /// 应用日志记录器
    /// 负责写入 %LocalAppData%\MingMou\Logs\log_yyyyMMdd.txt
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFolder = string.Empty;
        private static string _currentFilePath = string.Empty;
        private static DateTime _currentFileDate = DateTime.MinValue;

        /// <summary>
        /// 初始化日志目录
        /// </summary>
        public static void Initialize()
        {
            EnsureLogFolder();
        }

        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Info(string message)
        {
            WriteLog(LogLevel.Info, message);
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Debug(string message)
        {
            WriteLog(LogLevel.Debug, message);
        }

        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Warning(string message)
        {
            WriteLog(LogLevel.Warning, message);
        }

        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Error(string message)
        {
            WriteLog(LogLevel.Error, message);
        }

        /// <summary>
        /// 记录异常信息
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">异常发生时的上下文描述</param>
        public static void Exception(Exception ex, string context)
        {
            if (ex == null)
            {
                return;
            }

            var formatted = $"[{context}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            WriteLog(LogLevel.Error, formatted);
        }

        /// <summary>
        /// 内部方法：确保日志目录已存在，并构造完整路径
        /// </summary>
        private static void EnsureLogFolder()
        {
            // 仅在路径为空时初始化一次
            if (!string.IsNullOrEmpty(_logFolder))
            {
                return;
            }

            // 日志目录：%LocalAppData%\MingMou\Logs
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logFolder = Path.Combine(localAppData, AppConstants.AppDataFolderName, AppConstants.LogsSubFolderName);

            try
            {
                if (!Directory.Exists(_logFolder))
                {
                    Directory.CreateDirectory(_logFolder);
                }
            }
            catch (Exception ex)
            {
                // 目录创建失败时降级为系统临时目录，避免日志写入彻底失败
                _logFolder = Path.Combine(Path.GetTempPath(), AppConstants.AppDataFolderName, AppConstants.LogsSubFolderName);
                try
                {
                    Directory.CreateDirectory(_logFolder);
                }
                catch
                {
                    Trace.WriteLine($"无法创建日志目录: {_logFolder}");
                }
                Trace.WriteLine($"初始化日志目录异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部方法：获取当前应写入的日志文件路径（按天切换）
        /// </summary>
        /// <returns>完整的日志文件路径</returns>
        private static string GetCurrentLogFilePath()
        {
            var today = DateTime.Now.Date;
            if (today != _currentFileDate || string.IsNullOrEmpty(_currentFilePath))
            {
                _currentFileDate = today;
                var fileName = $"{AppConstants.LogFileNamePrefix}{today:yyyyMMdd}{AppConstants.LogFileExtension}";
                _currentFilePath = Path.Combine(_logFolder, fileName);
            }

            return _currentFilePath;
        }

        /// <summary>
        /// 内部方法：将格式化的日志内容写入文件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">原始日志内容</param>
        private static void WriteLog(LogLevel level, string message)
        {
            try
            {
                EnsureLogFolder();

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var line = $"[{timestamp}] [{level.ToString().ToUpperInvariant()}] {message}";

                // 使用锁保证多线程写入安全
                lock (_lock)
                {
                    File.AppendAllText(GetCurrentLogFilePath(), line + Environment.NewLine);
                }

                // 同时输出到 Visual Studio 输出窗口，方便调试
                Trace.WriteLine(line);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"写入日志失败: {ex.Message}");
            }
        }
    }
}
