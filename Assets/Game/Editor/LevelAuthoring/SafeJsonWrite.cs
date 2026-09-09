using System;
using System.IO;
using System.Text;
using Game.Foundation;

namespace Game.Editor.Level
{
    /// <summary>
    /// Editor 环境的 JSON 安全写入: 临时文件 + 回读校验 + 原子替换。
    /// </summary>
    /// <remarks>
    /// 仅在关卡编辑器的 Authoring 工具链中落盘关卡定义使用; 运行时不走此路径,
    /// 因此不存在"Editor 可用但打包后不可用"的差异。
    /// 与 Persistence 的 <c>AtomicFileWriter</c> 语义一致, 但后者的 writer 是 internal,
    /// 且 Editor.Level 不引用 Persistence 程序集, 故在此独立实现。
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
                    // 原子替换: 旧文件作为备份保留在 .prev, 新文件在落位前若失败, 目标文件仍完好。
                    File.Replace(temporaryPath, targetPath, previousPath);
                    File.Delete(previousPath);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ErrorCode.SaveFailed, ex.Message);
            }
        }
    }
}
