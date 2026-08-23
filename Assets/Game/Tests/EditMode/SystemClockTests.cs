using System;
using Game.Contracts;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 系统时钟测试：固定时间可注入，真实时钟走通 DateTimeOffset。
    /// </summary>
    public class SystemClockTests
    {
        /// <summary>验证可注入时钟会返回配置时间。</summary>
        [Test]
        public void FixedClock_Returns_Configured_Time()
        {
            var clock = new FixedClock();
            var now = clock.UtcNow;
            clock.UtcNow = now.AddDays(1);
            Assert.That(clock.UtcNow, Is.EqualTo(now.AddDays(1)));
        }

        /// <summary>验证系统时钟返回当前 UTC 与本地时间。</summary>
        [Test]
        public void SystemClock_Returns_Current_Time()
        {
            var clock = new Game.Flow.SystemClock();
            var before = DateTimeOffset.UtcNow.AddMinutes(-1);
            var after = DateTimeOffset.UtcNow.AddMinutes(1);
            Assert.That(clock.UtcNow, Is.InRange(before, after));
            Assert.That(clock.LocalNow, Is.InRange(before, after));
        }
    }
}
