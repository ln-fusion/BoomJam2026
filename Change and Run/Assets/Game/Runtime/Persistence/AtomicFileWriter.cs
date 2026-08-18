using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Persistence
{
    internal sealed class AtomicFileWriter : IAtomicFileWriter
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

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
