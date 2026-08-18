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
        [Test]
        public void Dispose_Requests_Cancellation()
        {
            using var scope = new CancellationTokenScope();
            Assert.That(scope.Token.IsCancellationRequested, Is.False);
            scope.Dispose();
            Assert.That(scope.IsCancellationRequested, Is.True);
        }

        [Test]
        public void Cancel_Is_Idempotent()
        {
            using var scope = new CancellationTokenScope();
            scope.Cancel();
            Assert.DoesNotThrow(scope.Cancel);
            Assert.That(scope.IsCancellationRequested, Is.True);
        }

        [Test]
        public void Token_Remains_Usable_After_Dispose()
        {
            var scope = new CancellationTokenScope();
            var token = scope.Token;
            scope.Dispose();
            Assert.That(token.IsCancellationRequested, Is.True);
        }

        [Test]
        public void Double_Dispose_Is_Safe()
        {
            var scope = new CancellationTokenScope();
            scope.Dispose();
            Assert.DoesNotThrow(scope.Dispose);
        }
    }
}
