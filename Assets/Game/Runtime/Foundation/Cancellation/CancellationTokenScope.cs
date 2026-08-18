using System;
using System.Threading;

namespace Game.Foundation
{
    /// <summary>
    /// 取消令牌生命周期作用域：Dispose 时统一取消关联的异步操作.
    /// </summary>
    /// <remarks>
    /// C02 用于场景生命周期：活跃场景卸载后调用 <see cref="Dispose"/>，
    /// 使基于该 token 的订阅与异步请求一并取消（开发计划 C02 验收第 3 条）.
    /// </remarks>
    public sealed class CancellationTokenScope : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        // 缓存 Token：CancellationTokenSource 释放后其 Token 属性会抛 ObjectDisposedException
        private readonly CancellationToken _token;

        private bool _disposed;

        public CancellationTokenScope()
        {
            _token = _cts.Token;
        }

        /// <summary>作用域关联的取消令牌.</summary>
        public CancellationToken Token => _token;

        /// <summary>是否已请求取消.</summary>
        public bool IsCancellationRequested => _cts.IsCancellationRequested;

        /// <summary>请求取消（幂等）.</summary>
        public void Cancel()
        {
            // CancellationTokenSource 释放后 Cancel 会抛 ObjectDisposedException，与"幂等"承诺冲突
            if (_disposed)
            {
                return;
            }

            _cts.Cancel();
        }

        /// <summary>请求取消并释放资源；幂等，重复调用及 Dispose 后再调用均不抛异常.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
