using System;
using Game.Contracts;
using Game.Flow;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 测试事件：验证订阅/发布/退订/异常隔离。
    /// </summary>
    public sealed class TestDomainEvent : IDomainEvent
    {
        /// <summary>测试事件的字符串载荷。</summary>
        public string Value { get; }

        /// <summary>创建测试事件。</summary>
        /// <param name="value">测试事件载荷。</param>
        public TestDomainEvent(string value) => Value = value;
    }

    /// <summary>
    /// 测试用时钟：固定时间，供 Flow 测试断言用。
    /// </summary>
    public sealed class FixedClock : IClock
    {
        /// <summary>测试 UTC 时间。</summary>
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        /// <summary>本地时间默认与 UTC 同（测试时区无关）。</summary>
        public DateTimeOffset LocalNow { get; set; } = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    }
}
