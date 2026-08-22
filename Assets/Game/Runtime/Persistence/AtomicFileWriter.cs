using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Persistence
{
    /// <summary>
    /// 以临时文件、校验、替换和备份顺序写入本地文件的原子写入器。
    /// </summary>
    internal sealed class AtomicFileWriter : IAtomicFileWriter
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>异步写入并校验一个本地文件。</summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="contents">待写入的 UTF-8 文本内容。</param>
        /// <param name="validateTemporaryFile">临时文件落盘后的校验回调。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>表示写入任务的异步任务。</returns>
        public Task WriteAsync(string targetPath, string contents,
            Func<string, bool> validateTemporaryFile,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("A target path is required.", nameof(targetPath));
            if (contents == null)
                throw new ArgumentNullException(nameof(contents));
            if (validateTemporaryFile == null)
                throw new ArgumentNullException(nameof(validateTemporaryFile));

            return Task.Run(() => Write(targetPath, contents, validateTemporaryFile,
                cancellationToken), cancellationToken);
        }

        /// <summary>在后台线程执行临时文件写入、校验和目标替换。</summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="contents">待写入内容。</param>
        /// <param name="validateTemporaryFile">临时文件校验回调。</param>
        /// <param name="cancellationToken">取消标记。</param>
        private static void Write(string targetPath, string contents,
            Func<string, bool> validateTemporaryFile,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("The target must include a directory.", nameof(targetPath));

            Directory.CreateDirectory(directory);
            string temporaryPath = Path.ChangeExtension(targetPath, ".tmp");
            string backupPath = Path.ChangeExtension(targetPath, ".bak");

            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            byte[] bytes = Utf8WithoutBom.GetBytes(contents);
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                           FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!validateTemporaryFile(temporaryPath))
                    throw new InvalidDataException("The temporary save file failed validation.");

                if (File.Exists(targetPath))
                    File.Replace(temporaryPath, targetPath, backupPath, true);
                else
                    File.Move(temporaryPath, targetPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
