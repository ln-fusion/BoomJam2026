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
        public NullLogger(bool collectEntries = true) => _collectEntries = collectEntries;

        public void Log(LogLevel level, LogContext context, string message)
        {
            if (_collectEntries)
            {
                Entries.Add((level, message));
            }
        }

        public void LogInfo(LogContext context, string message) => Log(LogLevel.Info, context, message);

        public void LogWarning(LogContext context, string message) => Log(LogLevel.Warning, context, message);

        public void LogError(LogContext context, string message) => Log(LogLevel.Error, context, message);
    }
}
