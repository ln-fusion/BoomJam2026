using System;
using System.Threading;

namespace Game.Contracts.Lifetime
{
    public sealed class CancellationLifetime : IDisposable
    {
        private readonly CancellationTokenSource _source;
        private bool _disposed;

        public CancellationToken Token => _source.Token;
        public bool IsCancellationRequested => _source.IsCancellationRequested;

        public CancellationLifetime(params CancellationToken[] parentTokens)
        {
            _source = parentTokens != null && parentTokens.Length > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(parentTokens)
                : new CancellationTokenSource();
        }

        public void Cancel()
        {
            if (_disposed || _source.IsCancellationRequested)
                return;

            _source.Cancel();
        }

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
