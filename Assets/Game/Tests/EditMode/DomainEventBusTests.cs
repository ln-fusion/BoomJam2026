using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Flow;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 领域事件总线测试：订阅/发布/退订释放/异常隔离.
    /// </summary>
    public class DomainEventBusTests
    {
        private IGameLogger _logger;

        [SetUp]
        public void SetUp() => _logger = new NullLogger(collectEntries: true);

        [Test]
        public void Subscribe_And_Publish_Delivers_Event()
        {
            var bus = new DomainEventBus(_logger);
            var received = 0;
            using (bus.Subscribe<TestDomainEvent>(_ => received++))
            {
                bus.Publish(new TestDomainEvent("a"));
                bus.Publish(new TestDomainEvent("b"));
            }

            Assert.That(received, Is.EqualTo(2));
        }

        [Test]
        public void Dispose_Subscription_Unsubscribes()
        {
            var bus = new DomainEventBus(_logger);
            var received = 0;
            var sub = bus.Subscribe<TestDomainEvent>(_ => received++);
            sub.Dispose();
            bus.Publish(new TestDomainEvent("a"));
            Assert.That(received, Is.EqualTo(0));
        }

        [Test]
        public void Subscriber_Exception_Is_Isolated_And_Logged()
        {
            var bus = new DomainEventBus(_logger);
            var received = 0;
            using (bus.Subscribe<TestDomainEvent>(_ => throw new InvalidOperationException("boom")))
            using (bus.Subscribe<TestDomainEvent>(_ => received++))
            {
                // 不应抛出：异常被隔离
                Assert.DoesNotThrow(() => bus.Publish(new TestDomainEvent("a")));
            }

            Assert.That(received, Is.EqualTo(1));
            var logger = (NullLogger)_logger;
            Assert.That(logger.Entries, Has.Exactly(1).Matches<(LogLevel L, string M)>(e => e.L == LogLevel.Error));
        }

        [Test]
        public void Null_Handler_Throws()
        {
            var bus = new DomainEventBus(_logger);
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<TestDomainEvent>(null!));
        }
    }
}
