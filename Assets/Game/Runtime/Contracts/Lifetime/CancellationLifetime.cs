using System;
using System.Threading;

namespace Game.Contracts.Lifetime
{
    /// <summary>
    /// 可释放的取消生命周期，统一管理一组异步任务的取消标记。
    /// </summary>
    /// <remarks>
    /// Dispose 会请求取消并释放内部 <see cref="CancellationTokenSource"/>，用于场景或订阅生命周期清理。
    /// </remarks>
    public sealed class CancellationLifetime : IDisposable
    {
        private readonly CancellationTokenSource _source;
        private bool _disposed;

        /// <summary>生命周期关联的取消标记。</summary>
        public CancellationToken Token => _source.Token;
        /// <summary>是否已经请求取消。</summary>
        public bool IsCancellationRequested => _source.IsCancellationRequested;

        /// <summary>创建取消生命周期，并可选择链接父级取消标记。</summary>
        /// <param name="parentTokens">父级取消标记集合。</param>
        public CancellationLifetime(params CancellationToken[] parentTokens)
        {
            _source = parentTokens != null && parentTokens.Length > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(parentTokens)
                : new CancellationTokenSource();
        }

        /// <summary>请求取消；已取消或已释放时不重复操作。</summary>
        public void Cancel()
        {
            if (_disposed || _source.IsCancellationRequested)
                return;

            _source.Cancel();
        }

        /// <summary>请求取消并释放内部资源；重复调用安全。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (!_source.IsCancellationRequested)
                _source.Cancel();
            _source.Dispose();
        }
    }
}
