using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Persistence
{
    /// <summary>
    /// Persistence 模块内部的原子文件写入抽象，便于替换为测试替身。
    /// </summary>
    internal interface IAtomicFileWriter
    {
        /// <summary>以临时文件和备份替换策略写入目标文件。</summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="contents">待写入的文本内容。</param>
        /// <param name="validateTemporaryFile">临时文件校验回调。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>表示写入任务的异步任务。</returns>
        Task WriteAsync(string targetPath, string contents,
            Func<string, bool> validateTemporaryFile,
            CancellationToken cancellationToken);
    }
}
