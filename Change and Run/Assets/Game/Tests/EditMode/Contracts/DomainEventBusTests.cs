using System;
using System.Threading;
using Game.Contracts.Events;
using Game.Contracts.Lifetime;
using NUnit.Framework;

namespace Game.Tests.EditMode.Contracts
{
    public sealed class DomainEventBusTests
    {
        [Test]
        public void DisposedSubscription_DoesNotReceiveLaterEvents()
        {
            var eventBus = new DomainEventBus();
            int calls = 0;
            IDisposable subscription = eventBus.Subscribe<TestEvent>(_ => calls++);

            eventBus.Publish(new TestEvent());
            subscription.Dispose();
            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void SubscriberFailure_DoesNotBlockOtherSubscribers()
        {
            var eventBus = new DomainEventBus();
            int calls = 0;
            eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("test"));
            eventBus.Subscribe<TestEvent>(_ => calls++);

            Assert.DoesNotThrow(() => eventBus.Publish(new TestEvent()));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Lifetime_DisposeCancelsToken()
        {
            var lifetime = new CancellationLifetime(CancellationToken.None);
            CancellationToken token = lifetime.Token;

            lifetime.Dispose();

            Assert.That(token.IsCancellationRequested, Is.True);
        }

        private sealed class TestEvent : IDomainEvent
        {
        }
    }
}
