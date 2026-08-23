using UnityEngine;

namespace Game.Foundation
{
    /// <summary>
    /// 基于 UnityEngine.Debug 的日志实现：把上下文前缀拼入消息，按级别映射 Debug API。
    /// </summary>
    public sealed class UnityDebugLogger : IGameLogger
    {
        /// <summary>共享实例（无内部状态，可全局复用）.</summary>
        public static UnityDebugLogger Instance { get; } = new UnityDebugLogger();

        /// <summary>按日志级别写入 Unity Debug 日志。</summary>
        /// <param name="level">日志级别。</param>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void Log(LogLevel level, LogContext context, string message)
        {
            var prefixed = $"{context?.FormatPrefix() ?? "[]"} {message}";
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(prefixed);
                    break;
                case LogLevel.Error:
                    Debug.LogError(prefixed);
                    break;
                default:
                    Debug.Log(prefixed);
                    break;
            }
        }

        /// <summary>写入信息级日志。</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void LogInfo(LogContext context, string message) => Log(LogLevel.Info, context, message);

        /// <summary>写入警告级日志。</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void LogWarning(LogContext context, string message) => Log(LogLevel.Warning, context, message);

        /// <summary>写入错误级日志。</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void LogError(LogContext context, string message) => Log(LogLevel.Error, context, message);
    }
}
