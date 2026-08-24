namespace Game.Foundation
{
    /// <summary>
    /// 日志抽象：跨模块统一入口，隔离 UnityEngine.Debug 依赖以便测试.
    /// </summary>
    /// <remarks>
    /// 预期失败路径必须有 Result/日志，不静默吞掉（开发计划 §2.2 最低验收标准 4）.
    /// </remarks>
    public interface IGameLogger
    {
        /// <summary>记录一条日志.</summary>
        /// <param name="level">日志级别</param>
        /// <param name="context">结构化上下文，可为 <see cref="LogContext.Empty"/></param>
        /// <param name="message">日志消息</param>
        void Log(LogLevel level, LogContext context, string message);

        /// <summary>记录信息级日志.</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        void LogInfo(LogContext context, string message);

        /// <summary>记录警告.</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        void LogWarning(LogContext context, string message);

        /// <summary>记录错误.</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        void LogError(LogContext context, string message);
    }
}
