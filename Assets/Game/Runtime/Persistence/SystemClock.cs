using System;
using Game.Contracts;

namespace Game.Persistence
{
    internal sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset LocalNow => DateTimeOffset.Now;
    }
}
