namespace Game.Foundation
{
    /// <summary>
    /// 带返回值操作结果：成功时携带 Value，失败时携带错误信息.
    /// </summary>
    /// <typeparam name="T">成功结果携带的值类型。</typeparam>
    public readonly struct Result<T>
    {
        /// <summary>操作是否成功。</summary>
        public bool IsSuccess { get; }

        /// <summary>成功结果携带的返回值。</summary>
        private readonly T _value;

        /// <summary>获取成功结果的值；失败结果访问时抛出异常。</summary>
        /// <exception cref="System.InvalidOperationException">当结果为失败时抛出。</exception>
        public T Value => IsSuccess
            ? _value
            : throw new System.InvalidOperationException("A failed result has no value.");

        /// <summary>错误码；成功时为 <see cref="ErrorCode.None"/>。</summary>
        public ErrorCode ErrorCode { get; }

        /// <summary>错误码的兼容别名。</summary>
        public ErrorCode Error => ErrorCode;

        /// <summary>人类可读错误消息；仅用于日志，不直接展示为玩家文案。</summary>
        public string Message { get; }

        /// <summary>创建带值的操作结果。</summary>
        /// <param name="isSuccess">操作是否成功。</param>
        /// <param name="value">操作值。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="message">日志用消息。</param>
        private Result(bool isSuccess, T value, ErrorCode errorCode, string message)
        {
            IsSuccess = isSuccess;
            _value = value;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        /// <summary>创建成功结果。</summary>
        /// <param name="value">成功返回值。</param>
        /// <returns>携带返回值的成功结果。</returns>
        public static Result<T> Success(T value) => new Result<T>(true, value, ErrorCode.None, string.Empty);

        /// <summary>创建失败结果。</summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">日志用错误消息</param>
        public static Result<T> Failure(ErrorCode errorCode, string message = null)
        {
            if (errorCode == ErrorCode.None)
                throw new System.ArgumentException("A failed result requires an error code.", nameof(errorCode));

            return new Result<T>(false, default!, errorCode, message);
        }

        /// <summary>将成功值取出；失败时抛异常，用于已确认成功的调用点。</summary>
        /// <returns>成功值。</returns>
        public T GetValueOrThrow() =>
            IsSuccess
                ? Value
                : throw new System.InvalidOperationException($"Result failed with {ErrorCode}: {Message}");

        /// <summary>将 <see cref="Result{T}"/> 转为无值 <see cref="Result"/>（保留错误信息）。</summary>
        /// <returns>不携带成功值的结果。</returns>
        public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(ErrorCode, Message);
    }
}
