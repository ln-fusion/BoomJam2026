using Game.Foundation;

namespace Game.Contracts.Persistence
{
    /// <summary>
    /// 本地存档加载结果来源。
    /// </summary>
    public enum LoadSource
    {
        /// <summary>主文件和备份文件均不存在（仅玩家档案会返回该来源）。</summary>
        NotFound,
        /// <summary>从主文件读取成功。</summary>
        Primary,
        /// <summary>主文件不可用时从备份文件恢复成功。</summary>
        Backup,
        /// <summary>无可用文件（仅设置存档）或读取失败时回退到安全默认对象。</summary>
        Default
    }

    /// <summary>
    /// 本地存档加载结果，包含数据、来源和恢复警告。
    /// </summary>
    /// <typeparam name="T">加载的数据类型。</typeparam>
    public sealed class LoadResult<T>
    {
        /// <summary>加载得到的数据；档案缺失时可为 null。</summary>
        public T Data { get; }
        /// <summary>数据来源。</summary>
        public LoadSource Source { get; }
        /// <summary>是否发生过损坏、非法或 IO 恢复警告。</summary>
        public bool HasRecoveryWarning { get; }
        /// <summary>恢复时保留的错误码；无警告时为 <see cref="ErrorCode.None"/>。</summary>
        public ErrorCode RecoveryError { get; }

        /// <summary>创建加载结果。</summary>
        /// <param name="data">加载得到的数据。</param>
        /// <param name="source">数据来源。</param>
        /// <param name="recoveryError">恢复警告错误码；无警告时传 <see cref="ErrorCode.None"/>。</param>
        public LoadResult(T data, LoadSource source, ErrorCode recoveryError)
        {
            Data = data;
            Source = source;
            RecoveryError = recoveryError;
            HasRecoveryWarning = recoveryError != ErrorCode.None;
        }
    }

    /// <summary>
    /// 本地存档写入结果。
    /// </summary>
    public readonly struct SaveResult
    {
        /// <summary>写入是否成功。</summary>
        public bool IsSuccess { get; }
        /// <summary>失败错误码；成功时为 <see cref="ErrorCode.None"/>。</summary>
        public ErrorCode Error { get; }
        /// <summary>日志用错误消息。</summary>
        public string Message { get; }

        /// <summary>创建具有指定成功状态和错误信息的写入结果。</summary>
        /// <param name="isSuccess">写入是否成功。</param>
        /// <param name="error">失败错误码；成功时为 <see cref="ErrorCode.None"/>。</param>
        /// <param name="message">日志用错误消息。</param>
        private SaveResult(bool isSuccess, ErrorCode error, string message)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
        }

        /// <summary>创建成功写入结果。</summary>
        /// <returns>成功结果。</returns>
        public static SaveResult Success()
        {
            return new SaveResult(true, ErrorCode.None, string.Empty);
        }

        /// <summary>创建失败写入结果。</summary>
        /// <param name="error">失败错误码。</param>
        /// <param name="message">日志用错误消息。</param>
        /// <returns>失败结果。</returns>
        public static SaveResult Failure(ErrorCode error, string message)
        {
            return new SaveResult(false, error, message);
        }
    }

    /// <summary>
    /// 触发存档写入的业务原因，写入日志并供平台同步策略使用。
    /// </summary>
    public enum SaveReason
    {
        /// <summary>未知或尚未分类的写入原因。</summary>
        Unknown,
        /// <summary>玩家应用设置后写入。</summary>
        SettingsApplied,
        /// <summary>创建新玩家档案后写入。</summary>
        ProfileCreated,
        /// <summary>关卡进度提交后写入。</summary>
        ProgressCommitted,
        /// <summary>剧情完成事实提交后写入。</summary>
        StoryCommitted,
        /// <summary>主界面页面切换后写入。</summary>
        PageChanged,
        /// <summary>应用失去焦点时写入。</summary>
        ApplicationFocusLost,
        /// <summary>应用退出时写入。</summary>
        ApplicationQuit
    }
}
