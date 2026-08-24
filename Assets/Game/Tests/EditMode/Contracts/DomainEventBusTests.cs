using System;
using System.Threading;
using Game.Contracts;
using Game.Contracts.Lifetime;
using Game.Flow;
using NUnit.Framework;

namespace Game.Tests.EditMode.Contracts
{
    /// <summary>
    /// Contracts 层领域事件总线测试：订阅释放、异常隔离与取消生命周期。
    /// </summary>
    public sealed class DomainEventBusTests
    {
        /// <summary>验证释放订阅后不会收到后续事件。</summary>
        [Test]
        public void DisposedSubscription_DoesNotReceiveLaterEvents()
        {
            var eventBus = new DomainEventBus(null);
            int calls = 0;
            IDisposable subscription = eventBus.Subscribe<TestEvent>(_ => calls++);

            eventBus.Publish(new TestEvent());
            subscription.Dispose();
            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>验证一个订阅者失败不会阻断其他订阅者。</summary>
        [Test]
        public void SubscriberFailure_DoesNotBlockOtherSubscribers()
        {
            var eventBus = new DomainEventBus(null);
            int calls = 0;
            eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("test"));
            eventBus.Subscribe<TestEvent>(_ => calls++);

            Assert.DoesNotThrow(() => eventBus.Publish(new TestEvent()));
            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>验证取消生命周期在释放时会请求取消。</summary>
        [Test]
        public void Lifetime_DisposeCancelsToken()
        {
            var lifetime = new CancellationLifetime(CancellationToken.None);
            CancellationToken token = lifetime.Token;

            lifetime.Dispose();

            Assert.That(token.IsCancellationRequested, Is.True);
        }

        /// <summary>测试事件类型。</summary>
        private sealed class TestEvent : IDomainEvent
        {
        }
    }
}
