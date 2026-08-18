using System;
using Game.Contracts;
using Game.Flow;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 测试事件：验证订阅/发布/退订/异常隔离.
    /// </summary>
    public sealed class TestDomainEvent : IDomainEvent
    {
        public string Value { get; }

        public TestDomainEvent(string value) => Value = value;
    }

    /// <summary>
    /// 测试用时钟：固定时间，供 Flow 测试断言用.
    /// </summary>
    public sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        /// <summary>本地时间默认与 UTC 同（测试时区无关）.</summary>
        public DateTimeOffset LocalNow { get; set; } = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    }
}
