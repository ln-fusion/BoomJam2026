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

        /// <summary>创建带收集日志的测试日志器。</summary>
        [SetUp]
        public void SetUp() => _logger = new NullLogger(collectEntries: true);

        /// <summary>验证订阅后发布会按顺序触发回调。</summary>
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

        /// <summary>验证退订后不会再收到后续事件。</summary>
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

        /// <summary>验证单个订阅者抛错不会影响其他订阅者并会被记录。</summary>
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

        /// <summary>验证空处理器会被拒绝。</summary>
        [Test]
        public void Null_Handler_Throws()
        {
            var bus = new DomainEventBus(_logger);
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<TestDomainEvent>(null!));
        }
    }
}
