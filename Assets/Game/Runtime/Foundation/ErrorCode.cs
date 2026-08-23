using System;

namespace Game.Foundation
{
    /// <summary>
    /// 统一错误类别：错误所属的模块边界，用于日志聚合与平台上报。
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>无错误。</summary>
        None = 0,
        /// <summary>未分类错误。</summary>
        Unknown,
        /// <summary>输入或数据校验失败。</summary>
        Validation,
        /// <summary>内容查找或内容格式错误。</summary>
        Content,
        /// <summary>存档文件读写失败。</summary>
        SaveIo,
        /// <summary>存档文件损坏。</summary>
        SaveCorrupt,
        /// <summary>平台服务不可用。</summary>
        PlatformUnavailable,
        /// <summary>平台同步失败。</summary>
        PlatformSync,
        /// <summary>场景切换失败。</summary>
        SceneTransition,
        /// <summary>模拟性能不满足要求。</summary>
        SimulationPerformance,
        /// <summary>基础设施异常。</summary>
        Infrastructure,
        /// <summary>未预期异常。</summary>
        Unexpected
    }

    /// <summary>
    /// 错误码：标记具体失败原因；新增时保持数值向后兼容，不重排既有枚举值。
    /// </summary>
    [Serializable]
    public readonly struct ErrorCode : IEquatable<ErrorCode>
    {
        /// <summary>无错误。</summary>
        public static readonly ErrorCode None = new ErrorCode(ErrorCategory.None, "none");
        /// <summary>未分类错误。</summary>
        public static readonly ErrorCode Unknown = new ErrorCode(ErrorCategory.Unknown, "unknown");
        /// <summary>参数不合法。</summary>
        public static readonly ErrorCode InvalidArgument = new ErrorCode(ErrorCategory.Validation, "invalid_argument");
        /// <summary>目标内容不存在。</summary>
        public static readonly ErrorCode NotFound = new ErrorCode(ErrorCategory.Content, "not_found");
        /// <summary>目标已经存在。</summary>
        public static readonly ErrorCode AlreadyExists = new ErrorCode(ErrorCategory.Validation, "already_exists");
        /// <summary>当前状态不允许执行操作。</summary>
        public static readonly ErrorCode OperationNotAllowed = new ErrorCode(ErrorCategory.Validation, "operation_not_allowed");
        /// <summary>存档写入失败。</summary>
        public static readonly ErrorCode SaveFailed = new ErrorCode(ErrorCategory.SaveIo, "save_failed");
        /// <summary>存档读取失败。</summary>
        public static readonly ErrorCode LoadFailed = new ErrorCode(ErrorCategory.SaveIo, "load_failed");
        /// <summary>场景加载失败。</summary>
        public static readonly ErrorCode SceneLoadFailed = new ErrorCode(ErrorCategory.SceneTransition, "scene_load_failed");
        /// <summary>场景卸载失败。</summary>
        public static readonly ErrorCode SceneUnloadFailed = new ErrorCode(ErrorCategory.SceneTransition, "scene_unload_failed");
        /// <summary>操作被取消。</summary>
        public static readonly ErrorCode OperationCancelled = new ErrorCode(ErrorCategory.Infrastructure, "operation_cancelled");
        /// <summary>本地化系统初始化失败。</summary>
        public static readonly ErrorCode LocalizationInitializationFailed =
            new ErrorCode(ErrorCategory.Infrastructure, "localization.initialization_failed");
        /// <summary>本地化资源未通过必需 Key 校验。</summary>
        public static readonly ErrorCode LocalizationDataInvalid =
            new ErrorCode(ErrorCategory.Content, "localization.data_invalid");
        /// <summary>设置草稿不符合约束。</summary>
        public static readonly ErrorCode SettingsInvalid = new ErrorCode(ErrorCategory.Validation, "settings.invalid");
        /// <summary>请求的 Locale 不受支持。</summary>
        public static readonly ErrorCode LocaleUnsupported = new ErrorCode(ErrorCategory.Validation, "locale.unsupported");
        /// <summary>窗口设置无法应用。</summary>
        public static readonly ErrorCode WindowApplyFailed = new ErrorCode(ErrorCategory.Infrastructure, "window.apply_failed");
        /// <summary>设置存档写入失败。</summary>
        public static readonly ErrorCode SettingsSaveFailed = new ErrorCode(ErrorCategory.SaveIo, "settings.save_failed");

        /// <summary>错误所属类别。</summary>
        public ErrorCategory Category { get; }
        /// <summary>错误的稳定字符串值。</summary>
        public string Value { get; }

        /// <summary>创建错误码。</summary>
        /// <param name="category">错误类别。</param>
        /// <param name="value">错误稳定字符串。</param>
        public ErrorCode(ErrorCategory category, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("An error code value is required.", nameof(value));

            Category = category;
            Value = value;
        }

        /// <summary>比较两个错误码的类别和值。</summary>
        /// <param name="other">待比较错误码。</param>
        /// <returns>相同返回 true，否则返回 false。</returns>
        public bool Equals(ErrorCode other) =>
            Category == other.Category && string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <summary>判断对象是否为相同错误码。</summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>相同返回 true，否则返回 false。</returns>
        public override bool Equals(object obj) => obj is ErrorCode other && Equals(other);

        /// <summary>返回错误码的哈希码。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Category * 397) ^ (Value == null ? 0 : Value.GetHashCode());
            }
        }

        /// <summary>返回类别和值组成的诊断文本。</summary>
        public override string ToString() => $"{Category}:{Value}";

        /// <summary>判断两个错误码是否相等。</summary>
        /// <param name="left">左侧错误码。</param>
        /// <param name="right">右侧错误码。</param>
        /// <returns>相等返回 true。</returns>
        public static bool operator ==(ErrorCode left, ErrorCode right) => left.Equals(right);
        /// <summary>判断两个错误码是否不相等。</summary>
        /// <param name="left">左侧错误码。</param>
        /// <param name="right">右侧错误码。</param>
        /// <returns>不相等返回 true。</returns>
        public static bool operator !=(ErrorCode left, ErrorCode right) => !left.Equals(right);
    }
}
