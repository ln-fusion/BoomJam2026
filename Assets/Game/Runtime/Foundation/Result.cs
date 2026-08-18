namespace Game.Foundation
{
    /// <summary>
    /// 无返回值操作结果：成功或携带错误码/消息.
    /// </summary>
    public readonly struct Result
    {
        /// <summary>操作是否成功</summary>
        public bool IsSuccess { get; }

        /// <summary>错误码；成功时为 <see cref="ErrorCode.None"/></summary>
        public ErrorCode ErrorCode { get; }

        /// <summary>人类可读错误消息；仅用于日志，不直接展示为玩家文案</summary>
        public string Message { get; }

        private Result(bool isSuccess, ErrorCode errorCode, string message)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        /// <summary>成功结果</summary>
        public static Result Success() => new Result(true, ErrorCode.None, string.Empty);

        /// <summary>失败结果</summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">日志用错误消息</param>
        public static Result Failure(ErrorCode errorCode, string message) => new Result(false, errorCode, message);
    }
}
