using System;

namespace Game.Foundation
{
    /// <summary>
    /// 统一错误类别：错误所属的模块边界，用于日志聚合与平台上报.
    /// </summary>
    public enum ErrorCategory
    {
        None = 0,
        Unknown,
        Validation,
        Content,
        SaveIo,
        SaveCorrupt,
        PlatformUnavailable,
        PlatformSync,
        SceneTransition,
        SimulationPerformance,
        Infrastructure,
        Unexpected
    }

    /// <summary>
    /// 错误码：标记具体失败原因；新增时保持数值向后兼容，不重排既有枚举值.
    /// </summary>
    [Serializable]
    public readonly struct ErrorCode : IEquatable<ErrorCode>
    {
        public static readonly ErrorCode None = new ErrorCode(ErrorCategory.None, "none");
        public static readonly ErrorCode Unknown = new ErrorCode(ErrorCategory.Unknown, "unknown");
        public static readonly ErrorCode InvalidArgument = new ErrorCode(ErrorCategory.Validation, "invalid_argument");
        public static readonly ErrorCode NotFound = new ErrorCode(ErrorCategory.Content, "not_found");
        public static readonly ErrorCode AlreadyExists = new ErrorCode(ErrorCategory.Validation, "already_exists");
        public static readonly ErrorCode OperationNotAllowed = new ErrorCode(ErrorCategory.Validation, "operation_not_allowed");
        public static readonly ErrorCode SaveFailed = new ErrorCode(ErrorCategory.SaveIo, "save_failed");
        public static readonly ErrorCode LoadFailed = new ErrorCode(ErrorCategory.SaveIo, "load_failed");
        public static readonly ErrorCode SceneLoadFailed = new ErrorCode(ErrorCategory.SceneTransition, "scene_load_failed");
        public static readonly ErrorCode SceneUnloadFailed = new ErrorCode(ErrorCategory.SceneTransition, "scene_unload_failed");
        public static readonly ErrorCode OperationCancelled = new ErrorCode(ErrorCategory.Infrastructure, "operation_cancelled");

        public ErrorCategory Category { get; }
        public string Value { get; }

        public ErrorCode(ErrorCategory category, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("An error code value is required.", nameof(value));

            Category = category;
            Value = value;
        }

        public bool Equals(ErrorCode other) =>
            Category == other.Category && string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ErrorCode other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Category * 397) ^ (Value == null ? 0 : Value.GetHashCode());
            }
        }

        public override string ToString() => $"{Category}:{Value}";

        public static bool operator ==(ErrorCode left, ErrorCode right) => left.Equals(right);
        public static bool operator !=(ErrorCode left, ErrorCode right) => !left.Equals(right);
    }
}
