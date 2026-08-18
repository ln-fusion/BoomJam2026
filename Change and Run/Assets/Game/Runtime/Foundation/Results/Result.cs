using System;

namespace Game.Foundation.Results
{
    public readonly struct Result
    {
        public bool IsSuccess { get; }
        public ErrorCode Error { get; }
        public string Message { get; }

        private Result(bool isSuccess, ErrorCode error, string message)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
        }

        public static Result Success()
        {
            return new Result(true, ErrorCode.None, string.Empty);
        }

        public static Result Failure(ErrorCode error, string message = null)
        {
            if (error == ErrorCode.None)
                throw new ArgumentException("A failed result requires an error code.", nameof(error));

            return new Result(false, error, message);
        }
    }

    public readonly struct Result<T>
    {
        private readonly T _value;

        public bool IsSuccess { get; }
        public ErrorCode Error { get; }
        public string Message { get; }

        public T Value
        {
            get
            {
                if (!IsSuccess)
                    throw new InvalidOperationException("A failed result has no value.");

                return _value;
            }
        }

        private Result(bool isSuccess, T value, ErrorCode error, string message)
        {
            IsSuccess = isSuccess;
            _value = value;
            Error = error;
            Message = message ?? string.Empty;
        }

        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, ErrorCode.None, string.Empty);
        }

        public static Result<T> Failure(ErrorCode error, string message = null)
        {
            if (error == ErrorCode.None)
                throw new ArgumentException("A failed result requires an error code.", nameof(error));

            return new Result<T>(false, default, error, message);
        }
    }
}
