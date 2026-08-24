using System.Collections.Generic;

namespace Game.Foundation
{
    /// <summary>
    /// 丢弃所有日志的实现：用于单元测试与显式静默场景（不静默吞错，仅测试替身）.
    /// </summary>
    public sealed class NullLogger : IGameLogger
    {
        /// <summary>收集到的日志（供断言使用）.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

        /// <summary>共享实例（丢弃日志，不收集）.</summary>
        public static NullLogger Instance { get; } = new NullLogger(collectEntries: false);

        private readonly bool _collectEntries;

        /// <summary>创建日志收集器（默认收集到 <see cref="Entries"/>）.</summary>
        /// <param name="collectEntries">为 true 时将日志追加到 <see cref="Entries"/>；为 false 时丢弃日志。</param>
        public NullLogger(bool collectEntries = true) => _collectEntries = collectEntries;

        /// <summary>按配置丢弃或收集一条日志。</summary>
        /// <param name="level">日志级别。</param>
        /// <param name="context">结构化日志上下文；该实现不使用。</param>
        /// <param name="message">日志消息。</param>
        public void Log(LogLevel level, LogContext context, string message)
        {
            if (_collectEntries)
            {
                Entries.Add((level, message));
            }
        }

        /// <summary>收集或丢弃信息级日志。</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void LogInfo(LogContext context, string message) => Log(LogLevel.Info, context, message);

        /// <summary>收集或丢弃警告级日志。</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void LogWarning(LogContext context, string message) => Log(LogLevel.Warning, context, message);

        /// <summary>收集或丢弃错误级日志。</summary>
        /// <param name="context">结构化日志上下文。</param>
        /// <param name="message">日志消息。</param>
        public void LogError(LogContext context, string message) => Log(LogLevel.Error, context, message);
    }
}
