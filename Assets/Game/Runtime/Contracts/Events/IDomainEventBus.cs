using System;

namespace Game.Contracts.Events
{
    public interface IDomainEvent
    {
    }

    public interface IDomainEventBus
    {
        IDisposable Subscribe<T>(Action<T> handler) where T : IDomainEvent;
        void Publish<T>(T domainEvent) where T : IDomainEvent;
    }
}
