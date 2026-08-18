using UnityEngine;

namespace Game.Foundation
{
    /// <summary>
    /// 基于 UnityEngine.Debug 的日志实现：把上下文前缀拼入消息，按级别映射 Debug API.
    /// </summary>
    public sealed class UnityDebugLogger : IGameLogger
    {
        /// <summary>共享实例（无内部状态，可全局复用）.</summary>
        public static UnityDebugLogger Instance { get; } = new UnityDebugLogger();

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

        public void LogInfo(LogContext context, string message) => Log(LogLevel.Info, context, message);

        public void LogWarning(LogContext context, string message) => Log(LogLevel.Warning, context, message);

        public void LogError(LogContext context, string message) => Log(LogLevel.Error, context, message);
    }
}
