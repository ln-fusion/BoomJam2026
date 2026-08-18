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

        /// <summary>作用域关联的取消令牌.</summary>
        public CancellationToken Token => _cts.Token;

        /// <summary>是否已请求取消.</summary>
        public bool IsCancellationRequested => _cts.IsCancellationRequested;

        /// <summary>请求取消（幂等）.</summary>
        public void Cancel() => _cts.Cancel();

        /// <summary>请求取消并释放资源；重复调用安全.</summary>
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
