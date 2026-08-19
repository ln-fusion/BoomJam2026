using System;

namespace Game.Contracts.Time
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
        DateTimeOffset LocalNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset LocalNow => DateTimeOffset.Now;
    }
}
