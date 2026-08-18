using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Persistence
{
    internal interface IAtomicFileWriter
    {
        Task WriteAsync(string targetPath, string contents,
            Func<string, bool> validateTemporaryFile,
            CancellationToken cancellationToken);
    }
}
