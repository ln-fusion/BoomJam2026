namespace Game.Foundation
{
    /// <summary>
    /// 带返回值操作结果：成功时携带 Value，失败时携带错误信息.
    /// </summary>
    public readonly struct Result<T>
    {
        /// <summary>操作是否成功</summary>
        public bool IsSuccess { get; }

        /// <summary>成功时的返回值；失败时为 default</summary>
        private readonly T _value;

        public T Value => IsSuccess
            ? _value
            : throw new System.InvalidOperationException("A failed result has no value.");

        /// <summary>错误码；成功时为 <see cref="ErrorCode.None"/></summary>
        public ErrorCode ErrorCode { get; }

        public ErrorCode Error => ErrorCode;

        /// <summary>人类可读错误消息；仅用于日志，不直接展示为玩家文案</summary>
        public string Message { get; }

        private Result(bool isSuccess, T value, ErrorCode errorCode, string message)
        {
            IsSuccess = isSuccess;
            _value = value;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        /// <summary>成功结果（value 不允许为 null 的引用类型）</summary>
        public static Result<T> Success(T value) => new Result<T>(true, value, ErrorCode.None, string.Empty);

        /// <summary>失败结果</summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">日志用错误消息</param>
        public static Result<T> Failure(ErrorCode errorCode, string message = null)
        {
            if (errorCode == ErrorCode.None)
                throw new System.ArgumentException("A failed result requires an error code.", nameof(errorCode));

            return new Result<T>(false, default!, errorCode, message);
        }

        /// <summary>将成功值取出；失败时抛异常，用于已确认成功的调用点</summary>
        public T GetValueOrThrow() =>
            IsSuccess
                ? Value
                : throw new System.InvalidOperationException($"Result failed with {ErrorCode}: {Message}");

        /// <summary>将 <see cref="Result{T}"/> 转为无值 <see cref="Result"/>（保留错误信息）</summary>
        public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(ErrorCode, Message);
    }
}
