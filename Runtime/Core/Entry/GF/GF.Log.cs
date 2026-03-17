using System.Diagnostics;
using JulyCore.Core;
using UnityEngine;

namespace JulyCore
{
    /// <summary>
    /// GF.Log - 日志模块
    /// 提供上层项目使用的日志接口
    /// </summary>
    public static partial class GF
    {
        /// <summary>
        /// 输出普通日志
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void Log(object message) => JLogger.Log(message);

        /// <summary>
        /// 输出普通日志（带上下文）
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void Log(object message, Object context) => JLogger.Log(message, context);

        /// <summary>
        /// 输出调试日志
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void LogDebug(object message) => JLogger.LogDebug(message);

        /// <summary>
        /// 输出信息日志
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void LogInfo(object message) => JLogger.LogInfo(message);

        /// <summary>
        /// 输出警告日志
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void LogWarning(object message) => JLogger.LogWarning(message);

        /// <summary>
        /// 输出警告日志（带上下文）
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void LogWarning(object message, Object context) => JLogger.LogWarning(message, context);

        /// <summary>
        /// 输出错误日志
        /// </summary>
        public static void LogError(object message) => JLogger.LogError(message);

        /// <summary>
        /// 输出错误日志（带上下文）
        /// </summary>
        public static void LogError(object message, Object context) => JLogger.LogError(message, context);

        /// <summary>
        /// 输出致命错误日志
        /// </summary>
        public static void LogFatal(object message) => JLogger.LogFatal(message);

        /// <summary>
        /// 输出异常日志
        /// </summary>
        public static void LogException(System.Exception exception) => JLogger.LogException(exception);

        /// <summary>
        /// 输出异常日志（带上下文）
        /// </summary>
        public static void LogException(System.Exception exception, Object context) => JLogger.LogException(exception, context);

        /// <summary>
        /// 断言
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void Assert(bool condition, object message) => JLogger.Assert(condition, message);

        /// <summary>
        /// 断言（带上下文）
        /// </summary>
        [Conditional("JULYGF_DEBUG")]
        public static void Assert(bool condition, object message, Object context) => JLogger.Assert(condition, message, context);
    }
}
