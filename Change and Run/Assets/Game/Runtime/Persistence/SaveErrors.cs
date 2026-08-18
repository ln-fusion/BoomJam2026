using Game.Foundation.Results;

namespace Game.Persistence
{
    internal static class SaveErrors
    {
        public static readonly ErrorCode Io =
            new ErrorCode(ErrorCategory.SaveIo, "save.io");
        public static readonly ErrorCode Corrupt =
            new ErrorCode(ErrorCategory.SaveCorrupt, "save.corrupt");
        public static readonly ErrorCode Invalid =
            new ErrorCode(ErrorCategory.Validation, "save.invalid");
        public static readonly ErrorCode UnsupportedVersion =
            new ErrorCode(ErrorCategory.Validation, "save.unsupported_version");
    }
}
