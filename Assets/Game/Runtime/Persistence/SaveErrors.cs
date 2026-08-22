using Game.Foundation;

namespace Game.Persistence
{
    /// <summary>Persistence 模块内部使用的标准存档错误码集合。</summary>
    internal static class SaveErrors
    {
        /// <summary>文件读写失败。</summary>
        public static readonly ErrorCode Io =
            new ErrorCode(ErrorCategory.SaveIo, "save.io");
        /// <summary>文件内容损坏或无法反序列化。</summary>
        public static readonly ErrorCode Corrupt =
            new ErrorCode(ErrorCategory.SaveCorrupt, "save.corrupt");
        /// <summary>文件内容通过反序列化但未通过业务校验。</summary>
        public static readonly ErrorCode Invalid =
            new ErrorCode(ErrorCategory.Validation, "save.invalid");
        /// <summary>文件结构版本超出当前运行时支持范围。</summary>
        public static readonly ErrorCode UnsupportedVersion =
            new ErrorCode(ErrorCategory.Validation, "save.unsupported_version");
    }
}
