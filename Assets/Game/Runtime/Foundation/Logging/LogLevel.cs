namespace Game.Foundation
{
    /// <summary>
    /// 日志级别：按严重程度过滤与聚合。
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试信息。</summary>
        Debug = 0,
        /// <summary>普通运行信息。</summary>
        Info,
        /// <summary>可恢复的异常情况。</summary>
        Warning,
        /// <summary>失败或需要处理的异常情况。</summary>
        Error,
    }
}
