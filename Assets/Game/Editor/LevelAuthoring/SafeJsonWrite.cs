using System;
using System.IO;
using System.Text;
using Game.Foundation;

namespace Game.Editor.Level
{
    /// <summary>
    /// Editor 环境下的 JSON 安全写入: 临时文件 + 回读校验 + 原子替换。
    /// </summary>
    /// <remarks>
    /// 与 Persistence 的 AtomicFileWriter 语义一致, 但 Persistence 的 writer 是 internal,
    /// Editor 程序集无法复用, 故在 Editor.Level 内独立实现。
    /// </remarks>
    internal static class SafeJsonWrite
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>安全写入目标文件; 失败时保留旧文件。</summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="contents">待写入的 UTF-8 JSON 文本。</param>
        /// <param name="validateTemporaryFile">临时文件回读校验回调。</param>
        /// <returns>成功返回成功结果, 失败返回 SaveFailed 错误码。</returns>
        public static Result Write(string targetPath, string contents, Func<string, bool> validateTemporaryFile)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("A target path is required.", nameof(targetPath));
            if (contents == null)
                throw new ArgumentNullException(nameof(contents));
            if (validateTemporaryFile == null)
                throw new ArgumentNullException(nameof(validateTemporaryFile));

            string directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("The target must include a directory.", nameof(targetPath));

            try
            {
                Directory.CreateDirectory(directory);
                string temporaryPath = targetPath + ".tmp";
                string previousPath = targetPath + ".prev";

                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                File.WriteAllText(temporaryPath, contents, Utf8WithoutBom);

                // 回读校验失败说明序列化产物无法被同一解析器接受, 属于内容错误;
                // 成功替换前不触碰目标文件, 旧文件在写入失败时保持可用。
                string reread = File.ReadAllText(temporaryPath);
                if (!validateTemporaryFile(reread))
                {
                    File.Delete(temporaryPath);
                    return Result.Failure(ErrorCode.InvalidArgument, "Temporary file validation failed: " + targetPath);
                }

                if (File.Exists(targetPath))
                {
                    if (File.Exists(previousPath))
                        File.Delete(previousPath);
                    File.Move(targetPath, previousPath);
                }
                File.Move(temporaryPath, targetPath);
                File.Delete(previousPath);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ErrorCode.SaveFailed, ex.Message);
            }
        }
    }
}
