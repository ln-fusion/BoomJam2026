using System.Threading;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 取消令牌生命周期测试：Dispose 时统一取消关联的异步操作.
    /// </summary>
    public class CancellationTokenScopeTests
    {
        /// <summary>验证 Dispose 会请求取消。</summary>
        [Test]
        public void Dispose_Requests_Cancellation()
        {
            using var scope = new CancellationTokenScope();
            Assert.That(scope.Token.IsCancellationRequested, Is.False);
            scope.Dispose();
            Assert.That(scope.IsCancellationRequested, Is.True);
        }

        /// <summary>验证 Cancel 是幂等的。</summary>
        [Test]
        public void Cancel_Is_Idempotent()
        {
            using var scope = new CancellationTokenScope();
            scope.Cancel();
            Assert.DoesNotThrow(scope.Cancel);
            Assert.That(scope.IsCancellationRequested, Is.True);
        }

        /// <summary>验证释放后缓存的 Token 仍可检查取消状态。</summary>
        [Test]
        public void Token_Remains_Usable_After_Dispose()
        {
            var scope = new CancellationTokenScope();
            var token = scope.Token;
            scope.Dispose();
            Assert.That(token.IsCancellationRequested, Is.True);
        }

        /// <summary>验证重复 Dispose 不会抛异常。</summary>
        [Test]
        public void Double_Dispose_Is_Safe()
        {
            var scope = new CancellationTokenScope();
            scope.Dispose();
            Assert.DoesNotThrow(scope.Dispose);
        }
    }
}
