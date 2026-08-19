using Game.Foundation.Results;

namespace Game.Contracts.Persistence
{
    public enum LoadSource
    {
        NotFound,
        Primary,
        Backup,
        Default
    }

    public sealed class LoadResult<T>
    {
        public T Data { get; }
        public LoadSource Source { get; }
        public bool HasRecoveryWarning { get; }
        public ErrorCode RecoveryError { get; }

        public LoadResult(T data, LoadSource source, ErrorCode recoveryError)
        {
            Data = data;
            Source = source;
            RecoveryError = recoveryError;
            HasRecoveryWarning = recoveryError != ErrorCode.None;
        }
    }

    public readonly struct SaveResult
    {
        public bool IsSuccess { get; }
        public ErrorCode Error { get; }
        public string Message { get; }

        private SaveResult(bool isSuccess, ErrorCode error, string message)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
        }

        public static SaveResult Success()
        {
            return new SaveResult(true, ErrorCode.None, string.Empty);
        }

        public static SaveResult Failure(ErrorCode error, string message)
        {
            return new SaveResult(false, error, message);
        }
    }

    public enum SaveReason
    {
        Unknown,
        SettingsApplied,
        ProfileCreated,
        ProgressCommitted,
        StoryCommitted,
        PageChanged,
        ApplicationFocusLost,
        ApplicationQuit
    }
}
